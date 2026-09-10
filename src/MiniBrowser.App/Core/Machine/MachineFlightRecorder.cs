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

    public MachineFlightRecorder(string directory, SessionContext session)
    {
        _directory = directory;
        _session = session;
        Directory.CreateDirectory(_directory);
        CleanupExpiredFiles();

        _channel = Channel.CreateUnbounded<MachineEventEnvelope>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _writerTask = Task.Run(WriterLoopAsync);
    }

    public void Record(MachineEventEnvelope evt) => _channel.Writer.TryWrite(evt);

    private async Task WriterLoopAsync()
    {
        try
        {
            await foreach (var evt in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                var path = Path.Combine(_directory, $"events-{evt.TimestampUtc:yyyyMMdd}.jsonl");
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
            $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{reason}-{_session.SessionId:N}.jsonl");

        var cutoff = DateTime.UtcNow.AddHours(-72);
        var inputs = Directory.EnumerateFiles(_directory, "events-*.jsonl")
            .Where(path => File.GetLastWriteTimeUtc(path) >= cutoff)
            .OrderBy(path => path)
            .ToArray();

        await using var destination = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 65536, true);
        foreach (var input in inputs)
        {
            await using var source = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, true);
            await source.CopyToAsync(destination, cancellationToken);
        }

        return output;
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
            catch
            {
                // Local diagnostic retention must never crash browser startup.
            }
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
