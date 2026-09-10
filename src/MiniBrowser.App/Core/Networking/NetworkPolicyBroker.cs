using MiniBrowser.Core.Capsules;
using MiniBrowser.Core.Security;

namespace MiniBrowser.Core.Networking;

public enum NetworkPolicyAction
{
    Allow,
    Block
}

public sealed record NetworkPolicyDecision(NetworkPolicyAction Action, string Reason)
{
    public bool IsAllowed => Action == NetworkPolicyAction.Allow;

    public static NetworkPolicyDecision Allow(string reason) => new(NetworkPolicyAction.Allow, reason);
    public static NetworkPolicyDecision Block(string reason) => new(NetworkPolicyAction.Block, reason);
}

/// <summary>
/// V0.6 policy choke point. Today CEF still owns the actual socket stack; this broker is invoked
/// before resource loads and can deny them. The long-term Zero Browser design moves the socket/DNS
/// implementation out of the renderer/browser engine entirely without changing this policy API.
/// </summary>
public sealed class NetworkPolicyBroker
{
    private readonly CapabilityBroker _capabilities;

    public NetworkPolicyBroker(CapabilityBroker capabilities)
    {
        _capabilities = capabilities;
    }

    public NetworkPolicyDecision Evaluate(
        CapsuleDescriptor capsule,
        string? url,
        bool isDownload,
        string resourceType)
    {
        if (isDownload && !_capabilities.IsAllowed(capsule, BrowserCapability.Downloads))
        {
            return NetworkPolicyDecision.Block("download capability denied");
        }

        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return NetworkPolicyDecision.Block("malformed or missing URL");
        }

        var scheme = uri.Scheme.ToLowerInvariant();

        if (scheme is "http" or "https" or "ws" or "wss")
        {
            return _capabilities.IsAllowed(capsule, BrowserCapability.Network)
                ? NetworkPolicyDecision.Allow("network capability granted")
                : NetworkPolicyDecision.Block("network capability denied");
        }

        // These schemes do not grant a raw filesystem/network capability by themselves.
        if (scheme is "about" or "data" or "blob")
        {
            return NetworkPolicyDecision.Allow("renderer-local scheme");
        }

        if (scheme == "file")
        {
            return _capabilities.IsAllowed(capsule, BrowserCapability.Filesystem)
                ? NetworkPolicyDecision.Allow("filesystem capability granted")
                : NetworkPolicyDecision.Block("filesystem capability denied");
        }

        if (scheme is "chrome" or "devtools" or "chrome-extension")
        {
            return _capabilities.IsAllowed(capsule, BrowserCapability.DevTools)
                ? NetworkPolicyDecision.Allow("trusted developer scheme")
                : NetworkPolicyDecision.Block("developer scheme denied");
        }

        return NetworkPolicyDecision.Block($"scheme '{scheme}' is not allowlisted");
    }
}
