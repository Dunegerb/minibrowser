# Citra Native roadmap

## 0.1 — chassis

Native UI, CitraCore, isolated network module, HTML tokenizer/render model, anonymous RAM-only capsules, separate image decoder, MiniMachine Visual Gate.

## 0.2 — document engine

Citra CSS subset, forms without JavaScript, cookies/storage broker, persistent capsule vault design, better tables and accessibility tree.

## 0.3 — scripting laboratory

Introduce a JavaScript runtime behind explicit capabilities. It receives DOM APIs and NetworkBroker handles, never ambient sockets/filesystem.

## 0.4 — process isolation

Renderer and image/media workers in restricted Windows processes/AppContainers, job objects, mitigations and IPC-only authority.

## 0.5 — modern layout

Expand CSS/layout, font shaping and compositing. Evaluate which Servo components are worth importing as libraries rather than importing a browser.

## 0.6+ — web application compatibility

Incrementally add media, WebSocket, WebAssembly and advanced APIs through brokers and capability grants.
