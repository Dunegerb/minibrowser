namespace MiniBrowser.Core.Machine;

public interface IMachineBridge : IDisposable
{
    /// <summary>
    /// True when the active Machine bridge wants the high-volume network resource stream.
    /// The no-op development bridge keeps this false so normal browsing does not allocate
    /// a protocol envelope for every image/script/font before MiniMachine exists.
    /// </summary>
    bool ResourceEventsEnabled { get; }

    Task PublishAsync(MachineEventEnvelope evt, CancellationToken cancellationToken);
    Task<MachineDecision> DecideAsync(MachineEventEnvelope evt, CancellationToken cancellationToken);
}
