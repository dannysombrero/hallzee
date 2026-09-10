# Security policy

Hallzee is maintained by Hallzee Labs. This policy covers the desktop clients,
terminal firmware, release tools, and the website code in this repository.

## Supported versions

| Version | Security maintenance |
| --- | --- |
| Current development branch, before v1.0 | Best effort; update to current source |
| Latest stable desktop release, once published | Supported |
| Latest stable firmware release, once published | Supported |
| Superseded releases, old preview builds, and third-party forks | No separate backports; upgrade |

Desktop and firmware versions advance independently. Supported downloads appear
under this repository's Releases page. This policy does not promise a release
date or a fixed support lifetime.

## Reporting a vulnerability

Use [Report a vulnerability](https://github.com/dannysombrero/hallzee/security/advisories/new)
to send a private report through GitHub. The maintainer must enable **Private
vulnerability reporting** in repository security settings for this link to work.
If the form is unavailable, open an issue asking only for a private reporting
channel; do not include vulnerability details, credentials, or student data.

Include the affected version and platform, reproduction steps using synthetic
data, expected and actual behavior, and the potential impact. Redact tokens,
pairing credentials, names, student IDs, and private signing keys from logs.
Do not test against a school system without its owner's permission.

We aim to acknowledge reports within seven days and provide a status update
within fourteen days, then at least every fourteen days while actively working
on an accepted report. These are best-effort goals for a small project, not an
SLA. If accepted, we coordinate a fix, release instructions, and disclosure with
the reporter. If declined or out of scope, we explain why. Credit is optional;
no paid bug bounty is currently offered.

## Signing keys and classroom data

The official private firmware signing key belongs in the `firmware-release`
GitHub environment and a secure maintainer backup, never in commits or reports.
`firmware/release-public-key.pem` is intentionally public verification material.
Official update authentication does not prevent users building their own
firmware and installing it over USB.

Use synthetic classroom data in issues, pull requests, screenshots, and fixtures.
Dependabot alerts are reviewed separately from functional test results; a green
build does not establish that dependencies have no known vulnerabilities.
