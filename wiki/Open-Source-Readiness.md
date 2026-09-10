# Open source readiness — September 9, 2026

Hallzee is the project name; Hallzee Labs holds the copyright in original
Hallzee work. `dannysombrero/hallzee-mono` is still private. The launch-readiness
implementation is merged, and the advertised branch/tag history was sanitized
on September 10, 2026. This pass does not rename the repository.

## Dependency fixes

| Scope | Result |
| --- | --- |
| Website npm audit | Zero full/production findings, previously 22 affected packages |
| Desktop/tool NuGet graphs | Zero reported vulnerable packages after native SQLite replacement |
| USB Python build graph | Zero OSV findings across 26 pinned packages after updating cryptography |
| Authenticated GitHub dashboard | 42 open, 0 closed: 25 high, 13 moderate, 4 low; all in the old website lockfile |

GitHub counts individual advisories differently from npm's affected-package
summary. Alerts remain until the new lockfile reaches the default branch and
GitHub rescans. No alerts were dismissed.

React/DOM/server components are aligned at 19.2.8; vinext, Vite, Cloudflare and
affected transitives are updated. Scoped overrides fix Drizzle's obsolete
esbuild loader and Miniflare's pinned Sharp dependency. Fresh installation,
production rendering, six website tests, lint and full TypeScript checking pass
on Node 22. CLI commands no longer depend on POSIX environment assignments.
See [website dependencies](Dependency-Updates-Web).

The database uses Microsoft.Data.Sqlite.Core 8.0.30, SQLitePCLRaw configuration
3.0.5 and SQLite 3.53.4. All 108 core and 86 desktop tests passed on Apple Silicon
and under Intel/Rosetta, and on Windows in GitHub Actions. Three desktop runtime
publishes, two USB-tool publishes, and simulated USB checks pass. A deliberately
vulnerable temporary graph proved CI rejects transitive advisory warnings.
See [desktop dependencies](Dependency-Updates-Desktop).

## GitHub settings verified

- Main requires PRs, an up-to-date branch, resolved conversations, and
  **Repository hygiene**. Approvals are off for one maintainer. Force pushes and
  deletion are blocked; administrator bypass remains available for maintenance.
- Dependency graph, Dependabot alerts/security updates, malware alerts, and
  grouped security updates are enabled. The repository-owned Windows and Mac
  NuGet submissions passed on `main`; GitHub's redundant Linux-only automatic
  submission is disabled.
- **Dependabot on self-hosted runners** is disabled. Hosted Actions runners
  remain enabled for dependency updates.
- Workflow tokens default to read-only. Actions creating/approving PRs and
  private-fork workflow execution are off. No self-hosted runners are configured.
- The `firmware-release` environment now permits **branch `main` only, zero
  tags**. Its signing secret is present; its contents were not read or changed.
- Security policy is enabled. Private reporting and further secret-protection
  controls are unavailable in this personal/private configuration. Enable the
  available controls after making the repository public, then verify the
  reporting link in `SECURITY.md`.

The unfiltered **PR readiness** check combines desktop tests, complete Mac
packaging, firmware checks, website checks, dependency audits and redacted
history scanning. It and **Repository hygiene** are required on `main`.
Existing check names are preserved.
The manual dependency-update workflow provides a patch instead of pushing to a
hardcoded branch. See [CI and security](CI-and-Security).

## History sanitation

The refreshed audit through `e7c6680` covers 236 reachable commits and 1,716
blobs across local and fetched remote refs. All eight published branch/tag refs
matched the cached refs when checked. Gitleaks found two historical synthetic
protocol keys; the new configuration exempts only that exact value in those two tests. The configured
scan is clear. A temporary Git fixture proved the same value in application
code still triggers detection. No production private key or credential was
confirmed by this scan.

Historical removals are `receiver/bathroom_trips.csv`, local hosting metadata,
a machine identifier under `Library/`, `.local/`, `TestResults/`, and
`.DS_Store`. Their contents are omitted here. The firmware partition CSV and
official public verification key must remain intact.

A fresh review snapshot covered 267 reachable commits and 1,796 blobs. The
advertised history of all 27 GitHub branches and `windows-client-latest` was
rewritten atomically with exact force-with-lease guards. A fresh clone confirms
that those branch/tag refs contain none of the targeted paths and passes the
configured Gitleaks scan. `main` moved from `d5d4412` to sanitized commit
`7969faf`; its source tree is unchanged. Force pushes were disabled again after
the maintenance operation. A verified private recovery bundle is retained under
the ignored `.tools/security-audit/` directory.

Replace or carefully rebase old clones afterward. GitHub PR refs, caches, forks,
old Actions logs/artifacts, and downloaded copies require separate review;
rewriting Git does not erase them. Keep the recovery bundle private and rebuild
release artifacts after sanitation so their source links use the final history.

GitHub still advertises read-only pull-request heads for PRs 1, 2, 3, 16, 24,
25, 26, 28, 29, 30, 31, 32, 33, 34, and 35. They retain the earlier history and
cannot be replaced with a normal push. A private Support request is prepared
with the final commit map. Keep the repository private until GitHub removes
those refs/cached views or the repository is recreated after preserving needed
settings and issue data. See [GitHub's sensitive-data removal guidance](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository).

A read-only retention inventory found 148 Actions runs, 74 unexpired artifacts
(about 3.95 GB), and one 75 MB release download. Artifact names and upload paths
indicate desktop packages, firmware/USB packages, and small release metadata
bundles. Their payloads and log contents have not been scanned by this inventory.
The private report records IDs, source commits, sizes and expiry dates for cleanup.

The `windows-client-latest` tag was rewritten from `73ee3289` to sanitized
commit `507ff31`. The legacy Windows binary's exact source provenance remains
unverified, so retire that old release before public visibility and publish
fresh, immutable versioned releases.

## Licensing and packaging

Original code remains AGPL-3.0-or-later; models use CC BY-SA 4.0. Copyright names
Hallzee Labs. Full license texts and separate DM Sans/Inter notices are retained.
Third-party copyrights remain with their owners.

Releases regenerate inventories from their actual restored targets, including
the Mac helper's runtime pack. Mac notices now live in `Contents/Resources`,
fixing a reproduced signing failure. Both Mac ZIPs were extracted, strictly
signature-verified, and their helpers launched; Intel verification used Rosetta.
The packaged SQLite libraries load 3.53.4.

Arduino libraries are pinned, including dependencies. Source bundling records
exact Arduino/ESP-IDF commits, submodules, linked managed components, SDK
configuration, compiler notices, and USB-tool sources. Keypad's inconsistent
upstream GPL/LGPL notices are preserved with its source. See
[release licensing](Release-Licensing) for materials and upstream limits.

Publication verifies repository, default branch, tested commit ancestry and
source-archive checksums. It refuses missing companions or existing version
tags, preserves the single Windows ZIP, and attaches the teacher guide without
overwriting README. Offline publication/input tests pass.

The complete five-variant firmware build also passed, with at least 209,744
bytes of application-slot headroom. Its isolated test-key package verifies
against the test key and is correctly rejected by the unchanged official
trust configuration. The actual rebuilt Mac USB binary/source ZIP pair passes
the publication hash checks. All 51 release-input, publication, licensing and
repository-hygiene tests pass; native firmware and touch tests pass too.

[Pull request readiness run 34409156145](https://github.com/dannysombrero/hallzee-mono/actions/runs/34409156145)
and the separate repository hygiene workflow passed on `54024ac`. This includes
Windows/Mac desktop tests, both complete Mac packages, Windows/Mac USB executable
builds and startup checks, Arduino display/touch builds, Linux/Windows website
builds/tests/lint/TypeScript, and the dependency and secret scans. These runner
checks do not exercise physical Bluetooth, USB devices, or classroom workflows.

## Remaining launch steps

1. Submit the prepared GitHub Support request for retained PR refs and wait for
   cleanup; alternatively preserve needed repository metadata and recreate the
   private repository from the sanitized history.
2. Review old artifacts/logs and retire the legacy Windows release.
3. Switch this repository to Public, enable reporting and secret protection,
   and verify signed-out access.
4. Build fresh releases, complete physical acceptance, and publish those exact
   artifacts in this same repository.

A Mac is sufficient for local audits, shared tests and Mac package verification.
A Windows PC is required for actual Windows launch, native database loading,
WinRT Bluetooth pairing/reconnect, dialogs, firmware transfer and USB recovery.
Those physical Windows capabilities, OTA on both displays, and clean-machine
teacher installation remain unverified. See [release readiness](Release-Readiness).
