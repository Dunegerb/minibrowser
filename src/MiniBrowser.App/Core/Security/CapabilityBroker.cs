using MiniBrowser.Core.Capsules;
using System.Collections.Concurrent;

namespace MiniBrowser.Core.Security;

/// <summary>
/// Central policy table for renderer capabilities. In V0.6 the network/filesystem/download
/// decisions are consumed by NetworkPolicyBroker. Device/clipboard permission hooks are slots
/// for the next milestone and are deliberately denied by default here.
/// </summary>
public sealed class CapabilityBroker
{
    private readonly ConcurrentDictionary<(string CapsuleId, BrowserCapability Capability), bool> _sessionOverrides = new();

    public bool IsAllowed(CapsuleDescriptor capsule, BrowserCapability capability)
    {
        if (_sessionOverrides.TryGetValue((capsule.Id, capability), out var overridden))
        {
            return overridden;
        }

        return capability switch
        {
            BrowserCapability.Network => true,
            BrowserCapability.Popups => true,     // popups are re-routed into trusted tabs
            BrowserCapability.DevTools => true,   // development build; can be disabled later
            _ => false
        };
    }

    public void SetForSession(CapsuleDescriptor capsule, BrowserCapability capability, bool allowed) =>
        _sessionOverrides[(capsule.Id, capability)] = allowed;

    public void ResetCapsule(CapsuleDescriptor capsule)
    {
        foreach (var key in _sessionOverrides.Keys.Where(key => key.CapsuleId == capsule.Id).ToArray())
        {
            _sessionOverrides.TryRemove(key, out _);
        }
    }
}
