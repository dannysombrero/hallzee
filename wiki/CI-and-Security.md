# CI and repository security

Every pull request runs **Pull request readiness** with a read-only token and
no firmware signing secrets. Its Linux core always runs the portable .NET and
Python regression suites plus a reachable-history scan using a
checksum-verified Gitleaks binary. It classifies the changed paths before
starting the more expensive jobs: website changes run the Node checks on
Linux, desktop changes package both Mac architectures in one macOS job,
Windows-relevant changes get one Windows build/test job, and firmware changes
get one Linux USB/audit/Arduino job. Changes to workflows, shared scripts, or
test infrastructure deliberately select every platform check.

The full manual desktop and firmware validation workflows remain available
for release candidates. They build the standalone USB executable on Windows
and Mac, run native firmware coverage, build optional firmware variants, and
exercise platform-specific release packaging. The PR core runs the
release-licensing regression suite, covering target-specific dependency
inventories, firmware source coverage, archive path safety, stale source
rejection, native-input provenance, and fail-closed OSV responses.

The final **PR readiness** job fails if a selected dependency fails or is
cancelled; jobs unrelated to the changed paths may be skipped. The workflow
itself has no path filter, so documentation-only PRs do not get stuck waiting
for a missing required check. The existing **Repository hygiene** check keeps
its name and still runs independently. In-progress runs are cancelled when a
new commit reaches the same PR.

After the first successful run, add **PR readiness** to the required checks on
`main`, alongside **Repository hygiene**. Keep PRs, an up-to-date branch, and
resolved conversations required. With one maintainer, leave required approvals
off. Block force pushes/deletions during normal work. History sanitation needs
a separately coordinated maintenance window before those protections resume.

## Dependency updates

Dependabot checks GitHub Actions, npm, the USB Python build, and all NuGet
project directories monthly. Each ecosystem is limited to two open version
update PRs, and routine updates are grouped to avoid a burst of near-identical
workflow runs. Related React, Cloudflare, Avalonia, and SQLite packages retain
their focused groups. Avalonia 12, Microsoft.Data.Sqlite 9+, Microsoft.NET.Test
SDK 18+, and xUnit Visual Studio runner 3+ stay deferred until the app moves
beyond its current .NET 8/Avalonia 11 baseline. Dependabot continues to offer
compatible updates, including security fixes within those supported lines.
The website audit covers development and production dependencies; CI's .NET
audit covers transitives and treats known vulnerability warnings as errors.
Registry availability failures remain visible in build output.

In repository security settings, keep **Dependabot on GitHub Actions runners**
enabled and **Dependabot on self-hosted runners** disabled. This project has no
runner carrying the required `dependabot` label; enabling the latter leaves
update jobs queued. After correcting it, use **Check for updates** to start new
jobs; toggling the setting does not start them. This setting change requires
verification in GitHub and is not applied by committing YAML.
See [GitHub's runner configuration instructions](https://docs.github.com/en/code-security/how-tos/secure-your-supply-chain/manage-your-dependency-security/configure-on-self-hosted-runners).

### Resolved NuGet dependency submission

**Submit resolved NuGet dependencies** restores Windows and Mac projects on
their respective runners, then submits their resolved graphs from Linux using
GitHub's component-detection action. It preserves the real repository `.csproj`
paths and uses separate stable Windows/Mac snapshot identifiers. No client
build, release signing, or firmware installation is involved.
Portable transfer artifacts stay in the runner's temporary directory, outside
the detector's source tree; only activated graphs can become submitted manifests.
All listed platform projects are restored. The detector emits manifests for
projects declaring NuGet packages or package downloads; a project using only
project references is represented by its referenced projects' manifests. Validation
checks every emitted path and requires every restored NuGet package name/version
to appear in the combined platform graph, so that distinction cannot hide a
missing transitive dependency.

PR readiness tests the graph-transfer, path-validation, and fail-closed logic
on Linux. The platform restores and checksum-pinned official detector run only
after a relevant change reaches `main`, or on explicit maintainer dispatch.
The separate submission job then uses Contents write; dispatching another
branch cannot submit a snapshot. This avoids repeating macOS and Windows
restores for every intermediate PR commit while preserving validation of the
exact merged dependency graph. Verify the resulting manifests in GitHub's
dependency graph after a submission run.
These snapshots describe restored NuGet package graphs. SDK/workload framework
packs are tracked separately by the release's dependency/license inventory;
the graph detector does not promise to inventory every bundled runtime binary.
The official upload action downloads the latest detector and offers no version
input. Before uploading, the workflow requires that release to match the version
and checksum used in PR validation; a new upstream release requires updating
that pin and validating again.

GitHub's built-in **Automatic Dependency Submission (NuGet)** tried to restore
every project on Ubuntu. It failed with `NETSDK1100` on the Windows project and
could not provide the Mac workload. The repository-owned replacement has now
uploaded both platform snapshots successfully, and **Automatic dependency
submission** is disabled. **Dependency graph**, Dependabot alerts/security
updates, and the repository-owned workflow remain enabled.
See [GitHub dependency submission](https://docs.github.com/en/code-security/how-tos/secure-your-supply-chain/secure-your-dependencies/use-dependency-submission-api)
and the [official action inputs](https://github.com/actions/component-detection-dependency-submission-action).

Review [website dependency updates](Dependency-Updates-Web.md),
[desktop dependency updates](Dependency-Updates-Desktop.md), and
[release licensing](Release-Licensing.md) before upgrading a dependency. Arduino
libraries use `firmware/terminal/arduino-libraries.txt`; Dependabot does not manage them.

The manual **Prepare preview dependency update** action updates within declared
version ranges, validates the result, and uploads a patch and lockfile. Download
the artifact and run `git apply dependency-update.patch` on a contributor
branch, then open a PR. It never pushes directly to a source branch. Dependabot
PRs are the normal route for routine version updates.

## Secret scanning and reports

Gitleaks uses its default rules. The only added exception matches the exact
synthetic `0123456789ABCDEF` protocol key in two historical unit-test files.
Other keys in those files and that value in other files remain scanned. Output
is fully redacted; do not upload unredacted reports. Repository hygiene also
rejects tracked private keys, classroom exports, and local host metadata.

GitHub's **Private vulnerability reporting**, **Secret scanning**, and **Push
protection** are separate repository settings; committing YAML does not enable
them. Enable the available controls after making this repository public, then
verify the private reporting link in [SECURITY.md](https://github.com/dannysombrero/hallzee/blob/main/SECURITY.md). Keep
Dependabot alerts, security updates, and repository-owned dependency submission
enabled as described above. No self-hosted runners are needed for this project.

PR workflows must remain read-only and must not use `pull_request_target` to
execute contributor code. Require approval for outside-contributor workflow
runs where GitHub exposes that control. Keep Actions creating/approving PRs
disabled. Release publication is a separate manual job with Contents write.
The `firmware-release` environment should allow only the `main` branch to use
`HALLZEE_FIRMWARE_SIGNING_KEY`; do not grant access to arbitrary tags or forks.

## Public releases

Build and publish from the protected default branch. Publication verifies the
successful manual build's product, repository, branch, source commit, version,
and expected files. It refuses a commit removed by history rewriting, an
existing version tag, or a private destination. Rebuild after sanitation; do not
reuse a pre-cleanup artifact whose source links would become stale.

Keep numbered `client-v*` and `firmware-v*` tags immutable after any history
maintenance. Publication attaches the tested guide and checksums without
writing over the README. See [releasing](Releasing.md) for the acceptance and
publication sequence.

A Mac is sufficient for local scripts, source scans, and shared tests. CI also
builds/tests on Windows. A physical Windows PC is still required for executable
launch, WinRT Bluetooth pairing/reconnect, and firmware transfer behavior;
those physical Windows capabilities have not been verified by this pass.

References: [GitHub branch protection](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches),
[reusable workflows](https://docs.github.com/en/actions/reference/workflows-and-actions/reusing-workflow-configurations),
and [Gitleaks configuration](https://github.com/gitleaks/gitleaks#configuration).

## Web client checks

`pr-readiness.yml` selects the reusable `web-client.yml` workflow for changes
to `web-client/`, `contracts/`, core protocol logic and shared CI/scripts. Its
result participates in **PR readiness** alongside the existing platform checks.
It installs pinned Node, locked npm packages and Playwright Chromium, runs the
web `check` command and C# conformance fixtures, and uploads a static review ZIP.
There is no deployment step. Tests use fictional data, simulate GATT only, and
exercise real IndexedDB, CryptoKey storage, locks, offline reload and interrupted
updates. Keep physical support claims tied to [recorded evidence](Web-Client-Acceptance.md).
