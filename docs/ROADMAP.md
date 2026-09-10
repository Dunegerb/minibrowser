# Engineering roadmap after the V0.6 architecture pivot

## V0.6 — Zero Browser foundation (this archive)

- identity capsules;
- Anonymous RAM-only RequestContext;
- Personal isolated persistent RequestContext;
- CapabilityBroker;
- NetworkPolicyBroker request choke point;
- capsule-aware audit/history/MachineProtocol v2;
- Trusted UI capsule selector.

## V0.7 — Permission broker enforcement

Wire camera, microphone, geolocation, notifications, clipboard and downloads to CapabilityBroker with explicit trusted UI prompts and per-capsule grants.

## V0.8 — Visual quarantine

First Paint Shield, response-byte capture, shared memory, image hash/perceptual-hash decision cache, CSS/srcset/data/blob/SVG coverage and async MiniMachine decisions.

## V0.9 — Dynamic behavior graph

DOM mutation sensor, visibility/dwell time, semantic click/scroll/focus events, causal IDs, form submission events and replay improvements.

## V0.10 — External Network Broker prototype

Move destination policy and transport toward a separate process. CEF must no longer be treated as the final network authority. Preserve the `NetworkPolicyBroker` contract while replacing its transport implementation.

## Later security milestones

- renderer/browser out-of-process hardening and disposable execution environments;
- identity vault and cryptographic capsule destruction;
- fingerprint uniformity profiles;
- anonymous network overlay / DNS privacy experimentation;
- reproducible builds, multi-party signing and transparency;
- external packet-capture verification and red-team/fuzzing.
