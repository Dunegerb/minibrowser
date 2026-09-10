using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace MiniBrowser.Core.Machine;

/// <summary>
/// Protocol scaffold for MiniMachineCore. Not enabled in V0.1 because the core process does not exist yet.
/// Messages are newline-delimited JSON over \\.\pipe\MiniMachineBrain.
/// The server side must enforce a Windows pipe ACL restricting access to the intended user/process identity.
/// </summary>
public sealed class NamedPipeMachineBridge : IMachineBridge
{
    public const string PipeName = "MiniMachineBrain";
    private readonly MachineFlightRecorder _recorder;
    private readonly TimeSpan _connectTimeout;

    public NamedPipeMachineBridge(MachineFlightRecorder recorder, TimeSpan? connectTimeout = null)
    {
        _recorder = recorder;
        _connectTimeout = connectTimeout ?? TimeSpan.FromMilliseconds(80);
    }

    public async Task PublishAsync(MachineEventEnvelope evt, CancellationToken cancellationToken)
    {
        _recorder.Record(evt);
        await SendAsync(evt, expectDecision: false, cancellationToken);
    }

    public async Task<MachineDecision> DecideAsync(MachineEventEnvelope evt, CancellationToken cancellationToken)
    {
        _recorder.Record(evt);
        return await SendAsync(evt, expectDecision: true, cancellationToken)
               ?? MachineDecision.Allow(); // development fail-open policy
    }

    private async Task<MachineDecision?> SendAsync(
        MachineEventEnvelope evt,
        bool expectDecision,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_connectTimeout);

        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeout.Token);

            await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true)
            {
                AutoFlush = true
            };
            using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, 1024, leaveOpen: true);

            await writer.WriteLineAsync(JsonSerializer.Serialize(evt));
            if (!expectDecision) return null;

            var line = await reader.ReadLineAsync(timeout.Token);
            return string.IsNullOrWhiteSpace(line)
                ? null
                : JsonSerializer.Deserialize<MachineDecision>(line);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Dispose()
    {
    }
}
