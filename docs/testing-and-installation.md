# Installation and testing

This is the contributor setup guide. Teachers should use
[Getting started](getting-started-users.md); maintainers publishing downloads
should use [Build, test, and publish](releasing.md).

## First day: start from nothing

On [GitHub](https://github.com/dannysombrero/hallzee), choose **Code → Download
ZIP** and extract it. Git is not needed. Open Terminal on Mac or PowerShell on
Windows in the extracted folder containing `README.md`, `firmware/`,
and `scripts/`. The folder name and location do not matter.

For desktop packages or automated checks without installing a local toolchain,
use GitHub Actions: **Build Desktop Release Packages** produces Windows x64 and
Mac Apple Silicon/Intel downloads with their supporting files. Actions installs
its own prerequisites. Build artifacts are for testing; **Publish Tested
Release** publishes the tested files in this same repository.

The artifact names are `Hallzee-Windows-win-x64`,
`Hallzee-Mac-osx-arm64`, and `Hallzee-Mac-osx-x64`. Packaged copies of the
project license and copyright notice use the Windows-friendly filenames
`LICENSE.txt` and `COPYRIGHT.txt`.

## Assemble or flash a terminal

Use an original ESP32 DevKit V1/WROOM-32 with at least 4 MB flash, a 3×4 keypad,
and either an ST7735 or ILI9341 SPI display. C3/S2/S3/H2 boards are not supported.

For component recommendations, shopping links, the 2.8″ enclosure, and a
beginner-friendly wiring walkthrough, see [Build Your Own Terminal](build-your-own-terminal.md).
Use a USB **data** cable and 3.3 V signal wiring.

### ESP32 board requirements

See the [complete board requirements](testing-reference.md#esp32-board-requirements).

### Physical wiring

Follow the [pin table and bring-up checklist](testing-reference.md#physical-wiring)
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
[firmware updates and recovery](firmware-updates.md) before using fast mode.

### Restore the standard UI after touch testing

Run the regular flasher with the display and rotation options above and omit
`--touch-test` / `-TouchTest`. Normal operation uses the physical keypad. See
the [paused touch experiment](touch-test.md) for its separate wiring and limits.

## Desktop development

Windows has a one-command build that installs its own .NET SDK:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-windows-client.ps1
```

Run `HallzeeSync.Universal.exe` from `artifacts\BathroomSync-Windows`.
Downloaded Windows release ZIPs likewise extract directly to the executable
and supporting files; extract the whole archive before launching.

Installed classroom data is outside the application folder, under the current
user's local application-data directory in `Hallzee/universal/hallzee-trips.db`.
An upgrade must continue opening that path so rosters, trips, policies, bell
times, profiles, and terminal metadata survive replacing the application. The
workspace JSON format deliberately excludes rosters and trip history.

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
Use the [release packaging workflow](releasing.md) for distribution; it builds,
signs and checks the complete Mac bundle. The package rewrites its Avalonia
native-library link to the copy inside the app bundle, so a clean Mac does not
need any library installed under `/usr/local/lib`.

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

For firmware/USB packaging failures, use the
[release recovery steps](releasing.md#recover-a-failed-firmwareusb-build).
Starting the firmware build in Actions runs the lockfile/packaging regressions
and installs its own tools; no local Python or Arduino setup is needed.

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
use [release readiness](release-readiness.md) and the detailed
[Bluetooth checklist](testing-reference.md#windows--mac-bluetooth-verification),
including the [hardware suffix and discovery badge checklist](testing-reference.md#windows--mac-bluetooth-verification).

Terminal notification and disconnect callbacks are marshalled to the desktop
UI thread before they update the dashboard. A Mac is sufficient for the shared
view-model tests, layout inspection, and native firmware unit tests; a Windows PC is required to verify WinRT Bluetooth callback
delivery during a real connected session and to verify refreshed names and status badges over WinRT. Windows behavior has not yet been
verified by this Mac-only check.

## Pull requests, security and maintenance

**PR readiness** runs on every PR. Its portable Linux checks always run, while
website, Windows, macOS packaging, and firmware jobs run only when their paths
are affected. Changes to workflows or shared build/test scripts select the full
PR set. The manual desktop and firmware workflows retain the complete release
matrices. Require **PR readiness** and **Repository hygiene** on `main`. The
manual **Prepare preview dependency update** workflow produces a reviewable
patch artifact; it does not push changes. See [CI and security](ci-and-security.md)
for exact workflow and branch settings.

NuGet audits include transitive dependencies; known advisories fail CI.
Dependabot monitors NuGet, npm and Actions. Use the focused
[desktop](dependency-updates-desktop.md), [website](dependency-updates-web.md)
and [release licensing](release-licensing.md) maintenance notes when updating.

Keep private keys, terminal backups, real classroom data and local build output
out of Git. Follow [CONTRIBUTING](../CONTRIBUTING.md) and report vulnerabilities
through [SECURITY](../SECURITY.md). Update the relevant `docs/` and matching
`wiki/` page with setup or behavior changes. Older detailed regression notes
are preserved in [Testing reference](testing-reference.md).
# Demo screenshots

For visual documentation, use only the fictional roster in `docs/demo-roster.csv`. On a safe macOS demo machine, close Hallzee and run:

```bash
bash scripts/seed-demo-data-macos.sh
```

This replaces the Hallzee workspace at `~/Library/Application Support/Hallzee/universal/hallzee-trips.db` with mock student records, completed restroom trips, one active pass, and a sample bell schedule. It is destructive to that one local Hallzee workspace; never use it on a production classroom computer.
