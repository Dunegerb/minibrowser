# MachineProtocol v1

Browser event envelope:

```json
{
  "event": "network.resource.request",
  "version": 1,
  "sessionId": "...",
  "windowId": "...",
  "tabId": 1,
  "frameId": 0,
  "timestampUtc": "...",
  "monotonicUs": 123456,
  "causalId": "...",
  "payload": {}
}
```

Decision:

```json
{
  "resourceId": "887",
  "decision": "Block",
  "risk": 0.91,
  "confidence": 0.87,
  "cacheForSeconds": 86400
}
```

Transport target: Windows named pipe `\\.\pipe\MiniMachineBrain`.

V0.1 ships the transport scaffold but uses `NullMachineBridge` so that the browser is usable before MiniMachineCore exists.
