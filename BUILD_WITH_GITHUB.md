# Build Citra 0.1 Native with GitHub Actions

Commit this directory to the repository root and push to `main`.

Then open:

```text
GitHub → Actions → Build Citra 0.1 Native Windows x64
```

After a green build, download the artifact:

```text
Citra-0.1-Native-win-x64
```

Keep these two executables together:

```text
Citra.exe
citra-image-decoder.exe
```

`Citra.exe` intentionally has no .NET runtime and no Chromium bundle.
