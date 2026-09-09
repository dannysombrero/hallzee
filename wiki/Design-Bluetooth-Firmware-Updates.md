# Plan: Bluetooth firmware packages and updates

**Status:** Implemented in source; physical OTA/rollback and USB migration
verification remain release gates. No production firmware release has been
published. See [Firmware operations and releases](Firmware-Updates.md) for the
actual wire/package formats, signing setup, commands, and supported platforms.

**Implementation decisions:** `manifest.txt` uses bounded ASCII pipe-separated
fields, signed with ECDSA P-256/SHA-256. One bundle covers five display/orientation
variants and signs USB bootstrap hashes as well as application hashes. `ota-v1`
has two 1.5 MiB slots and 896 KiB LittleFS; USB scripts verify backup/repacking
before flashing. The installed Arduino 3.3.11 core enables rollback; application
confirmation follows storage/BLE initialization under a 15-second watchdog.
Binary frames share the encrypted RX stream with explicit framing and do not
enter the newline parser. WinRT/CoreBluetooth negotiate payload size with a
20-byte fallback. Signing-key rotation remains a coordinated bridge/USB
procedure; automatic multi-key rotation is not implemented.

**Decision:** Deliver manual package installation first, then GitHub update
checks using the same installer. A one-time USB installation to enable OTA is
accepted. Routine updates should leave the terminal mounted and powered normally.

## 1. Teacher experience

### First release: download, choose file, update

1. In **Settings → Device**, show **Firmware version**, the connected terminal's
   friendly name and unique ID, and whether Bluetooth updates are supported.
   Read version information from the authenticated terminal, never infer it from
   the desktop version or a previously selected file. Offline values must say
   **Last seen**; older firmware that cannot report a version says **Unknown —
   USB setup required for wireless updates**.
2. Download one `.hallzee-fw` file from the firmware's GitHub Release. During
   private development, authorized maintainers can download and distribute the
   same package directly; importing a local file requires no GitHub login or
   internet connection. If source remains private when binaries become public,
   a public distribution repository can host the release assets.
3. Choose **Install firmware from file…** in Device settings. The client opens a
   native file picker and checks the package against the connected terminal.
   Teachers do not unzip files, choose flash offsets, compile code, or guess
   which display binary to install.
4. Show terminal name/ID, installed and target versions, release notes, and a
   short notice to keep the terminal powered and the computer nearby. Offer
   **Install update** or **Cancel**. A valid but incompatible package explains
   the required client/USB upgrade instead of starting a transfer.
5. After all passes are checked in, sync outstanding trips and enter update
   mode. Show **Sending → Verifying → Restarting → Reconnecting → Complete**,
   with byte progress during transfer. The terminal shows an update screen and
   temporarily pauses new checkouts.
6. Reconnect to the same unique ID, authenticate with the existing owner, query
   the running version/build, and refresh time and settings. Show success only
   when the terminal reports the intended build and a confirmed healthy boot.
   If it cannot reconnect, say **Update result not yet verified** and offer
   reconnect; do not call an acknowledged download a completed installation.

Names, unique IDs, ownership, BLE pairing, trips, and device settings must remain
intact across an ordinary update. Workspace/roster data stays on the computer.

### Later release: Check for software updates

Keep **Install firmware from file…** permanently available. Add **Check for
software updates**, showing separate results for **Desktop client** and
**Terminal firmware** so their independent versions are clear. For firmware,
show a compatible release and **Download & Install**; after the teacher chooses
it, download, validate, and run the same installation flow without a manual
browser download or file picker. No unattended reboot during class.

Checking requires internet only on the desktop. If the terminal is disconnected,
client updates may still be checked, but firmware eligibility requires reconnecting.
Network failure must not disrupt ordinary offline terminal operation. Desktop
application self-installation is a separate delivery concern; initially its
update result may link to the appropriate platform installer.

## 2. Package and release contract

Use a ZIP container with a custom `.hallzee-fw` extension. Prefer one release
bundle containing all supported terminal variants, with automatic image
selection by authenticated hardware information. The desktop streams only the
matching image to the device. Illustrative contents:

```text
Hallzee-Firmware-<version>.hallzee-fw
  manifest.txt
  manifest.sig
  release-notes.md
  images/esp32-st7735-<variant>.bin
  images/esp32-ili9341-<variant>.bin
```

The signature covers exact manifest bytes; clients do not normalize them. The
signature binds each image's digest and compatibility fields. The selected signature is ECDSA P-256/SHA-256 with DER encoding; the
wire manifest format is frozen as schema 1. Both desktop and terminal must verify
against trusted public keys installed independently of the downloaded package;
never trust a signing key supplied only by that package. Keep private signing
keys in protected release infrastructure, outside binaries and the source tree.

| Manifest field group | Required information |
| --- | --- |
| Format | Package schema version, firmware version, release channel, build/commit ID, signing key ID |
| Hardware | Board/chip family and supported revision, display profile, orientation/build variant, minimum flash size |
| Compatibility | Partition-layout ID, minimum OTA bootstrap version, OTA protocol version, minimum desktop version, supported persistent-data schema range |
| Payloads | Image path, exact byte count and SHA-256 digest for each supported variant; digest for release notes |

Use firmware Semantic Versioning independently of desktop versions. Embed
version, build ID, and hardware/layout identifiers in each image, and check
that release metadata matches them. Never infer hardware from a friendly name.
Reject ambiguous image matches. An equal version/build is **Already installed**;
block routine downgrades and reserve signed, explicitly compatible recovery
releases for a separate support procedure. Boot-failure rollback must still work.

Validate bounded manifest/archive sizes, duplicate entries, paths, decompressed
sizes, signatures, payload hashes, and image headers before transfer. Do not
execute content or extract arbitrary archive paths. The terminal independently
checks the signed metadata, hardware/layout, size, and received image digest
before making a new image bootable. A checksum alone is not publisher authentication.

Publish versioned, immutable assets under distinct firmware tags, for example
`firmware-v1.0.0`, alongside client releases or in the distribution repository.
Provide release notes, required desktop/bootstrap versions, and a separately
labeled USB setup/recovery download. CI must build every supported display and
orientation, validate package metadata/signatures, and enforce flash-size limits
before publication. Never silently replace the bytes of a published version.

The later checker should select firmware releases by product, channel, and
compatibility. Do not blindly use a repository's generic latest release when
client and firmware tags share it. Use a documented release catalog or filtered
release listing, skip drafts/prereleases by default, compare semantic versions,
and handle API errors/rate limits without labeling an unsuccessful check as
**Up to date**. GitHub exposes release metadata and asset download URLs through
its [Releases API](https://docs.github.com/en/rest/releases/releases). Public
release consumption must not require a teacher token or an embedded shared token.

## 3. Firmware and Bluetooth implementation

Add an authenticated firmware-info query without changing the existing v2
identity/claim response shape. Proposed `GET_FIRMWARE_INFO` returns a bounded,
versioned response with running version/build, hardware/display/orientation,
layout ID, OTA capabilities, available slot size, and boot-validation status.
Specify exact framing and errors in the Bluetooth protocol document during
implementation. Unsupported queries on old firmware must degrade cleanly.

Implement an update state machine owned by the existing authenticated session:
**Idle → Receiving → Verifying → Ready to reboot → Boot validation → Complete**,
with explicit abort, failure, rollback, and unverified-reconnect outcomes.

- Allow only the paired owner. Recheck that no pass is active on the terminal
  when entering update mode, not only in the desktop UI. Suspend new checkouts,
  owner reset/unpair, and competing settings/sync commands during installation.
- Acquire the desktop operation coordinator for the complete operation. Treat
  the planned reboot separately from accidental connection loss so normal sync
  and reconnect timers do not compete with update recovery.
- Use a bounded binary transfer characteristic or equivalent framed stream;
  do not push megabytes through the current newline command parser. Negotiate
  supported chunk size, retain a default-MTU fallback, and use flow control,
  offsets/sequence numbers, acknowledgements, and bounded retries/timeouts.
- Bind packets to an update/session ID and the chosen image. Define duplicate,
  missing, reordered, oversized, and wrong-session packet behavior. Bound MCU
  buffering and stream to the inactive app slot without staging a second image
  in LittleFS. Preserve the existing trip-sync service for normal operation.
- For v1, retry after a disconnected/aborted transfer starts from zero following
  fresh authentication. Do not promise cross-reboot transfer resume initially.
  Reject replayed chunks from the old session. Cancellation before commit leaves
  the running firmware selected and restores normal service; after commit, finish
  verification/reconnect rather than offering an unsafe interruption.
- Keep progress responsive on Windows and macOS. Measure actual transfer times
  and memory use before promising a duration; BLE throughput is platform dependent.

Espressif provides a [BLE OTA reference component](https://components.espressif.com/components/espressif/ble_ota/versions/0.1.17/readme?language=en).
Evaluate reuse against the existing Arduino 3.3.11/Bluedroid transport and size
budget; it is a reference, not an assumed drop-in dependency.

## 4. Flash layout, bootstrap, and recovery

The current configured 4 MB layout already contains two 1.25 MiB application
slots plus OTA metadata. The latest ILI9341 build measured 1,306,473 bytes against
1,310,720 bytes per slot: only 4,247 bytes remain. Adding an updater and signature
verification needs a size/layout decision before deploying the bootstrap.

The implemented updater/signature code exceeded the original slot size, so
`ota-v1` uses two 1.5 MiB app slots and the USB migration described below.
Physical retention-capacity measurement on 4 MB boards remains required. Set a CI headroom target (initial proposal: at least
128 KiB per slot after signing overhead); confirm feasibility in the spike.
Do not solve the problem by removing the recovery slot or assuming 8 MB hardware.

The one-time USB workflow must install the agreed layout, an explicitly
rollback-enabled bootloader, firmware containing the updater, and trusted update
verification keys. Verify the actual bootloader configuration: rollback is not
made available just by adding an application-level update command. ESP32's
[supported OTA process](https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/system/ota.html)
writes the inactive slot and can roll back an unconfirmed new application when
configured. Partition placement must follow the
[ESP32 partition rules](https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-guides/partition-tables.html).

Extend the existing Mac/Windows bootstrap scripts so a clean computer can perform
initial setup in one or two root-relative commands, installing prerequisites
automatically. Prefer prebuilt, verified USB bundles so teachers need no compiler.
Check actual chip/flash size and selected display/orientation before flashing.
For existing terminals, back up and verify persistent data before any partition
migration. If offsets move, the script must restore compatible data into the new
layout and verify it; a normal app upload alone is not a migration. Stop on failed
backup/restore checks instead of silently formatting. Keep device backups local
with restricted access; never put owner credentials or trip data into release
packages. New terminals use the same
workflow without old data. Retain USB recovery and document its exceptional use.

Routine BLE packages update application firmware only, not the bootloader,
partition table, or filesystem images. Layout/bootstrap changes require USB and
must be reported as such before transfer. The initial boot health check should
verify storage, BLE/service startup, and essential initialization with a bounded
watchdog, then mark the app valid. Do not depend on internet, a teacher being
present, a populated roster, or the clock already being set. Keep data migrations
backward compatible with the rollback image; never perform destructive migration
before a new image is confirmed. Report rollback separately from success.

Design signing-key rotation before shipment: overlapping trusted keys and a
signed transition release must preserve a USB recovery path. Do not make
irreversible eFuse/security changes an incidental part of ordinary updates.

## 5. Delivery milestones and acceptance gates

| Phase | Deliverables | Exit gate |
| --- | --- | --- |
| 0 — Feasibility and format | Measure OTA/signature code size, finalize layout/data migration, choose signing format and BLE framing, prototype transfer and rollback on both displays | Both images fit with agreed headroom; physical interrupted-transfer and failed-boot recovery demonstrated |
| 1 — Version and USB foundation | Embedded version/build/hardware info, authenticated query and Device UI, automatic USB setup/recovery scripts | Connected UI reports the actual build; old firmware gives a clear USB requirement; persistence survives bootstrap migration |
| 2 — Manual package installation | Package builder/validator, protected signing workflow, file picker and review UI, BLE updater, reconnect verification | A local package installs fully offline with no unpairing, manual binary selection, or data loss; failure cases below pass |
| 3 — GitHub distribution | Versioned firmware/USB assets and release notes alongside client downloads; documented private-to-public distribution | Teacher downloads one firmware file and installs through Phase 2; no GitHub token is embedded or required for public downloads |
| 4 — Online update checks | Check for software updates, compatible release selection, Download & Install, separate desktop-client result | Downloaded packages use the same validation/recovery path; offline use and manual import remain supported |

Private CI artifacts can supply Phase 2 tests before public releases exist.
Phases 0–3 are the first usable delivery; Phase 4 is a later convenience feature.
The release repository coordinates and exact layout/signature choices are
implementation decisions to record before publishing the bootstrap, not teacher
configuration tasks.

## 6. Verification before release

Mac testing is sufficient for shared package/version/UI/state-machine tests and
native firmware tests. A physical ESP32 is required for flash integrity,
interruption recovery, retained data, and rollback checks on both ST7735 and
ILI9341 configurations. A Mac can test the CoreBluetooth path. **A Windows PC is
required** for the Windows-specific WinRT binary GATT writes, negotiated payloads,
flow control, file picker, reboot reconnect, and retained Bluetooth/credential
state. Both platform paths are implemented, but neither physical BLE OTA path has
been verified; Windows OTA behavior has not been verified. Mac-only testing cannot release it.

Required scenarios:

- Version comes from the connected unique ID, remains accurate after rename, and
  is visibly stale offline; legacy firmware cannot falsely claim OTA support.
- Correct package installs and survives power cycles with name, ID, owner,
  pairing, trips, and settings retained; reconnect confirms exact build ID.
- Wrong board/display/orientation/layout, old client, unsupported schema, invalid
  signature/hash, truncated archive, oversized image, ambiguous match, and
  unauthorized client all fail without changing the boot target.
- A pass becomes active between client preflight and firmware update start;
  terminal refuses entry. During transfer, new checkout and competing device
  operations are blocked and restored after abort/failure.
- Bluetooth loss, app closure, duplicate/missing chunks, and power removal during
  reception/verification/commit recover predictably; retries cannot corrupt the
  running slot. A deliberately failing new build rolls back without data loss.
- Successful boot with an absent desktop remains usable; a failed reconnect
  reports unverified outcome until querying the terminal resolves it.
- Test minimum supported flash and both display/orientation variants; record
  end-to-end transfer time and peak memory on each desktop platform.
- Public/private distribution, offline file import, unavailable GitHub, rate
  limits, stable/prerelease filtering, and independent client/firmware versions
  behave as documented without leaking credentials or student information.

Update the protocol, installation/user guides, release instructions, and matching
Wiki pages with each implemented phase. Keep planned UI clearly distinguished
from shipped behavior until its acceptance gate passes.
