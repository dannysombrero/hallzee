# Release licenses and source

The maintained release licensing instructions are in
[docs/release-licensing.md](https://github.com/dannysombrero/hallzee-mono/blob/main/docs/release-licensing.md).
They explain the AGPL software and CC BY-SA model split, exact release
inventories, pinned Arduino dependencies, source archives, Keypad's differing
source/header notices, and rebuilding modified firmware over USB.

Every firmware release retains the actual SDK configuration, linked-component
inventory, third-party sources, and original notices alongside its downloads.
Desktop/USB NuGet inventories are regenerated for the project and runtime being
published. Preserve the corresponding source archives when redistributing.

The USB release builds esptool from a hash-locked Python dependency graph and
publishes a separate source/notices ZIP for each platform. The source manifest
records exact installed package versions, native input hashes, and interpreter
details. Windows mklittlefs includes the original MinGW winpthreads and GCC
runtime notices. The source guide documents rebuilding modified firmware over
USB and the closed Espressif radio components that remain under upstream terms.
The pinned radio repositories supply Apache-2.0 notices and binary archives but
omit implementation source; no new Hallzee license exception is asserted.

Run `python scripts/audit-usb-python.py` to check all locked Python dependencies
against OSV. Incomplete/error responses fail the check. USB builds require fresh
source/license output directories so stale dependencies cannot enter a release.
