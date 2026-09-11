# Build, test, and publish Hallzee

Teacher instructions live in [the teacher guide](getting-started-users.md).
The open source repository is also the public download repository. GitHub Releases
hold the desktop and firmware files, while the README links teachers to the guide.
Do not commit student data, private keys, or local build output.

## One-time maintainer setup

Use GitHub's web interface; no local Git, .NET, or Arduino installation is needed.

1. Change the repository visibility to **Public** only after reviewing its
   history and local files for credentials, student data, and unpublished
   material. Rotate anything that was ever committed before making it public.
2. Original software uses AGPL-3.0-or-later in `LICENSE`; models use CC BY-SA 4.0
   in `models/LICENSE`. See [license](license.md), `COPYRIGHT`, `CONTRIBUTING.md`,
   and `SECURITY.md`. Follow [release licensing](release-licensing.md) for the
   exact dependency inventory and source bundles shipped with each release.
3. The workflows default `HALLZEE_RELEASE_REPOSITORY` to this repository, so no
   repository variable or `HALLZEE_PUBLIC_RELEASE_TOKEN` is needed. The publish
   workflow uses GitHub's built-in token with Actions read and Contents write
   permissions. Keep the existing `firmware-release` environment secret for the
   private firmware signing key. Its deployment rule permits branch `main`
   only, with no tags. See [CI and security](ci-and-security.md).

For firmware, also configure the existing `firmware-release` environment and
`HALLZEE_FIRMWARE_SIGNING_KEY`; see [firmware updates](firmware-updates.md).
The public key must match the terminals and clients already distributed.
Do not put the private signing key in the downloads repository.

## Build and test before publication

Actions logs and build artifacts in a public repository are not private staging.
Use only synthetic classroom data in builds and artifacts.

Run **Build Desktop Release Packages** or **Build Firmware Release Packages**
from the protected default branch (`main`) with a new `major.minor.patch` version (for example,
`1.0.0`). Both builds also accept `v1.0.0`, `1.0`, and `v1.0`, trimming
surrounding whitespace and normalizing these to `1.0.0` before building. One
preflight job validates the version and destination; all platform builds and
release metadata use its same normalized outputs. Unsupported input now reports
what to enter instead of a bare `AssertionError`. Firmware packages and release
metadata both use that normalized numeric version. Actions installs
its build tools and runs tests. The desktop workflow builds Windows x64 plus
Mac Apple-silicon and Intel ZIPs, including the native Mac Bluetooth helper.
Both workflows save the source revision, version, public destination, guide,
and release notes alongside their downloadable artifacts.

The root `global.json` selects the latest installed stable .NET 8.0 SDK, even
when the runner also has .NET 9 or 10. The Mac workload installer uses the same
`dotnet` executable as packaging. This keeps the existing `net8.0-macos` helper
on its intended toolchain; upgrading that target remains separate work.
The Mac jobs explicitly select `/Applications/Xcode_16.2.app` on `macos-14`,
which includes the macOS 15 SDK required by the helper. The runner default
Xcode 15.4 causes `MM0179`/`MM2301` linker failures. Xcode and SDK versions
are printed before packaging so the selected toolchain is visible in the log.

Mac packaging explicitly signs and verifies native libraries, including the
helper's `Contents/MonoBundle` libraries that `codesign --deep` does not discover.
It also launches the helper without arguments on a matching Mac architecture to
check runtime loading without starting Bluetooth. A skipped architecture launch
check must be completed on a matching Mac before release.

The Actions artifacts are **Hallzee-Windows-win-x64**,
**Hallzee-Mac-osx-arm64**, and **Hallzee-Mac-osx-x64**. Download the Windows
artifact and extract once to reach the executable and supporting files. Windows
builds upload the app files directly, letting GitHub create the download ZIP.
Publication reuses that exact artifact ZIP as `Hallzee-Windows-win-x64.zip`; it
does not repackage the app. Older build runs and their `Desktop-*` artifact names
remain publishable. Mac Actions artifacts wrap the prebuilt Mac ZIP because the
inner archive preserves application permissions for installation and release.

Edit `docs/client-release-notes.md` or `firmware/release-notes.md` before building.
Download the artifacts and test them. Record the successful run's numeric ID
from its URL. Artifacts expire according to repository retention settings;
publish before expiry or build and test again. A build does not publish a release.
The old moving Windows workflow is now a manual legacy build only.

## Recover a failed firmware/USB build

The firmware workflow's `prepare` job runs the packaging regression tests before
compilation, including parsing the checked-in Python lock and checking its Mac,
Windows, and source-build dependencies. The USB matrix uses `fail-fast: false`
so one platform's failure does not cancel the other. Both must succeed before
release metadata is produced; a partial USB build cannot be published.

An older run reporting `Expected a pinned requirement: esp-pylib[cli,ide,serial]`
used a parser that rejected dependency extras. Its sibling Windows job may show
`extracting archive ... interrupted` followed by `The operation was canceled`
because the matrix canceled it after the Mac failure. Check the job conclusions
and timestamps before diagnosing that as an Arduino archive or Windows path error.

If an older run fails in **Bundle USB tools** because `usb/licenses` already
exists, the Python esptool build has already created `usb/licenses/python`.
Bundling now merges the firmware license tree into `usb/licenses`, preserving
the Python notices and source manifest. Do not delete the existing directory to
work around the collision. A failed copy still stops packaging on both platforms.

After the fix reaches `main`, choose **Build Firmware Release Packages → Run
workflow** to start a new run using that revision. **Re-run jobs** on an old run
uses the old source revision. The same version may be reused if it was never
published or installed; otherwise use a newer version. Publish only the new,
successful build run ID after testing its packages. Do not edit a downloaded
`release.json` to work around a failed build.

A Mac is sufficient for the parser tests and Mac standalone esptool build.
Windows CI must verify Windows dependency installation and executable startup.
A Windows PC is required for physical USB/COM-port discovery and esptool
read/write verification. Those Windows behaviors are not verified by Mac tests.


## Publish the tested files

Run **Publish Tested Release** from the default branch, select `client` or
`firmware`, and enter the successful build run ID. It validates the run's
repository, default branch, tested source commit and expected asset set, refuses
private destinations and existing version tags, uploads a draft, then publishes
that draft. It attaches the tested guide and SHA-256 checksums. It targets the
tested source commit and links its source archive. README changes go through
normal source review; publication does not overwrite the repository README.

Firmware publication also requires the exact firmware/USB third-party source
archives, `Hallzee-Firmware-Licenses.zip`, and separate Python-source companions
for Windows and Mac USB tools. It checks archive hashes and matches each
standalone esptool binary to its source manifest. These source downloads sit
beside the USB ZIPs; teachers still extract only their platform's USB ZIP.

The advertised branch/tag history was sanitized on September 10, 2026. Do not
reuse packages built before that rewrite. Publication rejects a tested commit
that is no longer an ancestor of the default branch; this keeps source download
links valid.

The release is created in this same repository. On a partial failure, inspect the
draft release and workflow log; do not bypass version checks or replace public
assets. A new build/version is the simplest recovery.

Release tags are `client-v1.0.0` and `firmware-v1.0.0`. The app queries that public
repository's GitHub releases API, ignores drafts/prereleases, and compares
numeric versions independently. It validates signed firmware compatibility
before offering installation. Terminals have no internet feed; the desktop
fetches firmware and sends it over Bluetooth. No new terminal URL is needed.

The client version comes from the same build version used in the release tag.
Developer builds default to the source repository; no private token is embedded
in an app. Manual **Updates** works without a connected terminal for desktop
checks. Startup checks and automatic desktop replacement are deferred.

## Release acceptance

A Mac is sufficient for shared UI, release-parser, package verification, and
firmware simulator tests. **A Windows PC is required** for clean Windows ZIP
launch, WinRT discovery/pairing, saved credentials/reconnect, native file dialogs,
firmware write pacing/notifications, and reconnect after a terminal restart.
Windows behavior has not yet been verified for this release work.

Test the downloaded Mac `.app` on each supported architecture, including a Mac
without development tools, Bluetooth permission and helper launch. Packages
currently use ad-hoc signing only; Developer ID notarization and Windows publisher
signing are not configured. Confirm school IT's installation route before wider
rollout. Neither successful compilation nor a local source run proves clean
machine installation works.

On a physical terminal, verify checkout/check-in, disconnect/reconnect recovery,
one signed update with retained data/pairing, and interruption recovery on both
displays. Mac alone is insufficient for the complete cross-platform release.
Mac/Windows physical OTA and USB migration still need verification. See
[the v1.0 review](release-readiness.md) for the minimum classroom pilot checklist.
