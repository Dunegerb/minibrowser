# V0.6 patch notes

Base: MiniBrowser V0.5 optimized.

## Added

- `CapsuleManager` and `CapsuleDescriptor`
- Anonymous RAM-only CEF RequestContext
- Personal persistent isolated CEF RequestContext
- `CapabilityBroker`
- `NetworkPolicyBroker`
- capsule-aware network audit
- capsule-aware RAM-only session history
- MachineProtocol v2 with `capsule_id`
- capsule selector in Trusted UI
- `Ctrl+Shift+N` Anonymous tab / `Ctrl+Shift+P` Personal tab

## Changed

- CEF global profile moved under `cef-root/global`; it is no longer the tab identity store.
- popups remain native-blocked and are opened in the same capsule when popup capability permits.
- `file://`, downloads and unknown schemes are denied by default by policy.
- Clear Cache now targets the active capsule RequestContext.

## Not yet claimed

NetworkPolicyBroker is currently in-process and CEF still owns sockets/DNS. The V0.6 architecture is deliberately shaped so the actual transport can be moved out later without changing UI/Machine/capsule contracts.
