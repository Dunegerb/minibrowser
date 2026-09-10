# MiniBrowser V0.6.1 — Zero Browser foundation

V0.6.1 introduces identity capsules, per-capsule CEF RequestContexts, a capability broker, a network policy broker and MachineProtocol v2 while retaining the optimized HwndHost renderer from V0.5.

## Recommended: build with GitHub Actions

Push the repository to GitHub. The workflow at `.github/workflows/build-windows.yml` produces two Windows x64 artifacts:

- `MiniBrowser-v0.6.1-portable-x64` — self-contained .NET runtime;
- `MiniBrowser-v0.6.1-thin-x64` — smaller package, requires .NET 8 Desktop Runtime.

No Visual Studio or .NET SDK is required on the machine that merely runs the portable artifact.

## Local build requirements

- Windows 10/11 x64
- .NET 8 SDK, or Visual Studio with .NET desktop development
- Internet for first NuGet restore

```powershell
dotnet restore .\MiniBrowser.sln -r win-x64
dotnet build .\MiniBrowser.sln -c Debug -p:Platform=x64
dotnet run --project .\src\MiniBrowser.App\MiniBrowser.App.csproj -c Debug
```

## Capsules

### Anonymous

Uses a dedicated `RequestContext` with an empty `CachePath`. Chromium treats this as incognito/in-memory storage. Closing MiniBrowser destroys the context; MiniBrowser does not create a persistent profile path for this capsule.

### Personal

Uses:

```text
%LOCALAPPDATA%\MiniBrowser\cef-root\capsules\personal\
```

Cookies, localStorage and IndexedDB can persist. Volatile browser caches are trimmed on startup.

Tabs never share RequestContexts across different capsules.

## Local data

```text
%LOCALAPPDATA%\MiniBrowser\
  cef-root\
    global\                 CEF global context (not used as tab identity)
    capsules\personal\     Personal capsule website state
  profile\                  legacy V0.5 data, left intact except volatile-cache trimming
  audit\                    bounded network audit ring
  flight-recorder\          compact semantic Machine events
  logs\cef.log              local CEF log
```

## Security boundary in V0.6.1

`NetworkPolicyBroker` executes before resource loads and can block requests by capability/scheme. It is **not yet** an external socket broker: CEF still owns network I/O in this milestone.

Default capability policy:

```text
Network       ALLOW
Popups        ALLOW, but native popup is cancelled and re-routed to a tab
DevTools      ALLOW (development)
Filesystem    BLOCK
Downloads     BLOCK
Camera        BLOCK policy slot
Microphone    BLOCK policy slot
Geolocation   BLOCK policy slot
Notifications BLOCK policy slot
Clipboard     BLOCK policy slot
```

The device/clipboard entries are policy defaults but their CEF permission-handler enforcement is a later milestone. Filesystem/download/network scheme enforcement is wired into the current request path.

## Keyboard

- `Ctrl+L` focus omnibox
- `Ctrl+T` new tab in current capsule
- `Ctrl+Shift+N` anonymous tab
- `Ctrl+Shift+P` personal tab
- `Ctrl+W` close tab
- `Ctrl+H` session history
- `F12` DevTools when capability is enabled

## Still intentionally incomplete

V0.6.1 does not claim full Zero Browser isolation, microVMs, anonymous overlay networking, anti-fingerprinting uniformity, Visual Quarantine, encrypted vaults or an out-of-process Network Broker. See `docs/ZERO_BROWSER_ARCHITECTURE_V06.md`.
