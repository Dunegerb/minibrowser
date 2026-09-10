# MiniBrowser V0.6.1 — CefSharp 151 HwndHost compile fix

This patch fixes the five compile errors exposed by the V0.6 GitHub Actions smoke test without reverting the V0.6 architecture.

## Fixed

- Replaced `ChromiumWebBrowser.AddressChanged` and the incompatible WPF `TitleChanged` event usage with a dedicated `MiniDisplayHandler`, using CefSharp's `IDisplayHandler` callbacks.
- Removed `RequestContextSettings.PersistUserPreferences`, which is absent from the pinned CefSharp 151 API.
- Changed HTTP cache clearing to `ClearHttpCache(null!)`, matching the pinned API's required completion-callback parameter; CEF allows a null callback.
- Bumped the app/workflow artifact version to `0.6.1`.

## Preserved

- HwndHost/native renderer.
- Anonymous RAM-only capsule and Personal persistent capsule.
- Per-capsule `RequestContext` isolation.
- CapabilityBroker and NetworkPolicyBroker.
- MachineProtocol v2 with `capsule_id`.
- Network audit and session-only history.
