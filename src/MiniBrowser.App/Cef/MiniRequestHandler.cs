using CefSharp;
using CefSharp.Handler;
using MiniBrowser.Core;
using MiniBrowser.Core.Audit;
using MiniBrowser.Core.Machine;

namespace MiniBrowser.Cef;

public sealed class MiniRequestHandler : RequestHandler
{
    private readonly SessionContext _session;
    private readonly int _tabId;
    private readonly NetworkAuditService _audit;
    private readonly IMachineBridge _machine;

    public MiniRequestHandler(
        SessionContext session,
        int tabId,
        NetworkAuditService audit,
        IMachineBridge machine)
    {
        _session = session;
        _tabId = tabId;
        _audit = audit;
        _machine = machine;
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
            _audit,
            _machine,
            isNavigation,
            isDownload,
            requestInitiator);
    }
}
