# MachineProtocol v2

V2 extends the V1 envelope with `capsule_id`.

```json
{
  "event": "network.resource.request",
  "version": 2,
  "session_id": "...",
  "window_id": "...",
  "tab_id": 4,
  "capsule_id": "anon-a1b2c3d4e5f6",
  "frame_id": "...",
  "timestamp_utc": "...",
  "monotonic_us": 123456,
  "causal_id": "...",
  "payload": {
    "resource_id": "...",
    "resource_type": "Image",
    "domain": "example.com",
    "url_hash": "...",
    "policy_action": "ALLOW",
    "policy_reason": "network capability granted"
  }
}
```

Decision values remain:

```text
ALLOW
BLOCK
BLUR
REPLACE
DEFER
SCAN_MORE
```

V2 does not expose cookies, passwords, payment data or raw profile contents to MiniMachine.
