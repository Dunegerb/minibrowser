# Zero Browser Architecture — V0.6 boundary

V0.6 is the first architectural cut where Chromium is treated as an untrusted renderer rather than the owner of browser identity.

## Implemented now

```text
Trusted WPF UI
    |
    +-- CapsuleManager
    |     +-- Anonymous capsule -> CEF RequestContext with empty CachePath (RAM/incognito)
    |     `-- Personal capsule  -> isolated persistent RequestContext
    |
    +-- CapabilityBroker
    |     +-- network: allowed
    |     +-- popup: allowed only as MiniBrowser tab
    |     +-- devtools: allowed in development
    |     `-- filesystem/device/download capabilities: denied by default
    |
    +-- NetworkPolicyBroker
    |     +-- http/https/ws/wss -> capability checked
    |     +-- file:// -> denied by default
    |     +-- unknown schemes -> denied
    |     `-- decision written to local Network Audit
    |
    +-- MachineBridge / MachineProtocol v2
    |     `-- every event can carry capsule_id
    |
    `-- CEF/Chromium renderer
```

## Security property introduced by V0.6

State from the Anonymous capsule is not stored in a persistent CEF CachePath. State from the Personal capsule is stored in a dedicated RequestContext path and is not shared with Anonymous tabs.

Switching capsule creates a new renderer tab instead of changing the identity context of an existing tab.

## What V0.6 does NOT claim

V0.6 does **not** yet provide a physically separate network service. Chromium/CEF still owns the socket/DNS implementation. `NetworkPolicyBroker` is the policy choke point that will survive when networking is moved out of the engine.

V0.6 also does not yet implement:

- microVM per origin;
- anonymous onion/multipath overlay;
- Oblivious DoH/OHTTP;
- standardized anti-fingerprinting profile;
- out-of-process Capability Broker;
- camera/microphone/geolocation/clipboard permission handlers wired into CEF;
- encrypted persistent vault;
- cryptographic capsule destruction;
- Visual Quarantine / First Paint Shield;
- shared-memory visual classification;
- signed/reproducible updater.

Those are milestones layered on the interfaces introduced here, not reasons to move identity back into Chromium.

## Disk model

Anonymous:

```text
RequestContext CachePath = empty
cookies/cache/localStorage/IndexedDB -> memory for this process lifetime
shutdown -> context destroyed
```

Personal:

```text
%LOCALAPPDATA%\MiniBrowser\cef-root\capsules\personal\
```

The Personal capsule persists website state, while volatile Chromium cache directories are trimmed on startup. History remains MiniBrowser-owned, RAM-only and limited to the current process session.

## Browser/Machine separation

MachineProtocol v2 adds `capsule_id` to the envelope. MiniMachine can therefore reason about exposure and behavior without receiving a global identity profile from the browser.

CEF remains replaceable. Capsule, capability, network-policy and Machine contracts are intended to survive a future renderer change.
