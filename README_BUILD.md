# MiniBrowser V0.4 — build and run

This repository is the from-zero foundation for the MiniBrowser/MiniMachine architecture.

## Requirements

- Windows 10/11 x64
- Visual Studio 2022+ with **.NET desktop development**, or .NET 8 SDK
- Internet access for the first NuGet restore

The project targets `net8.0-windows` and pins `CefSharp.Wpf.NETCore` **151.3.240**.

## Build

```powershell
dotnet restore .\MiniBrowser.sln
dotnet build .\MiniBrowser.sln -c Debug -p:Platform=x64
```

Run:

```powershell
dotnet run --project .\src\MiniBrowser.App\MiniBrowser.App.csproj -c Debug
```

Or open `MiniBrowser.sln` in Visual Studio and run `MiniBrowser.App` as x64.

## Local data

Nothing in MiniBrowser code uploads diagnostics. Local state is written under:

```text
%LOCALAPPDATA%\MiniBrowser\
  profile\          Chromium cookies/storage/cache
  logs\cef.log      local CEF log
  audit\            network audit JSONL
  flight-recorder\  compact Machine events
```

Website traffic still goes to the websites and third parties they contact. “No application telemetry” is not the same as “no web tracking.”

## Keyboard

- `Ctrl+L` focus omnibox
- `Ctrl+T` new tab
- `Ctrl+W` close tab
- `F12` DevTools

## What V0.4 implements

- WPF shell + CefSharp/CEF Chromium engine
- x64, separate MiniBrowser profile
- tabs, back, forward, reload and omnibox
- URL vs search parsing; no search request is made before Enter
- `search.before_submit` Machine event
- interception of CEF resource requests via `RequestHandler`/`ResourceRequestHandler`
- local network audit with URL SHA-256 instead of plaintext URL
- explicit `session_id`, `window_id`, `tab_id`, `frame_id`
- UTC + monotonic timing
- `MachineProtocol v1` decision enum: ALLOW/BLOCK/BLUR/REPLACE/DEFER/SCAN_MORE
- `IMachineBridge` abstraction and no-op Machine implementation
- named-pipe bridge scaffold for `\\.\pipe\MiniMachineBrain`
- browser native popups cancelled and re-routed to tabs
- local 72-hour Machine flight recorder
- `[RECAÍ]` event + preservation of recent recorder files
- F12 DevTools
- conservative flags intended to reduce Chromium-owned background networking

## Deliberately not claimed as complete yet

V0.4 is the foundation, not the final protected browser. The following are architecture slots/TODOs, **not completed security guarantees**:

- pre-paint Visual Quarantine / First Paint Shield
- image response-byte capture into shared memory
- hashes/perceptual hashes and decision cache
- `srcset`, CSS backgrounds, `blob:`, `data:`, SVG and canvas/WebGL capture
- DOM Mutation Sensor, visibility/dwell-time and causal interaction graph
- video frame sampling
- form-submission interception inside websites
- permission UI for camera/microphone/geolocation/notifications
- download gate
- secure password store
- signed updater
- external packet-capture test proving the zero-browser-telemetry goal

Those belong in the next milestones without changing the browser/Machine separation established here.

## Build sem instalar .NET/Visual Studio — GitHub Actions

O repositório inclui `.github/workflows/build-windows.yml`. Ao fazer push para `main` (ou executar o workflow manualmente), o GitHub cria um build Windows x64 self-contained e publica o artefato `MiniBrowser-win-x64`.

O PC que executará o artefato não precisa ter o .NET 8 SDK/runtime instalado. Extraia o artefato e execute `MiniBrowser.exe` mantendo os arquivos CefSharp/CEF que vêm ao lado dele.

Veja `BUILD_WITH_GITHUB.md` para o passo a passo.
