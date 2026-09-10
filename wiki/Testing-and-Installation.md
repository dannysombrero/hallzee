# Installation and testing

This is the contributor setup guide. Teachers should use
[Getting started](Getting-Started-Users); maintainers publishing downloads
should use [Build, test, and publish](Releasing).

## First day: start from nothing

On [GitHub](https://github.com/dannysombrero/hallzee), choose **Code → Download
ZIP** and extract it. Git is not needed. Open Terminal on Mac or PowerShell on
Windows in the extracted folder containing `README.md`, `bathroom-signin.ino`
and `scripts/`. The folder name and location do not matter.

For desktop packages or automated checks without installing a local toolchain,
use GitHub Actions: **Build Desktop Release Packages** produces Windows x64 and
Mac Apple Silicon/Intel downloads with their supporting files. Actions installs
its own prerequisites. Build artifacts are for testing; **Publish Tested
Release** publishes the tested files in this same repository.

## Assemble or flash a terminal

Use an original ESP32 DevKit V1/WROOM-32 with at least 4 MB flash, a 3×4 keypad,
and either an ST7735 or ILI9341 SPI display. C3/S2/S3/H2 boards are not supported.
Use a USB **data** cable and 3.3 V signal wiring.

### ESP32 board requirements

See the [complete board requirements](Testing-Reference#esp32-board-requirements).

### Physical wiring

Follow the [pin table and bring-up checklist](Testing-Reference#physical-wiring)
before applying power. Connect one terminal, then run your platform's command:

```sh
bash scripts/flash-terminal-macos.sh
```

```powershell
powershell -ExecutionPolicy Bypass -File scripts/flash-terminal-windows.ps1
```

These scripts install Arduino CLI, pinned ESP32 board support and libraries,
and the .NET tool used to back up/migrate the terminal. No Arduino or .NET
installation is required beforehand. Keep the private terminal backup.

For an ILI9341, add `--display ili9341` on Mac or `-Display ili9341` on Windows.
For an inverted mount, also add `--rotation 3` / `-Rotation 3`. If multiple USB
ports are present, pass `/dev/cu.usbserial-XXXX` as the final Mac argument or
`-Port COM5` on Windows, using the actual terminal's port.

To install the Arduino tools and compile without changing hardware, add
`--compile-only` / `-CompileOnly`; no terminal is needed. After a successful
normal installation, `--fast` / `-Fast` reuses tools and writes only the
application and boot selection. It makes no new backup. Read
[firmware updates and recovery](Firmware-Updates) before using fast mode.

### Restore the standard UI after touch testing

Run the regular flasher with the display and rotation options above and omit
`--touch-test` / `-TouchTest`. Normal operation uses the physical keypad. See
the [paused touch experiment](Touch-Test) for its separate wiring and limits.

## Desktop development

Windows has a one-command build that installs its own .NET SDK:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-windows-client.ps1
```

Run `HallzeeSync.Universal.exe` from `artifacts\BathroomSync-Windows`.
Downloaded Windows release ZIPs likewise extract directly to the executable
and supporting files; extract the whole archive before launching.

For local tests or Mac UI development, first install the
[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) for your computer's
architecture. Run commands from this checkout: `global.json` selects .NET 8
even when newer SDKs are installed. To run the UI:

```sh
dotnet run --project receiver/universal/BathroomSync.Universal.csproj
```

Physical Mac Bluetooth also needs the native helper. The downloadable Mac app
already includes it. For development, install full Xcode (with the macOS 15
SDK or newer) from Apple, open it once to finish setup, and select it in
**Xcode → Settings → Locations → Command Line Tools**. Then install the workload:

```sh
sudo "$(command -v dotnet)" workload install macos
```

Build the Debug helper before running the UI; substitute `osx-x64` on Intel or
when deliberately using an x64 .NET process under Rosetta:

```sh
dotnet build receiver/MacBLEAgent/BathroomSync.MacBLEAgent.csproj -r osx-arm64 -p:EnableCodeSigning=false && dotnet run --project receiver/universal/BathroomSync.Universal.csproj
```

The development client selects the helper matching its process architecture.
Use the [release packaging workflow](Releasing) for distribution; it builds,
signs and checks the complete Mac bundle.

## Website and browser prototype

Install [Node.js 22 LTS](https://nodejs.org/) (22.13 or newer) using its installer.
Then, from the repository root:

```sh
npm --prefix preview-site ci && npm --prefix preview-site run dev
```

Open `http://localhost:3000` for the website or `/preview` for the simulated
desktop prototype. Leave the command running; press Ctrl+C to stop it. Later,
`npm --prefix preview-site run dev` starts it again. `npm start` runs a previously
built production server; it does not install dependencies. The browser prototype
does not verify native Bluetooth or packaged desktop behavior.

## Choose the relevant checks

After installing the .NET SDK, these tests need no terminal or Windows PC:

```sh
dotnet test receiver/windows/BathroomSync.Tests/BathroomSync.Tests.csproj -c Release
dotnet test receiver/universal.tests/BathroomSync.Universal.Tests.csproj -c Release
```

For native firmware unit tests, install Apple's command-line tools once with
`xcode-select --install`, finish its installer, then run `make -C test coverage`.
The firmware compile-only command above installs the Arduino dependencies.
Website checks after `npm ci` are `npm --prefix preview-site test` and
`npm --prefix preview-site run lint`.

| Change | Mac sufficient? | Windows-specific verification |
| --- | --- | --- |
| Website, simulated prototype, shared UI/storage/protocol | Yes | No Windows PC required for shared tests |
| ESP32 display/keypad, firmware build, Mac BLE/USB | Yes, with a physical terminal for hardware behavior | Does not verify Windows BLE/USB |
| Windows package and database | No | Launch extracted app; load native SQLite; open/upgrade existing classroom data |
| Windows BLE and USB updates | No | WinRT pairing, reconnect, transfer/MTU behavior, COM selection, PowerShell and Windows esptool |

Windows builds and test runners can verify compilation and database/file-lock
tests. They do not replace a Windows PC with a physical terminal. Windows
launch/BLE/USB behavior has not been verified by Mac-only checks. Physical Mac
updates and clean-machine installation also need release acceptance testing;
use [release readiness](Release-Readiness) and the detailed
[Bluetooth checklist](Testing-Reference#windows--mac-bluetooth-verification).

## Pull requests, security and maintenance

**PR readiness** runs on every PR and combines desktop, firmware, website and
secret-history checks. Require **PR readiness** and **Repository hygiene** on
`main` after both have run successfully. The manual **Prepare preview dependency
update** workflow produces a reviewable patch artifact; it does not push changes.
See [CI and security](CI-and-Security) for exact workflow and branch settings.

NuGet audits include transitive dependencies; known advisories fail CI.
Dependabot monitors NuGet, npm and Actions. Use the focused
[desktop](Dependency-Updates-Desktop), [website](Dependency-Updates-Web)
and [release licensing](Release-Licensing) maintenance notes when updating.

Keep private keys, terminal backups, real classroom data and local build output
out of Git. Follow [CONTRIBUTING](https://github.com/dannysombrero/hallzee/blob/main/CONTRIBUTING.md) and report vulnerabilities
through [SECURITY](https://github.com/dannysombrero/hallzee/blob/main/SECURITY.md). Update the relevant `docs/` and matching
`wiki/` page with setup or behavior changes. Older detailed regression notes
are preserved in [Testing reference](Testing-Reference).
