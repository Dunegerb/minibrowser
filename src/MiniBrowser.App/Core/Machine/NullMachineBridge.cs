namespace MiniBrowser.Core.Machine;

public sealed class NullMachineBridge : IMachineBridge
{
    private readonly MachineFlightRecorder _recorder;

    public bool ResourceEventsEnabled => false;

    public NullMachineBridge(MachineFlightRecorder recorder)
    {
        _recorder = recorder;
    }

    public Task PublishAsync(MachineEventEnvelope evt, CancellationToken cancellationToken)
    {
        _recorder.Record(evt);
        return Task.CompletedTask;
    }

    public Task<MachineDecision> DecideAsync(MachineEventEnvelope evt, CancellationToken cancellationToken)
    {
        _recorder.Record(evt);
        return Task.FromResult(MachineDecision.Allow(
            evt.Payload.TryGetValue("resource_id", out var id) ? id?.ToString() ?? string.Empty : string.Empty));
    }

    public void Dispose()
    {
    }
}
