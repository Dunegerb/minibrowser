# Architecture — V0.6

```text
                         TRUSTED UI
                   tabs / omnibox / capsule
                              |
                              v
                 +--------------------------+
                 |      Browser Core        |
                 |                          |
                 |  CapsuleManager          |
                 |  CapabilityBroker        |
                 |  NetworkPolicyBroker     |
                 |  MachineBridge           |
                 +------------+-------------+
                              |
                 policy / isolated context
                              |
                              v
                 +--------------------------+
                 | CEF / Chromium           |
                 | untrusted web renderer   |
                 +------------+-------------+
                              |
                              v
                           INTERNET
```

## Current trust boundary

- Web content is not trusted.
- Website JavaScript never receives direct access to `MachineBridge`, capsule management or Browser Core services.
- Tabs in different capsules receive different CEF `RequestContext` instances.
- Anonymous capsule storage is in-memory; Personal capsule website state is stored in an isolated path.
- `NetworkPolicyBroker` is called before resource loads and can block filesystem/download/unknown-scheme access.
- Network Audit records capsule + policy outcome locally.

## Important limitation

CEF still owns sockets and DNS in V0.6. Therefore the current `NetworkPolicyBroker` is an enforcement choke point, not yet the final physically separated Network Broker.

## Browser / Machine invariant

Browser Core reports **what happened**, where, when and under which capsule/policy.
MiniMachine decides **what it means** and what action to take.

The Machine protocol remains independent of the renderer implementation.
