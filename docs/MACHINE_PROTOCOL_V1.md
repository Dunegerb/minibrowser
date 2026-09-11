# Citra ↔ MiniMachine protocol v1

Transport on Windows:

```text
\\.\pipe\MiniMachineBrain
```

One JSON event followed by `\n`, then one JSON response followed by `\n`.

Visual event:

```json
{
  "event": "visual.resource",
  "version": 1,
  "session_id": "...",
  "capsule_id": "anon-...",
  "tab_id": 1,
  "navigation_id": 4,
  "resource_id": "img-2",
  "timestamp_utc_ms": 0,
  "page": {
    "url_hash": "...",
    "domain": "example.com"
  },
  "resource": {
    "url_hash": "...",
    "mime": "image/jpeg",
    "byte_len": 10000,
    "width": 800,
    "height": 600,
    "sha256": "...",
    "preview_width": 128,
    "preview_height": 96,
    "preview_rgba_base64": "..."
  }
}
```

Response:

```json
{
  "resource_id": "img-2",
  "decision": "ALLOW",
  "risk": 0.1,
  "confidence": 0.9,
  "cache_for_seconds": 3600,
  "reason": "..."
}
```

Decisions understood by Citra 0.1:

- `ALLOW`: pixels may cross the visual gate into UI.
- `BLOCK`: neutral placeholder.
- `BLUR`: quarantined placeholder in 0.1; actual blur lands later.
- `REPLACE`: quarantined placeholder in 0.1.
- `DEFER`: quarantined placeholder.
- `SCAN_MORE`: quarantined placeholder.

Citra uses an in-memory decision cache keyed by SHA-256. No visual decision is persisted in 0.1.
