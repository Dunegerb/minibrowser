# Citra 0.1 Native architecture

```text
Trusted Citra UI
      |
      v
CitraCore -------------------- MiniMachineBridge
   |                                  |
   | visual preview + context         | Named Pipe
   |                                  v
   |                           MiniMachineCore
   |
   +--> NetworkBroker --> HTTP/HTTPS
   |
   +--> Citra HTML Renderer
   |       (NO networking)
   |
   +--> image bytes
          |
          v
   citra-image-decoder.exe
          |
          | bounded RGBA display + preview
          v
      Visual Gate
          |
      ALLOW only
          |
          v
         UI
```

## Security properties already structural in 0.1

1. The HTML renderer has no network client dependency.
2. The UI does not directly fetch web resources.
3. `file:`, custom schemes and URL-embedded credentials are denied by NetworkPolicyBroker.
4. Page and image response sizes are capped.
5. Redirects are manually followed and policy-checked at every hop.
6. Browser state is RAM-only in 0.1; there is no history DB, cookie store or HTTP disk cache.
7. Image decoders are outside `Citra.exe`. A decoder panic kills the decoder worker, not the main browser.
8. Image dimensions and decoder allocation are bounded.
9. The UI never receives image pixels before MiniMachine returns ALLOW, except when the user explicitly enables DEV ALLOW.
10. MiniMachine web content never receives access to the named pipe: only CitraCore owns the bridge.

## Important non-guarantees

Crate separation is architecture, not an OS sandbox. `citra-network` currently uses Reqwest/Rustls and the OS networking stack. The image decoder is a separate process but does not yet have a Windows AppContainer/job-token sandbox. DNS is still conventional. These are explicit future boundaries, not hidden claims.
