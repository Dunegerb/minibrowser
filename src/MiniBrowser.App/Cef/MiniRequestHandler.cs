using CefSharp;
using CefSharp.Handler;
using MiniBrowser.Core;
using MiniBrowser.Core.Audit;
using MiniBrowser.Core.Capsules;
using MiniBrowser.Core.Machine;
using MiniBrowser.Core.Networking;

namespace MiniBrowser.Cef;

public sealed class MiniRequestHandler : RequestHandler
{
    private readonly SessionContext _session;
    private readonly int _tabId;
    private readonly CapsuleDescriptor _capsule;
    private readonly NetworkAuditService _audit;
    private readonly IMachineBridge _machine;
    private readonly NetworkPolicyBroker _networkPolicy;

    public MiniRequestHandler(
        SessionContext session,
        int tabId,
        CapsuleDescriptor capsule,
        NetworkAuditService audit,
        IMachineBridge machine,
        NetworkPolicyBroker networkPolicy)
    {
        _session = session;
        _tabId = tabId;
        _capsule = capsule;
        _audit = audit;
        _machine = machine;
        _networkPolicy = networkPolicy;
    }

    protected override IResourceRequestHandler GetResourceRequestHandler(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        IFrame frame,
        IRequest request,
        bool isNavigation,
        bool isDownload,
        string requestInitiator,
        ref bool disableDefaultHandling)
    {
        return new MiniResourceRequestHandler(
            _session,
            _tabId,
            _capsule,
            _audit,
            _machine,
            _networkPolicy,
            isNavigation,
            isDownload,
            requestInitiator);
    }
}
