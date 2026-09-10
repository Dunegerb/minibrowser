using CefSharp;
using CefSharp.Handler;
using MiniBrowser.Core;
using MiniBrowser.Core.Audit;
using MiniBrowser.Core.Capsules;
using MiniBrowser.Core.Machine;
using MiniBrowser.Core.Networking;

namespace MiniBrowser.Cef;

/// <summary>
/// Per-request policy choke point. V0.6 adds capsule-aware capability/policy decisions.
/// CEF still owns sockets in this milestone; NetworkPolicyBroker is therefore an enforcement
/// hook, not yet the final out-of-process Network Broker described by Zero Browser Architecture.
/// </summary>
public sealed class MiniResourceRequestHandler : ResourceRequestHandler
{
    private readonly SessionContext _session;
    private readonly int _tabId;
    private readonly CapsuleDescriptor _capsule;
    private readonly NetworkAuditService _audit;
    private readonly IMachineBridge _machine;
    private readonly NetworkPolicyBroker _networkPolicy;
    private readonly bool _isNavigation;
    private readonly bool _isDownload;
    private readonly string _requestInitiator;

    public MiniResourceRequestHandler(
        SessionContext session,
        int tabId,
        CapsuleDescriptor capsule,
        NetworkAuditService audit,
        IMachineBridge machine,
        NetworkPolicyBroker networkPolicy,
        bool isNavigation,
        bool isDownload,
        string requestInitiator)
    {
        _session = session;
        _tabId = tabId;
        _capsule = capsule;
        _audit = audit;
        _machine = machine;
        _networkPolicy = networkPolicy;
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
            var requestUrl = request.Url ?? string.Empty;
            var uri = Uri.TryCreate(requestUrl, UriKind.Absolute, out var parsed) ? parsed : null;
            var domain = uri?.Host ?? string.Empty;
            var urlHash = NetworkAuditService.HashUrl(requestUrl);
            var frameId = frame?.Identifier ?? string.Empty;
            var resourceType = request.ResourceType.ToString();
            var policy = _networkPolicy.Evaluate(_capsule, requestUrl, _isDownload, resourceType);
            var policyAction = policy.Action.ToString().ToUpperInvariant();

            _audit.Publish(new NetworkAuditEntry(
                DateTimeOffset.UtcNow,
                _session.MonotonicMicroseconds(),
                _session.SessionId,
                _session.WindowId,
                _tabId,
                _capsule.Id,
                frameId,
                request.Identifier,
                request.Method ?? string.Empty,
                domain,
                urlHash,
                resourceType,
                _requestInitiator,
                _isNavigation,
                _isDownload,
                policyAction,
                policy.Reason));

            if (_machine.ResourceEventsEnabled)
            {
                var evt = MachineEventFactory.ResourceRequest(
                    _session,
                    _tabId,
                    _capsule.Id,
                    frameId,
                    request.Identifier.ToString(),
                    resourceType,
                    domain,
                    urlHash,
                    request.Method ?? string.Empty,
                    _requestInitiator,
                    _isNavigation,
                    _isDownload,
                    policyAction,
                    policy.Reason);

                _ = _machine.PublishAsync(evt, CancellationToken.None);
            }

            return policy.IsAllowed ? CefReturnValue.Continue : CefReturnValue.Cancel;
        }
        catch
        {
            // Development fail-open for sensor bugs. Security milestones will make fail policy
            // configurable by capsule once the broker runs outside CEF.
            return CefReturnValue.Continue;
        }
        finally
        {
            if (!callback.IsDisposed) callback.Dispose();
        }
    }
}
