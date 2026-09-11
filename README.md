# Citra 0.1 Native — Chassis

Citra 0.1 Native is the first non-Chromium chassis for Citra.

It is deliberately small in scope:

- native Rust application and UI;
- no Chromium/CEF, Blink or V8;
- no JavaScript execution;
- no persistent browser profile, history database or disk cache;
- HTTP/HTTPS networking exists only in `citra-network`;
- HTML is tokenized by `html5ever` into a small Citra-owned render model;
- images are decoded in a separate helper process with input/dimension/allocation limits;
- every decoded visual gets a bounded RGBA preview and crosses the MiniMachine bridge before the UI can receive the display pixels;
- MiniMachine offline is **fail-closed for visuals by default**;
- an explicit `DEV ALLOW VISUALS` switch exists only so we can develop before MiniMachine is ready.

This is a chassis, not a modern-Web-compatible browser yet.

## What works in 0.1

- multiple anonymous tabs;
- URL navigation;
- text searches through DuckDuckGo's server-rendered HTML endpoint;
- Back / Forward / Home / Reload;
- HTML headings, paragraphs, links, lists, preformatted text, rules and images;
- HTTP redirects with policy checks;
- JPEG / PNG / GIF / WebP still-image decoding;
- MiniMachine `visual.resource` protocol over `\\.\pipe\MiniMachineBrain`;
- image placeholders when MiniMachine is offline or denies a visual.

## What intentionally does not exist yet

- JavaScript;
- external CSS layout;
- cookies/login state;
- forms;
- downloads;
- audio/video;
- WebSockets;
- WebAssembly;
- service workers;
- extensions;
- persistent capsules;
- anonymous network overlay;
- renderer/process sandboxing beyond the separate image decoder process.

Those features must be added through Citra-owned brokers instead of giving the renderer ambient authority.

## Build locally

Requires Rust 1.98.1+ on Windows x64:

```powershell
cargo build --release --workspace
```

Run `target\release\Citra.exe`. Keep `citra-image-decoder.exe` next to it.

## GitHub Actions

Push this source tree to GitHub. The workflow creates `Citra-0.1-Native-win-x64` containing both executables and SHA-256 hashes.
