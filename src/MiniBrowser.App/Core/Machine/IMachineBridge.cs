namespace MiniBrowser.Core.Machine;

public interface IMachineBridge : IDisposable
{
    Task PublishAsync(MachineEventEnvelope evt, CancellationToken cancellationToken);
    Task<MachineDecision> DecideAsync(MachineEventEnvelope evt, CancellationToken cancellationToken);
}
