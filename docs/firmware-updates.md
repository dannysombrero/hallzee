# Firmware updates and releases

The client implements signed firmware packages, Bluetooth installation, firmware
version reporting, and GitHub update checks. Physical OTA and USB migration
validation is still required before a production release. No release has been
published by this implementation.

## Teacher workflow

Perform the one-time USB setup with the existing root-relative flash script in
[Installation and testing](testing-and-installation.md), or use a matching
prebuilt USB bundle from a firmware release. The bootstrap enables two larger
firmware slots and migrates terminal files without erasing pairing or NVS. It
checks the physical chip/flash size, reads the old flash twice to verify its
backup, and verifies restored files. Backups are bound to the physical ESP32 MAC address. Unknown layouts or data that cannot fit
stop the operation before flashing. Keep the private backup if recovery is needed. The prebuilt bundle provides
`recover-usb-macos.sh` and `recover-usb-windows.ps1` to restore a verified backup
to the same physical device. Recovery returns both firmware and terminal files
to the backup's timestamp; sync any newer trips first if the terminal is usable.

1. Connect to the terminal and open **Settings → Device**. The client requests
   the running firmware version/build. **Refresh version** repeats that query.
   Cached information is labeled **Last seen** until a fresh query succeeds.
   Older firmware shows a USB setup notice instead of claiming update support.
2. Download a `.hallzee-fw` package from the firmware's GitHub Release, or use a
   file provided by an authorized maintainer during private testing.
3. Choose **Install firmware from file…**. The client verifies the signature,
   image hashes, versions, hardware, orientation, and layout. It selects the
   correct image automatically. Do not unzip the file.
4. Review the target terminal, version, and release notes; check in every active
   pass, keep power connected, and choose **Install update**. Stay nearby.
5. The app syncs trips, sends and verifies the image, restarts the terminal, and
   reconnects to the same unique ID. **Complete** means the terminal reported the
   expected version/build, a confirmed boot, and successful reconciliation sync.

**Cancel transfer** is available before commit. A disconnected/aborted transfer
must restart from the beginning after reconnecting; it cannot replace the
running image. After commit, allow restart and verification to finish. An
unverified result does not mean success: reconnect and refresh the version.
The previous image is retained for boot-failure rollback. Ordinary updates retain
terminal name/ID, pairing, credentials, trips, and settings.

**Check for software updates** checks versioned GitHub Releases and shows client
and terminal results separately. **Download & Install firmware** uses the same
signed-package installer without a file picker. The client needs internet for
this check/download; the terminal does not need Wi-Fi. Local package import
works offline. **Open desktop release** opens the client release page; installing
the desktop app is still a separate platform operation.

The default release repository is `dannysombrero/hallzee-mono`. If binaries move
to another public distribution repository, update the pinned repository in
`FirmwareReleases.cs` and release the client. Public downloads require no GitHub
token; private development uses downloaded files. A private repository, API
failure, or rate limit reports a failed check, not “up to date.”

## Package format and compatibility

A `.hallzee-fw` file is a bounded ZIP containing `manifest.txt`, `manifest.sig`,
`release-notes.md`, and `images/<variant>.bin`. Supported variants are
`esp32-st7735-r1` and `esp32-ili9341-r0` through `r3`. The initial USB installation
selects the physical display/orientation; future imports match that identity.

The manifest is ASCII with LF line endings and a final LF. ECDSA P-256/SHA-256
signs its exact bytes; `manifest.sig` is a DER-encoded signature. The firmware
and desktop both contain the trusted public key. Package-provided keys are never
trusted. Header fields, separated by `|`, are:

```text
HALLZEE-FW|1|version|build|minClient|bootstrap|dataSchema|notesSha256|keyId
```

One following line per image has these fields:

```text
variant|ota-v1|imageBytes|imageSha256|bootloaderSha256|partitionsSha256|bootAppSha256
```

SHA-256 values use lowercase hexadecimal. Bootstrap/data schema are currently
`1`; versions use three integers from 0–999 with no leading zeroes. Build IDs
contain 1–40 letters, digits, dots, underscores, or hyphens. Only stable versions
are installed; same-version reinstall and downgrade are blocked. A signed image
also contains an embedded version/build/variant marker checked by the package
validator. Archive size is limited to 16 MiB, manifest to 2 KiB, notes to 32 KiB,
and each image to its 1.5 MiB slot. Duplicate/unsafe entries and ambiguous images
are rejected. The USB bundle's app, bootloader, partition table, and boot-app
metadata must match the signed package before downloaded bundles can be flashed.

The header deliberately uses a bounded text format rather than the design's
illustrative JSON manifest, keeping the terminal parser small and avoiding a
new JSON dependency. CRC/checksum-only firmware is never accepted as a release.

## Build and publish

Source builds use the pinned ESP32 Arduino core 3.3.11, which supplies both
`CONFIG_BOOTLOADER_APP_ROLLBACK_ENABLE` and `CONFIG_APP_ROLLBACK_ENABLE`. The new
`ota-v1` layout uses app slots at `0x10000` and `0x190000`, each `0x180000` bytes;
LittleFS moves to `0x310000` with `0xe0000` bytes (896 KiB). NVS stays at `0x9000`.
The USB migration repacks and compares file contents instead of copying an old
filesystem image into a smaller partition. Actual retention capacity depends on
record sizes; full/oversized filesystems fail migration rather than discard data.

The source flash scripts install Arduino tools and .NET 8 when needed. Prebuilt
USB ZIPs include the runtime, esptool, and mklittlefs, so the recipient needs no
compiler, Git, .NET, or Arduino installation. Instructions are in the bundle's
README. Windows x64 and Apple-silicon Mac USB bundles are built by the release
workflow; Intel Mac users can use the source workflow.

Run **Build Firmware Release Packages** in GitHub Actions with a new version.
It tests both desktop platforms, compiles all five firmware variants, checks
128 KiB of remaining app-slot space, signs one package, and creates USB bundles.
Initially leave **publish** unchecked: download private Actions artifacts for
hardware testing. Once those tests pass, use **publish** to create the immutable
`firmware-v<version>` release. An existing tag/release is not overwritten.
The checker recognizes independent `client-v<version>` desktop releases; older
moving `latest` client releases are not presented as versioned update results.

Before running the release workflow, configure its protected `firmware-release`
environment and `HALLZEE_FIRMWARE_SIGNING_KEY` secret. Restrict signing to reviewed
release branches; do not expose this environment to untrusted pull-request code. The initial private key was
generated locally at `.tools/firmware-signing/release-key.pem` (ignored by Git).
Back it up securely; the public counterpart is `firmware/release-public-key.pem`
and is embedded in `FirmwareRelease.h` and the desktop. Do not print or commit the
private key, attach it to artifacts, or replace it after installing terminals.
The signing tool refuses a private key that does not match the pinned public key.

For a local developer release after installing the tools with the flash script:

```sh
python3 scripts/build-firmware-release.py --version 1.0.1 --build local-test --output artifacts/firmware-1.0.1 --key .tools/firmware-signing/release-key.pem --notes firmware/release-notes.md --arduino-data "$HOME/Library/Arduino15" --cli .tools/arduino-cli/bin/arduino-cli
```

This is a developer command, not a teacher prerequisite. The workflow supplies
Python and .NET automatically on its runners. The output directory must be new;
this prevents accidentally replacing an existing package. CLI verification:

```sh
dotnet run --project tools/FirmwareTool/FirmwareTool.csproj -- verify --package artifacts/firmware-1.0.1/Hallzee-Firmware-1.0.1.hallzee-fw
```

Signing-key rotation requires a coordinated bridge firmware/client release
signed by the existing trusted key, or USB setup. This implementation pins one
key; automatic multi-key rotation and cross-reboot transfer resume are not
provided. Do not enable irreversible eFuse changes as part of routine updates.
Bootloader/layout changes remain USB operations. USB migration backups contain
owner credentials and trip data and are restricted to the installing user.

## Verification status

Mac testing is sufficient for package/version/state-machine tests, bounded binary
frame tests, and simulated USB migration using the real LittleFS tool. Physical
ESP32 tests are required for both displays: power interruption during write and
commit, deliberate failed-boot rollback, owner/bond retention, data retention,
and measured BLE transfer duration. Mac testing covers CoreBluetooth only.
A **Windows PC is required** for WinRT MTU/write pacing, notification delivery,
file selection, reboot reconnect, and retained Windows credential/bond state.
Physical Windows OTA, Mac OTA, and USB migration behavior have not yet been
verified. Cross-compilation and simulated migration are not hardware validation.

See the [implementation design and acceptance matrix](design/bluetooth-firmware-updates.md)
for remaining physical release gates. No attached terminal is flashed by tests.
