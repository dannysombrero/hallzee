# Open source readiness — September 9, 2026

Hallzee is the project name; Hallzee Labs is the copyright holder for original
Hallzee work. The remote is still `dannysombrero/hallzee-mono`; this pass does
not rename the GitHub repository.

## History review and sanitation

Fetched all configured remote branches/tags. The checkout is not shallow.
The first audit covered 232 reachable commits; the subsequent local commit
`a9a3508` brings the sanitation snapshot to 233. Gitleaks 8.30.1 scanned
all-branch history with redacted output. Its two findings were historical test
pairing-passkey literals in fake protocol/connection tests, not production
credentials. No production private key or credential was confirmed by that scan.
A scanner result is not a proof that all sensitive content is absent.

Deleted files still existed in history: `receiver/bathroom_trips.csv` (three
data rows), hosting project metadata, a developer machine identifier, and local
tool/test artifacts. Their contents are intentionally not copied into this report.

A separate sanitized mirror is prepared at
`.tools/security-audit/reviewed-history.git`. Its commit map is under
`filter-repo/commit-map`. It removes the historical CSV, hosting metadata,
`Library/`, `.local/`, `TestResults/`, and `.DS_Store` paths. The targeted
paths are absent from its reachable history; the same two test fixtures remain.
The source checkout, its refs, and GitHub history have not been rewritten.

This mirror contains the committed snapshot through `a9a3508`, not the
uncommitted work prepared afterward. Before replacing remote history, finish
the pending commit, fetch again, regenerate the sanitized mirror from that
final state, and compare each branch/tag against its old SHA. Coordinate a
force-with-lease push of the explicitly reviewed branch/tag refs. Do not use
`git push --mirror`: local remote-tracking/internal refs are not publication
targets. Old clones must be replaced or carefully rebased to avoid restoring
removed history. GitHub caches, pull-request refs, forks, Actions logs/artifacts,
and already downloaded copies require separate review; a Git rewrite does not
erase those. Keep the original local copy as a private recovery backup.

## Dependency findings

The session could not read the private GitHub Dependabot dashboard: no browser
was available, and the installed GitHub connector exposes no alert endpoint.
These are independent registry audits, not an export of the dashboard.

| Scope | Current result |
| --- | --- |
| npm committed lockfile | 22 affected packages: 13 high, 8 moderate, 1 low, 0 critical |
| Restored desktop NuGet graph on Mac | High alert for SQLitePCLRaw.lib.e_sqlite3 2.1.6 |
| GitHub-only alert state and Windows-specific restored graph | Not verified |

Npm affected packages: @babel/core, @cloudflare/vite-plugin,
@esbuild-kit/core-utils, @esbuild-kit/esm-loader, baseline-browser-mapping,
browserslist, drizzle-kit, esbuild, fast-uri, fflate, image-size, js-yaml,
miniflare, nanoid, postcss, react-server-dom-webpack, sharp, undici, vinext,
vite, wrangler, and ws. Several entries share a transitive advisory; 22 packages
does not mean 22 independent remotely exploitable defects.

Prioritize the React server-component and vinext/runtime dependency paths,
then Cloudflare/Vite and shared transitive packages. The audit proposes
vinext beta.9, React server components 19.3.0, Vite 8.2.2, and Cloudflare plugin
1.54.6 as candidate updates. They require compatibility review, especially
keeping React packages aligned. Do not run `npm audit fix --force`: its
Drizzle suggestion is a downgrade to 0.18.1, not a routine safe update.

The [SQLite advisory](https://github.com/advisories/GHSA-2m69-gcr7-jv3q)
lists no patched version of that package line. Moving to a maintained native
SQLite distribution needs a deliberate package migration and verification;
merely bumping Microsoft.Data.Sqlite may leave the vulnerable native library.
Dependencies were audited, not upgraded or dismissed, in this pass.

## Dependency automation and security reporting

`.github/dependabot.yml` covers weekly GitHub Actions, preview-site npm, and
all seven NuGet project directories, with limited PR queues and related
React/Cloudflare/Avalonia groups. Arduino CLI libraries are outside these
ecosystems and still need manual version review. Version-update scheduling does
not itself enable security-update PRs or clear existing alerts.

`SECURITY.md` defines support for current development and the latest stable
desktop/firmware releases, private reporting, and best-effort acknowledgment
within seven days/status within fourteen days. Enable private vulnerability
reporting in repository settings; this session has not verified that switch.
The GitHub reporting link replaces the earlier unusable “private message the
owner on GitHub” fallback.

## Suggested main-branch rules

- Require a pull request and resolution of review conversations.
- Block force pushes and deletion during normal work.
- While there is only one active maintainer, require zero approvals so the
  author is not locked out. With a second maintainer, require one approval and
  dismiss stale approvals when new commits arrive.
- Require the always-running **Repository hygiene** check after its first
  successful Actions run. Existing firmware/desktop workflows use path filters;
  requiring them globally can leave unrelated PRs waiting forever. Add a stable
  aggregate check before making platform-specific jobs universally required.
- Permit squash merges; linear history is optional. Do not require deployments,
  signed commits, or a merge queue for this initial small-maintainer workflow.
- Restrict `firmware-release` secrets to the reviewed release branch. Require
  approval for outside-contributor Actions runs and keep PR workflows read-only.
- Protect published `client-v*` and `firmware-v*` tags against modification or
  deletion once any planned history cleanup is complete.

These are recommendations; no remote rules or settings were changed.
GitHub reference: [protected branches](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches).

## Licensing and release corrections

`COPYRIGHT` explicitly names Hallzee Labs and preserves AGPL-3.0-or-later for
original code. Models use CC BY-SA 4.0; full official license texts are included.
Commercial use remains permitted under those terms. Third-party fonts/code
retain their own rights. The licensing page no longer claims every private
modification must be publicly released or that AGPL bans commercial products.

`THIRD-PARTY-NOTICES.md`, the generated 843-package npm/NuGet inventory, DM Sans
OFL notice, and available NuGet notices are included. Desktop and USB-tool
outputs copy these notices. Arduino/ESP-IDF component-level source and license
obligations still need completion; the inventory is not a compliance certificate.

Publication now targets the tested commit and links its corresponding source.
It attaches the teacher guide without overwriting the repository README, so
publication no longer needs to bypass main-branch protection. A duplicate YAML
`permissions` key introduced in the earlier hardening pass was also removed.

## Verification scope

This pass uses history/dependency scans and tooling/packaging checks, not a
physical release-acceptance run. Mac is sufficient for these local review tasks.
A Windows PC is required for actual Windows executable launch, native BLE
pairing/reconnect, and firmware transfer verification; those Windows behaviors
have not been reverified here. The private dashboard and native dependency
license review remain explicitly outstanding.
