# MiniBrowser V0.4 — compile-fix pass

This revision addresses the C# compiler failures observed in GitHub Actions with CefSharp 151.3.240.

Fixes:

- aliases `CefSharp.Cef` as `CefRuntime` so it cannot collide with the project namespace `MiniBrowser.Cef`
- uses WPF `AddressChanged`/`TitleChanged` dependency-property event values via `DependencyPropertyChangedEventArgs.NewValue`
- adds explicit `System.IO` imports to local audit, flight-recorder, runtime-services, and named-pipe code
- imports `MiniBrowser.Core.Audit` in `MainWindow.xaml.cs` so `AuditWindow` resolves
- changes `frame_id` from `long` to `string`, matching current CEF/CefSharp frame identifiers
- keeps the V0.3 `win-x64` self-contained restore/publish workflow and .NET 8 SDK pin

The browser/Machine boundary remains unchanged conceptually; only the concrete frame-id representation changed to preserve the engine's native identifier.
