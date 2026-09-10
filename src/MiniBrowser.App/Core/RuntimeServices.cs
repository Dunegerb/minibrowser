using System.IO;
using MiniBrowser.Core.Audit;
using MiniBrowser.Core.Capsules;
using MiniBrowser.Core.History;
using MiniBrowser.Core.Machine;
using MiniBrowser.Core.Networking;
using MiniBrowser.Core.Security;

namespace MiniBrowser.Core;

public sealed class RuntimeServices : IDisposable
{
    public SessionContext Session { get; }
    public NetworkAuditService NetworkAudit { get; }
    public MachineFlightRecorder FlightRecorder { get; }
    public SessionHistoryService History { get; }
    public IMachineBridge MachineBridge { get; }
    public CapsuleManager Capsules { get; }
    public CapabilityBroker Capabilities { get; }
    public NetworkPolicyBroker NetworkPolicy { get; }

    private RuntimeServices(
        SessionContext session,
        NetworkAuditService networkAudit,
        MachineFlightRecorder flightRecorder,
        SessionHistoryService history,
        IMachineBridge machineBridge,
        CapsuleManager capsules,
        CapabilityBroker capabilities,
        NetworkPolicyBroker networkPolicy)
    {
        Session = session;
        NetworkAudit = networkAudit;
        FlightRecorder = flightRecorder;
        History = history;
        MachineBridge = machineBridge;
        Capsules = capsules;
        Capabilities = capabilities;
        NetworkPolicy = networkPolicy;
    }

    public static RuntimeServices CreateDefault(string appRoot)
    {
        var session = SessionContext.Create();
        var audit = new NetworkAuditService(Path.Combine(appRoot, "audit"));
        var recorder = new MachineFlightRecorder(Path.Combine(appRoot, "flight-recorder"), session);
        var history = new SessionHistoryService();
        var capsules = new CapsuleManager(appRoot);
        var capabilities = new CapabilityBroker();
        var networkPolicy = new NetworkPolicyBroker(capabilities);
        IMachineBridge bridge = new NullMachineBridge(recorder);

        return new RuntimeServices(
            session,
            audit,
            recorder,
            history,
            bridge,
            capsules,
            capabilities,
            networkPolicy);
    }

    public void Dispose()
    {
        MachineBridge.Dispose();
        FlightRecorder.Dispose();
        NetworkAudit.Dispose();
        Capsules.Dispose();
    }
}
