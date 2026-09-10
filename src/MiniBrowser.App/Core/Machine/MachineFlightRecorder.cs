using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace MiniBrowser.Core.Machine;

public sealed class MachineFlightRecorder : IDisposable
{
    private readonly string _directory;
    private readonly SessionContext _session;
    private readonly Channel<MachineEventEnvelope> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _writerTask;
    private const long MaxDailyFileBytes = 16L * 1024 * 1024;
    private const long MaxPreservedBytes = 128L * 1024 * 1024;
    private const int MaxPreservedSnapshots = 20;

    public MachineFlightRecorder(string directory, SessionContext session)
    {
        _directory = directory;
        _session = session;
        Directory.CreateDirectory(_directory);
        CleanupExpiredFiles();
        TrimOversizedLegacyFiles();
        CleanupPreservedSnapshots();

        _channel = Channel.CreateBounded<MachineEventEnvelope>(new BoundedChannelOptions(2048)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _writerTask = Task.Run(WriterLoopAsync);
    }

    public void Record(MachineEventEnvelope evt)
    {
        // Raw network traffic already exists in the bounded Network Audit.
        // The Flight Recorder is for compact semantic events, not a duplicate request log.
        if (evt.Event == "network.resource.request") return;
        _channel.Writer.TryWrite(evt);
    }

    private async Task WriterLoopAsync()
    {
        try
        {
            await foreach (var evt in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                var path = Path.Combine(_directory, $"events-{evt.TimestampUtc:yyyyMMdd}.jsonl");
                if (File.Exists(path) && new FileInfo(path).Length >= MaxDailyFileBytes)
                {
                    continue;
                }

                var json = JsonSerializer.Serialize(evt);
                await File.AppendAllTextAsync(path, json + Environment.NewLine, Encoding.UTF8, _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async Task<string> PreserveRecentAsync(string reason, CancellationToken cancellationToken)
    {
        var snapshots = Path.Combine(_directory, "preserved");
        Directory.CreateDirectory(snapshots);
        var output = Path.Combine(
            snapshots,
            $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{reason}-{_session.SessionId:N}.jsonl.gz");

        var cutoff = DateTime.UtcNow.AddHours(-72);
        var inputs = Directory.EnumerateFiles(_directory, "events-*.jsonl")
            .Where(path => File.GetLastWriteTimeUtc(path) >= cutoff)
            .OrderBy(path => path)
            .ToArray();

        await using (var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 64 * 1024, true))
        await using (var destination = new GZipStream(file, CompressionLevel.SmallestSize, leaveOpen: false))
        {
            foreach (var input in inputs)
            {
                await using var source = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, true);
                await source.CopyToAsync(destination, cancellationToken);
            }
        }

        CleanupPreservedSnapshots();
        return output;
    }

    private void TrimOversizedLegacyFiles()
    {
        foreach (var path in Directory.EnumerateFiles(_directory, "events-*.jsonl"))
        {
            try
            {
                var info = new FileInfo(path);
                if (info.Length <= MaxDailyFileBytes) continue;

                var temp = path + ".trim";
                using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    input.Seek(-MaxDailyFileBytes, SeekOrigin.End);
                    using var reader = new StreamReader(
                        input,
                        Encoding.UTF8,
                        detectEncodingFromByteOrderMarks: true,
                        bufferSize: 64 * 1024,
                        leaveOpen: true);
                    _ = reader.ReadLine(); // discard a potentially partial first JSON line
                    var tail = reader.ReadToEnd();
                    File.WriteAllText(temp, tail, new UTF8Encoding(false));
                }

                File.Move(temp, path, overwrite: true);
            }
            catch { }
        }
    }

    private void CleanupPreservedSnapshots()
    {
        var snapshots = Path.Combine(_directory, "preserved");
        if (!Directory.Exists(snapshots)) return;

        var files = Directory.EnumerateFiles(snapshots)
            .Where(path => path.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".jsonl.gz", StringComparison.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .ToList();

        long keptBytes = 0;
        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            keptBytes += file.Length;
            if (index < MaxPreservedSnapshots && keptBytes <= MaxPreservedBytes) continue;

            try { file.Delete(); } catch { }
        }
    }

    private void CleanupExpiredFiles()
    {
        var cutoff = DateTime.UtcNow.AddHours(-72);
        foreach (var path in Directory.EnumerateFiles(_directory, "events-*.jsonl"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(path) < cutoff) File.Delete(path);
            }
            catch { }
        }
    }

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _cts.Cancel();
        try { _writerTask.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _cts.Dispose();
    }
}
