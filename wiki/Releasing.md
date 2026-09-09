# Build, test, and publish Hallzee

Teacher instructions live in [the teacher guide](Getting-Started-Users).
The source guide links to the source repository; publishing replaces those
links with the chosen public repository and installs the guide as that repository's README. Desktop
and firmware release lists are linked separately, so one cannot hide the other.
The public repository contains downloads and instructions, not student data.

## One-time maintainer setup

Use GitHub's web interface; no local Git, .NET, or Arduino installation is needed.

1. Create a **public**, dedicated downloads repository with an initial README.
   Keep the source repository private if desired. In the source repository's
   **Settings → Secrets and variables → Actions → Variables**, set
   `HALLZEE_RELEASE_REPOSITORY` to its `owner/repository`. Set this **before**
   building a client: the destination is embedded in the app's trusted update
   feed and saved with the build artifacts. Keep that URL stable after release.
2. Create a `public-release` environment and its secret
   `HALLZEE_PUBLIC_RELEASE_TOKEN`. The token needs read access to Actions artifacts
   in the source repository and Contents write access to the public repository.
   Limit access to those repositories. Configure reviewed release branches and
   reviewers in this environment as appropriate for your team.

For firmware, also configure the existing `firmware-release` environment and
`HALLZEE_FIRMWARE_SIGNING_KEY`; see [firmware updates](Firmware-Updates).
The public key must match the terminals and clients already distributed.
Do not put the private signing key in the downloads repository.

## Build privately

Run **Build Desktop Release Packages** or **Build Firmware Release Packages**
from the reviewed branch with a new `major.minor.patch` version (for example,
`1.0.0`). The desktop build also accepts `v1.0.0`, `1.0`, and `v1.0`, trimming
surrounding whitespace and normalizing these to `1.0.0` before building. One
preflight job validates the version and destination; all platform builds and
release metadata use its same normalized outputs. Unsupported input now reports
what to enter instead of a bare `AssertionError`. Firmware versions still use
the full numeric form, such as `1.0.0`. Actions installs
its build tools and runs tests. The desktop workflow builds Windows x64 plus
Mac Apple-silicon and Intel ZIPs, including the native Mac Bluetooth helper.
Both workflows save the source revision, version, public destination, guide,
and release notes alongside their downloadable artifacts.

The root `global.json` selects the latest installed stable .NET 8.0 SDK, even
when the runner also has .NET 9 or 10. The Mac workload installer uses the same
`dotnet` executable as packaging. This keeps the existing `net8.0-macos` helper
on its intended toolchain; upgrading that target remains separate work.

Mac packaging explicitly signs and verifies native libraries, including the
helper's `Contents/MonoBundle` libraries that `codesign --deep` does not discover.
It also launches the helper without arguments on a matching Mac architecture to
check runtime loading without starting Bluetooth. A skipped architecture launch
check must be completed on a matching Mac before release.

Edit `docs/client-release-notes.md` or `firmware/release-notes.md` before building.
Download the artifacts and test them. Record the successful run's numeric ID
from its URL. Artifacts expire according to repository retention settings;
publish before expiry or build and test again. A build does not publish a release.
The old moving Windows workflow is now a manual legacy build only.

## Publish the tested files

Run **Publish Tested Release**, select `client` or `firmware`, and enter the
successful build run ID. It validates the run and expected asset set, refuses
private destinations and existing version tags, uploads a draft, updates the
public teacher README, then publishes that draft. It attaches a guide and SHA-256
checksums. It does not rebuild tested binaries or overwrite existing releases.

The destination must be a separate public repository with a README. On a partial
failure, inspect the draft release and workflow log; do not bypass version checks
or replace public assets. A new build/version is the simplest recovery. Review
and remove an unpublished failed draft only if you intentionally want to retry.

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
[the v1.0 review](Release-Readiness) for the minimum classroom pilot checklist.
