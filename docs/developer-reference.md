# Hallzee developer reference

This page is the technical map for contributors and maintainers. Start with
[Getting started for developers](getting-started-developers.md) for setup and
commands. Teachers should use the [teacher guide](getting-started-users.md).

## Project layout

| Path | Purpose |
| --- | --- |
| `firmware/terminal/` | ESP32 terminal sketch, C++ components, display fonts, Wokwi files, and Arduino build inputs |
| `receiver/universal/` | Shared Avalonia desktop app for Windows and macOS |
| `receiver/windows/` | Windows platform integration and shared core services |
| `receiver/MacBLEAgent/` | Native macOS Bluetooth helper |
| `models/` | Enclosure and mounting models, licensed under CC BY-SA 4.0 |
| `preview-site/` | Public project website |
| `scripts/` | Setup, validation, packaging, and release automation |
| `test/` | Native firmware tests and repository safeguard tests |
| `tools/FirmwareTool/` | Firmware packaging, signing, migration, and USB update tool |
| `docs/` | User, contributor, architecture, design, and release documentation |
| `wiki/` | Repository-owned copies of the GitHub Wiki pages |

The terminal's composition root is
[`firmware/terminal/bathroom-signin.ino`](../firmware/terminal/bathroom-signin.ino).
The Arduino scripts stage that sketch in an Arduino-compatible temporary
folder, so contributors should run the documented scripts instead of opening
the repository root as a sketch.

## Build, test, and hardware

- [Testing and installation](testing-and-installation.md) is the main
  contributor guide for clean-machine setup, wiring, flashing, desktop builds,
  and required verification.
- [Testing reference](testing-reference.md) contains the full test matrix,
  emulators, troubleshooting, and hardware checks.
- [Firmware updates](firmware-updates.md) explains USB preparation, signed
  Bluetooth updates, rollback behavior, and firmware releases.
- [3D models](../models/) contains the enclosure and fabrication files; the
  [Wiki model guide](https://github.com/dannysombrero/hallzee/wiki/3D-Models)
  explains the current and historical sets.
- [CI and repository security](ci-and-security.md) explains required checks,
  dependency review, secret scanning, and release protections.

## Architecture and protocols

- [System architecture](architecture.md)
- [Desktop UI architecture](client-ui-architecture.md)
- [Bluetooth protocol](bluetooth-protocol.md)
- [Product requirements](product-requirements.md)

Feature-level designs live in [`docs/design/`](design/). They describe the
roster, live pass protocol, policies and schedules, automatic sync, terminal
identity and reassignment, and Bluetooth firmware updates.

## Releases and maintenance

- [Release readiness](release-readiness.md)
- [Publishing releases](releasing.md)
- [Release licensing](release-licensing.md)
- [Open-source readiness](open-source-readiness.md)
- [Desktop dependency updates](dependency-updates-desktop.md)
- [Website dependency updates](dependency-updates-web.md)

Keep a matching page in `wiki/` whenever documentation covered by the project
policy changes. GitHub Actions validates the repository copies; publishing the
Wiki remains a separate repository operation.
