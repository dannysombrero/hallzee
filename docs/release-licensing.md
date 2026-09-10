# Release licenses and source

Hallzee Labs owns original Hallzee code, licensed AGPL-3.0-or-later. The CAD/model
files under `models/` use CC BY-SA 4.0. Dependencies retain their own notices;
these grants do not assign third-party copyrights to Hallzee Labs.

## What a download contains

Desktop and USB downloads include `LICENSE`, `COPYRIGHT`,
`THIRD-PARTY-NOTICES.md`, font notices, and licenses collected from their exact
restored NuGet graph. `licenses/dependency-inventory.json` records the target
runtime, package versions and repository commits where upstream supplies them.
Windows and USB inventories also record published-file SHA-256 hashes. Mac
inventories live in `Hallzee.app/Contents/Resources/licenses`; code signing changes
Mach-O files after collection, so Mac binary integrity uses the final ZIP checksum.
The inventory describes
resolved build dependencies, including build-only packages, rather than claiming
that each package is loaded at runtime. The checked-in inventory is a reference;
release builds regenerate it after publishing the selected project and RID.

Firmware releases provide source archives alongside the firmware and USB ZIPs.
`licenses/firmware-source-manifest.json` identifies each archive, commit/version,
checksum, recursive Git submodules, and the SDK archives selected by each
variant's linker map. `licenses/usb-source-manifest.json` covers esptool and
mklittlefs source. Each USB platform also provides a
`Hallzee-USB-Python-Sources-<rid>.zip` containing exact sources and notices for
our rebuilt esptool executable and its resolved Python environment. Preserve
these companions when redistributing a release.
The GitHub source archive at the release tag provides Hallzee's matching source,
firmware partition table, build scripts, and installation instructions.

The release builder validates the exact Arduino library versions in
`firmware/terminal/arduino-libraries.txt`, preserves their complete installed source and
copyright headers, and collects upstream license files. Installation disables
automatic dependency resolution because every declared dependency is pinned in
that file. It retains the installed ESP32 SDK's `versions.txt` and `sdkconfig`.
Unexpected library versions, unavailable source, or a newly linked ESP-IDF
managed component without recorded source stop bundling.

## Firmware source and license decisions

- **Arduino-ESP32 3.3.11:** include the source at its exact release commit and
  its [LGPL 2.1 license](https://github.com/espressif/arduino-esp32/blob/189089bb76e74978dc95abebefdad42b0d421ba9/LICENSE.md).
  The compiled SDK identifies ESP-IDF 5.5.5 at `b774170ff46`, separately from
  the Arduino core. The source archive includes IDF's recursive submodules,
  their licenses, and Espressif's supplied binary radio components. These
  components have their own terms; the entire SDK is not simply Apache-2.0.
  See [Espressif's component copyright list](https://docs.espressif.com/projects/esp-idf/en/v5.5.5/esp32/COPYRIGHT.html).
- **Managed SDK components:** the current linker maps use LittleFS 1.22.2 and
  ESP Diagnostics 1.3.3. Their exact upstream sources, submodules, and notices
  accompany the release. Other installed SDK components are recorded in
  `versions.txt`; availability in the SDK does not mean the linker included them.
- **Keypad 3.1.1:** its `src/Keypad.h`, `src/Keypad.cpp`, `src/Key.h`, and
  `src/Key.cpp` grant LGPL 2.1; its root `LICENSE` instead contains GPL 3.
  Both the complete unmodified source (including those headers) and upstream
  GPL license file are retained. The Arduino source bundle supplies the LGPL
  2.1 text too. Do not describe this as a permissive library or pretend the
  upstream inconsistency has disappeared. Our distribution supplies source
  and permits rebuilding/relinking under both sets of copyleft conditions;
  original Hallzee code remains AGPL-3.0-or-later. [Upstream Keypad source](https://github.com/Chris--A/Keypad)
  and the [GPL/AGPL combination clause](https://www.gnu.org/licenses/gpl-3.0.html#section13)
  describe the underlying grants.
- **Compiler runtimes:** the installed toolchain's license directory is copied
  with the firmware. This retains Newlib notices, GCC runtime exception text,
  and component licenses without distributing the compiler executable itself.
- **SDK build provenance:** the installed SDK records lib-builder commit
  `ee57070cc8571c488a64b8ef028684acdc6ff5e2`. That build-only repository does not
  provide a standalone license file, so its scripts are linked in the manifest
  rather than copied under an invented license. Hallzee's build commands,
  exact SDK configuration, and Arduino/IDF source are supplied.

## Rebuilding and installing modified firmware

Use the one-command bootstrap in [Testing and installation](testing-and-installation.md),
then the firmware flasher there to build and install your modified checkout.
The source flasher stages the sketch, installs pinned dependencies, and programs
the ESP32 over USB. OTA signing authenticates Hallzee releases; it does not
prevent an owner from installing modified firmware over USB. Do not enable
hardware secure-boot/flash-encryption restrictions as part of these instructions.
To use your own OTA signing key, change the firmware's public key and the
client/tool trust configuration, then install that firmware by USB. Hallzee's
private release key is not needed and is never included in source archives.

## USB executables

Hallzee builds esptool 5.3.1 in a fresh Python 3.13 virtual environment using
`firmware/requirements.txt`. All package versions and hashes are locked,
including PyInstaller's build dependencies. `scripts/build-usb-esptool.py` retains
every installed distribution's original copyright/license files and source
archive, verifies source hashes against the lockfile, and captures the actual
interpreter version, OpenSSL version/license, supplied interpreter notices,
native binary input hashes, and final executable hash. This records build
dependencies too; it does not claim that every installed package is frozen into
the executable. The standalone executable is checked with version and flash
command help invocations before bundling. It uses the same esptool CLI interface
as the upstream executable.

The pinned mklittlefs 4.0.2 source archive includes littlefs and TCLAP licenses.
Its [upstream Windows build](https://github.com/earlephilhower/mklittlefs/blob/db0513ade5d4ffb757b0529ce277d0be93e4e46a/.github/workflows/make-release.yml)
includes `libwinpthread-1.dll`; its MIT/BSD notices are retained in
`MinGW-winpthreads-COPYING.txt`. Its statically linked GCC runtimes use the
included GCC Runtime Library Exception and GPL text. This does not relicense
mklittlefs or require recipients to install the compiler.

## Generating the materials

The normal release workflows run these scripts automatically. Use the desktop
packaging script after installing the contributor prerequisites; it collects
the app/helper's resolved dependencies and exact Mac runtime-pack notices:

```sh
bash scripts/package-client-macos.sh 1.0.0 osx-arm64 dannysombrero/hallzee packaged
```

`scripts/bundle-firmware-licenses.py` downloads pinned upstream repositories to
`.tools/release-sources` and archives source without Git metadata. Its `--scope
firmware` mode requires a linker map for each variant; `--scope usb` collects
the two USB tools. It uses Git/Python installed by the contributor bootstrap
or the CI runner. The cache may be reused; modified/ignored files in cached
checkouts and recursive submodules are rejected before archiving.

For Python USB tooling, CI installs Python 3.13 and runs:

```sh
python scripts/build-usb-esptool.py --output usb/tool --licenses usb/licenses/python --sources usb-python-sources
```

To update the Python tool lockfile, change the `.in` file and regenerate it
with `uv pip compile firmware/requirements.in --project firmware
--python-version 3.13 --universal --generate-hashes --output-file firmware/requirements.txt`.
The release workflow installs prerequisites automatically; contributors can use
an isolated `uv` installation for dependency maintenance.
The configuration matches the existing USB package matrix (Mac ARM64 and Windows
x64). It allows the patched cryptography 50.0.0 dependency; esptool's Intel-Mac
dependency ceiling would otherwise retain vulnerable versions. This does not
change Intel Mac desktop support.

Run `python scripts/audit-usb-python.py` to check all locked versions, including
Windows-only packages, against the public OSV database. Network errors, malformed
responses, and incomplete/paginated results fail the check. The script uses the
[OSV batch API](https://google.github.io/osv.dev/post-v1-querybatch/).
Source/license output directories must be empty for every build, preventing an
old dependency's source archive from surviving a later release.

Review both platform builds and their source inventories before publishing the
updated USB binaries. A Mac verifies the local tool build and simulated USB
migration. Windows CI verifies the Windows executable build/startup; real Windows
USB device access still requires a Windows PC and is not verified by a Mac run.

## Remaining upstream limits

The pinned Wi-Fi, PHY, coexistence, and Bluetooth radio repositories provide
Apache-2.0 license texts alongside binary archives, but not their implementation
source. The release retains those supplied binaries and notices; collecting
them does not supply missing source. See the pinned
[Wi-Fi component](https://github.com/espressif/esp-idf/tree/b774170ff46c393eeb5e495ea37936038d3f4f4f/components/esp_wifi/lib)
and [PHY component](https://github.com/espressif/esp-idf/tree/b774170ff46c393eeb5e495ea37936038d3f4f4f/components/esp_phy/lib).
The manifest makes this source-availability boundary visible. This process does
not assert a blanket System Libraries exception or add an exception to Hallzee's
AGPL license; the copyleft treatment of those linked binaries remains a legal
review question before claiming that every part of a firmware image is open source.
