using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using MiniBrowser.Core.Storage;

namespace MiniBrowser.Core.Audit;

public sealed class NetworkAuditService : IDisposable
{
    private readonly string _directory;
    private readonly Channel<NetworkAuditEntry> _channel;
    private readonly ConcurrentQueue<NetworkAuditEntry> _recent = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _writerTask;
    private const int RecentLimit = 1200;

    public NetworkAuditService(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
        CleanupLegacyFiles();

        _channel = Channel.CreateBounded<NetworkAuditEntry>(new BoundedChannelOptions(4096)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _writerTask = Task.Run(WriterLoopAsync);
    }

    public static string HashUrl(string url)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public void Publish(NetworkAuditEntry entry)
    {
        _recent.Enqueue(entry);
        while (_recent.Count > RecentLimit && _recent.TryDequeue(out _)) { }
        _channel.Writer.TryWrite(entry);
    }

    public IReadOnlyList<NetworkAuditEntry> Snapshot() => _recent.ToArray().Reverse().ToArray();

    private async Task WriterLoopAsync()
    {
        StreamWriter? writer = null;
        try
        {
            writer = OpenCurrentWriter();
            var pending = 0;
            var lastFlush = Environment.TickCount64;

            await foreach (var entry in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                if (writer.BaseStream.Length >= StoragePolicy.AuditFileBytes)
                {
                    await writer.FlushAsync(_cts.Token);
                    writer.Dispose();
                    RotateFiles();
                    writer = OpenCurrentWriter();
                }

                await writer.WriteLineAsync(JsonSerializer.Serialize(entry));
                pending++;

                if (pending >= 128 || Environment.TickCount64 - lastFlush >= 1000)
                {
                    await writer.FlushAsync(_cts.Token);
                    pending = 0;
                    lastFlush = Environment.TickCount64;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (writer is not null)
            {
                try { await writer.FlushAsync(); } catch { }
                writer.Dispose();
            }
        }
    }

    private StreamWriter OpenCurrentWriter()
    {
        var path = Path.Combine(_directory, "network-current.jsonl");
        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 64 * 1024, useAsync: true);
        return new StreamWriter(stream, new UTF8Encoding(false), 64 * 1024);
    }

    private void RotateFiles()
    {
        for (var i = StoragePolicy.AuditFileCount - 1; i >= 1; i--)
        {
            var source = i == 1
                ? Path.Combine(_directory, "network-current.jsonl")
                : Path.Combine(_directory, $"network-{i - 1}.jsonl");
            var destination = Path.Combine(_directory, $"network-{i}.jsonl");

            try
            {
                if (File.Exists(destination)) File.Delete(destination);
                if (File.Exists(source)) File.Move(source, destination);
            }
            catch { }
        }
    }

    private void CleanupLegacyFiles()
    {
        foreach (var path in Directory.EnumerateFiles(_directory, "network-20*.jsonl"))
        {
            try { File.Delete(path); } catch { }
        }
    }

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _cts.Cancel();
        try { _writerTask.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts.Dispose();
    }
}
