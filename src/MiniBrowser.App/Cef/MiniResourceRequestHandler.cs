using CefSharp;
using CefSharp.Handler;
using MiniBrowser.Core;
using MiniBrowser.Core.Audit;
using MiniBrowser.Core.Machine;

namespace MiniBrowser.Cef;

/// <summary>
/// One instance per resource request. V0.1 audits all requests and emits protocol observations.
/// Visual bytes/quarantine hooks intentionally live here in the next milestone rather than in UI code.
/// </summary>
public sealed class MiniResourceRequestHandler : ResourceRequestHandler
{
    private readonly SessionContext _session;
    private readonly int _tabId;
    private readonly NetworkAuditService _audit;
    private readonly IMachineBridge _machine;
    private readonly bool _isNavigation;
    private readonly bool _isDownload;
    private readonly string _requestInitiator;

    public MiniResourceRequestHandler(
        SessionContext session,
        int tabId,
        NetworkAuditService audit,
        IMachineBridge machine,
        bool isNavigation,
        bool isDownload,
        string requestInitiator)
    {
        _session = session;
        _tabId = tabId;
        _audit = audit;
        _machine = machine;
        _isNavigation = isNavigation;
        _isDownload = isDownload;
        _requestInitiator = requestInitiator ?? string.Empty;
    }

    protected override CefReturnValue OnBeforeResourceLoad(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        IFrame frame,
        IRequest request,
        IRequestCallback callback)
    {
        try
        {
            var uri = Uri.TryCreate(request.Url, UriKind.Absolute, out var parsed) ? parsed : null;
            var domain = uri?.Host ?? string.Empty;
            var urlHash = NetworkAuditService.HashUrl(request.Url ?? string.Empty);
            var frameId = frame?.Identifier ?? string.Empty;
            var resourceType = request.ResourceType.ToString();

            _audit.Publish(new NetworkAuditEntry(
                DateTimeOffset.UtcNow,
                _session.MonotonicMicroseconds(),
                _session.SessionId,
                _session.WindowId,
                _tabId,
                frameId,
                request.Identifier,
                request.Method ?? string.Empty,
                domain,
                urlHash,
                resourceType,
                _requestInitiator,
                _isNavigation,
                _isDownload));

            var evt = MachineEventFactory.ResourceRequest(
                _session,
                _tabId,
                frameId,
                request.Identifier.ToString(),
                resourceType,
                domain,
                urlHash,
                request.Method ?? string.Empty,
                _requestInitiator,
                _isNavigation,
                _isDownload);

            _ = _machine.PublishAsync(evt, CancellationToken.None);

            // IMPORTANT: V0.1 is observation-only. Do not block the CEF IO thread waiting for Machine.
            // The Visual Quarantine milestone will use async callbacks/shared memory and explicit fail-safe policy.
            return CefReturnValue.Continue;
        }
        catch
        {
            // Development fail-open: a bug in audit/sensor code must not brick navigation.
            return CefReturnValue.Continue;
        }
        finally
        {
            if (!callback.IsDisposed) callback.Dispose();
        }
    }
}
