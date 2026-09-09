# CI and repository security

Every pull request runs **Pull request readiness** with a read-only token and
no firmware signing secrets. It calls the Mac/Windows desktop validation and
firmware validation workflows, builds/tests/lints/type-checks the website on
Linux and Windows, audits npm, and
scans reachable Git history using a checksum-verified Gitleaks binary. Running
all checks also covers changes to shared scripts and dependency configuration.
The firmware workflow also builds the standalone USB executable on Windows
and Mac and audits every pinned Python package against OSV.

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
Dependabot alerts, security updates, and dependency submission enabled. No
self-hosted runners are needed for this project.

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
