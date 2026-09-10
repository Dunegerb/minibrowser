# Architecture

```text
INTERNET
   |
   v
CEF request pipeline
   |
   +--> MiniRequestHandler / MiniResourceRequestHandler
   |       |--> NetworkAuditService (local JSONL)
   |       `--> IMachineBridge
   |               |--> NullMachineBridge (V0.1)
   |               `--> NamedPipeMachineBridge (scaffold)
   |
   v
CEF / Chromium renderer
   |
   v
MiniBrowser UI
```

The invariant is intentional:

- Browser code reports **what happened**, where and when.
- Machine code decides **what it means** and what action to take.
- Website JavaScript never receives a direct reference to MachineBridge.

## Protocol boundary

`MachineEventEnvelope` is versioned independently from the engine. The initial events are:

- `network.resource.request`
- `search.before_submit`
- `relapse.confirmed`

The initial decision vocabulary is:

- `Allow`
- `Block`
- `Blur`
- `Replace`
- `Defer`
- `ScanMore`

## Next milestone: resource/visual gate

The next implementation should remain inside the CEF request/render integration layer:

1. detect visual resource candidates before display;
2. quarantine unknown visuals;
3. collect metadata and bytes without pushing MB-sized blobs through JSON;
4. put bytes in shared memory and send a descriptor through MachineProtocol;
5. ask Machine asynchronously;
6. release, block, blur or replace according to the decision;
7. cache by cryptographic + perceptual hash;
8. add DOM mutation coverage and a first-paint shield for visual paths not represented by simple image requests.

Do not implement Machine model logic in the browser project.
