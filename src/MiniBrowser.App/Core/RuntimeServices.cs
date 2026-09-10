using System.IO;
using MiniBrowser.Core.Audit;
using MiniBrowser.Core.Machine;

namespace MiniBrowser.Core;

public sealed class RuntimeServices : IDisposable
{
    public SessionContext Session { get; }
    public NetworkAuditService NetworkAudit { get; }
    public MachineFlightRecorder FlightRecorder { get; }
    public IMachineBridge MachineBridge { get; }

    private RuntimeServices(
        SessionContext session,
        NetworkAuditService networkAudit,
        MachineFlightRecorder flightRecorder,
        IMachineBridge machineBridge)
    {
        Session = session;
        NetworkAudit = networkAudit;
        FlightRecorder = flightRecorder;
        MachineBridge = machineBridge;
    }

    public static RuntimeServices CreateDefault(string appRoot)
    {
        var session = SessionContext.Create();
        var audit = new NetworkAuditService(Path.Combine(appRoot, "audit"));
        var recorder = new MachineFlightRecorder(Path.Combine(appRoot, "flight-recorder"), session);

        // V0.1 deliberately uses a no-op Machine. Swapping this for NamedPipeMachineBridge
        // does not require changing browser/UI code.
        IMachineBridge bridge = new NullMachineBridge(recorder);

        return new RuntimeServices(session, audit, recorder, bridge);
    }

    public void Dispose()
    {
        MachineBridge.Dispose();
        FlightRecorder.Dispose();
        NetworkAudit.Dispose();
    }
}
