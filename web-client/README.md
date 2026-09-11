# Hallzee web client

Local classroom client for Chromium browsers (Google Chrome and Microsoft Edge), version 1.2.0-dev.1.
Hardware acceptance is pending; this is a development build, not a supported Chromebook/Windows release.
Mozilla Firefox supports local classroom and data features; direct terminal Bluetooth is deferred to a future local helper daemon.

From the repository root, one command installs checksum-pinned Node and locked
npm packages, builds the offline app, and starts it at http://localhost:4190:

```sh
bash scripts/web-client-macos.sh preview
```

Windows x64 PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/web-client-windows.ps1 preview
```

Use `check` instead of `preview` for type checking, lint, unit tests, production
build, and Chromium tests. It also installs the pinned Playwright browser.
Use `dev` for hot reload at http://localhost:5173 (offline worker disabled).
Download/extract the repository ZIP first if Git is not installed. No .NET,
Arduino, cloud account or terminal is needed for browser software checks.
The separate C# conformance job requires .NET, installed by the main contributor
bootstrap or CI. See [contributor guide](../docs/testing-and-installation.md).

`src/app` owns one runtime under a Web Lock; `transport` is the sole browser
Bluetooth boundary; `protocol` implements existing firmware v2; `storage` uses
IndexedDB transactions and nonextractable CryptoKeys; `sync` acknowledges only
durable records and advances the cursor only after a complete stream. `domain`
contains roster, reports and policy logic; `ui` renders real local state; `pwa`
controls offline installation and explicit updates. No production imports from
`preview-site`, test fixtures, cloud services or simulated providers are allowed.

`npm run build` emits `dist/`, including headers, build/source metadata, manifest,
worker, AGPL license and dependency notices. Serve the whole directory at the
root of a dedicated stable HTTPS origin; apply `_headers` or its equivalent.
Do not deploy it until the platform gates are reviewed. Local preview applies
the same security headers over localhost. CI produces a ZIP without deploying.

Tests use fictional records. Browser tests simulate only GATT while exercising
real browser storage, crypto, worker updates, downloads and locks. Their ports
4187/4188 must be free. They never connect to real Bluetooth devices.

[Teacher guide](../docs/chromebook-guide.md) ·
[IT guide](../docs/web-client-it-guide.md) ·
[Acceptance evidence](../docs/testing/web-client-acceptance.md) ·
[Design contract](../docs/design/chromebook-web-client.md)
