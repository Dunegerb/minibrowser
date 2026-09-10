using System.IO;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace MiniBrowser.Core.Audit;

public sealed class NetworkAuditService : IDisposable
{
    private readonly string _directory;
    private readonly Channel<NetworkAuditEntry> _channel;
    private readonly ConcurrentQueue<NetworkAuditEntry> _recent = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _writerTask;
    private const int RecentLimit = 2000;

    public NetworkAuditService(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
        _channel = Channel.CreateUnbounded<NetworkAuditEntry>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
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
        try
        {
            await foreach (var entry in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                var path = Path.Combine(_directory, $"network-{entry.TimestampUtc:yyyyMMdd}.jsonl");
                var json = JsonSerializer.Serialize(entry);
                await File.AppendAllTextAsync(path, json + Environment.NewLine, Encoding.UTF8, _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
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
