# Release licenses and source

The maintained release licensing instructions are in
[docs/release-licensing.md](https://github.com/dannysombrero/hallzee/blob/main/docs/release-licensing.md).
They explain the AGPL software and CC BY-SA model split, exact release
inventories, pinned Arduino dependencies, source archives, Keypad's differing
source/header notices, and rebuilding modified firmware over USB.

Packaged copies of Hallzee's root notices use `LICENSE.txt` and `COPYRIGHT.txt`
so Windows opens them as plain-text files. Upstream dependency filenames remain
unchanged inside the accompanying `licenses` directory.

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
against OSV, including dependencies declared with extras and Windows-only
dependencies. The builder and auditor share `scripts/usb_python_lock.py`, which
matches extras such as `esp-pylib[cli,ide,serial]` to the base distribution name
while preserving pip's original requirement lines and hashes. Regenerate the
complete lock from the repository root as described in the source guide; retain
the platform dependencies and explicitly pinned setuptools source-build backend.
The firmware workflow runs the parser/lock regression tests in `prepare`.
Incomplete/error responses fail the check. USB builds require fresh
source/license output directories so stale dependencies cannot enter a release.
