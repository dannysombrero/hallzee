# CI and repository security

Every pull request runs **Pull request readiness** with a read-only token and
no firmware signing secrets. It calls the Mac/Windows desktop validation and
firmware validation workflows, builds/tests/lints/type-checks the website on
Linux and Windows, audits npm, and
scans reachable Git history using a checksum-verified Gitleaks binary. Running
all checks also covers changes to shared scripts and dependency configuration.
The firmware workflow also builds the standalone USB executable on Windows
and Mac and audits every pinned Python package against OSV. Its fast validation
job runs the release-licensing regression suite, covering target-specific
dependency inventories, firmware source coverage, archive path safety, stale
source rejection, native-input provenance, and fail-closed OSV responses.

The final **PR readiness** job fails if any dependency fails, is cancelled, or
is unexpectedly skipped. It has no path filter, so documentation-only PRs do
not get stuck waiting for a missing required check. The existing **Repository
hygiene** check keeps its name and still runs independently.

After the first successful run, add **PR readiness** to the required checks on
`main`, alongside **Repository hygiene**. Keep PRs, an up-to-date branch, and
resolved conversations required. With one maintainer, leave required approvals
off. Block force pushes/deletions during normal work. History sanitation needs
a separately coordinated maintenance window before those protections resume.

## Dependency updates

Dependabot checks GitHub Actions, npm, the USB Python build, and all NuGet
project directories weekly.
Related React, Cloudflare, Avalonia, and SQLite packages are grouped for review.
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

Relevant PRs exercise both restores and run a checksum-pinned official detector
against the transferred graphs. They verify every resulting manifest path with
read-only permissions and no snapshot upload. Snapshot submission runs only on default-branch pushes or an
explicit maintainer dispatch selecting the default branch, using a separate job
with Contents write. Dispatching another branch cannot submit a snapshot. The
first actual upload should be verified after merge by running this workflow
on `main` and checking the dependency graph's refreshed manifests.
These snapshots describe restored NuGet package graphs. SDK/workload framework
packs are tracked separately by the release's dependency/license inventory;
the graph detector does not promise to inventory every bundled runtime binary.
The official upload action downloads the latest detector and offers no version
input. Before uploading, the workflow requires that release to match the version
and checksum used in PR validation; a new upstream release requires updating
that pin and validating again.

GitHub's built-in **Automatic Dependency Submission (NuGet)** currently tries
to restore every project on Ubuntu. It fails with `NETSDK1100` on the Windows
project and cannot provide the Mac workload. After the replacement successfully
uploads both snapshots, disable **Automatic dependency submission** in GitHub
settings while keeping **Dependency graph**, Dependabot alerts/security updates,
and the repository-owned workflow enabled. This setting change is still pending;
the replacement's PR checks validate graphs without submitting them.
See [GitHub dependency submission](https://docs.github.com/en/code-security/how-tos/secure-your-supply-chain/secure-your-dependencies/use-dependency-submission-api)
and the [official action inputs](https://github.com/actions/component-detection-dependency-submission-action).

Review [website dependency updates](dependency-updates-web.md),
[desktop dependency updates](dependency-updates-desktop.md), and
[release licensing](release-licensing.md) before upgrading a dependency. Arduino
libraries use `firmware/arduino-libraries.txt`; Dependabot does not manage them.

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
verify the private reporting link in [SECURITY.md](../SECURITY.md). Keep
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
writing over the README. See [releasing](releasing.md) for the acceptance and
publication sequence.

A Mac is sufficient for local scripts, source scans, and shared tests. CI also
builds/tests on Windows. A physical Windows PC is still required for executable
launch, WinRT Bluetooth pairing/reconnect, and firmware transfer behavior;
those physical Windows capabilities have not been verified by this pass.

References: [GitHub branch protection](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches),
[reusable workflows](https://docs.github.com/en/actions/reference/workflows-and-actions/reusing-workflow-configurations),
and [Gitleaks configuration](https://github.com/gitleaks/gitleaks#configuration).
