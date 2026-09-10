# Build V0.6.1 with GitHub Actions

Push the repository to `main` or run the workflow manually from GitHub → Actions → **Build MiniBrowser v0.6.1 Windows x64**.

Artifacts:

```text
MiniBrowser-v0.6.1-portable-x64
MiniBrowser-v0.6.1-thin-x64
```

Use Portable first. It includes the .NET runtime and should run on Windows x64 without installing the SDK.

The Thin artifact omits the .NET runtime and requires the .NET 8 Desktop Runtime.

The Chromium/CEF files remain the largest part of both packages. This is expected; V0.6.1 changes the trust/storage architecture, not the engine payload size.
