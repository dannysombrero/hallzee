# Developer documentation

This is the technical area for Hallzee contributors and maintainers. Teachers
should start with the [teacher guide](Getting-Started-Users).

## Start developing

- [Getting started for developers](Getting-Started-Developers)
- [Testing and installation](Testing-and-Installation)
- [Testing reference](Testing-Reference)
- [Contributing on GitHub](https://github.com/dannysombrero/hallzee/blob/main/CONTRIBUTING.md)

## Project layout

| Path | Purpose |
| --- | --- |
| `firmware/terminal/` | ESP32 terminal sketch, C++ components, display fonts, Wokwi files, and Arduino build inputs |
| `receiver/universal/` | Shared Avalonia desktop app for Windows and macOS |
| `receiver/windows/` | Windows platform integration and shared core services |
| `receiver/MacBLEAgent/` | Native macOS Bluetooth helper |
| `models/` | Enclosure and mounting models |
| `preview-site/` | Public project website |
| `scripts/` | Setup, validation, packaging, and release automation |
| `test/` | Firmware tests and repository safeguard tests |
| `tools/FirmwareTool/` | Firmware packaging, signing, migration, and USB update tool |
| `docs/` | Source documentation mirrored into this Wiki |

The terminal source begins at
[`firmware/terminal/bathroom-signin.ino`](https://github.com/dannysombrero/hallzee/blob/main/firmware/terminal/bathroom-signin.ino).
Use the repository flashing scripts because they stage the sketch in the
folder structure required by Arduino.

## Hardware and firmware

- [Firmware updates](Firmware-Updates)
- [3D models and fabrication](3D-Models)
- [Kiosk settings](Kiosk-Settings)
- [Bluetooth protocol](Bluetooth-Protocol)
- [Paused touch-screen experiment](Touch-Test)

## Architecture and design

- [System architecture](Architecture)
- [Desktop UI architecture](Client-UI-Architecture)
- [Product requirements](Product-Requirements)
- [Roster import and enrichment](Design-Roster-Import)
- [Live active-pass protocol](Design-Active-Pass-Protocol)
- [Policies and bell schedules](Design-Policy-And-Schedules)
- [Background auto-sync](Design-Auto-Sync)
- [Terminal identity and claim](Design-Terminal-Identity-And-Exclusive-Claim)
- [Terminal reassignment](Design-Terminal-Reassignment)
- [Bluetooth firmware updates](Design-Bluetooth-Firmware-Updates)
- [Development Chromebook and web client implementation](Design-Chromebook-Web-Client)

## Releases, security, and maintenance

- [Release readiness](Release-Readiness)
- [Publishing releases](Releasing)
- [Release licensing](Release-Licensing)
- [CI and repository security](CI-and-Security)
- [Open-source readiness](Open-Source-Readiness)
- [Desktop dependency updates](Dependency-Updates-Desktop)
- [Website dependency updates](Dependency-Updates-Web)

- [Chrome teacher guide](Chromebook-Guide.md), [IT operations](Web-Client-IT-Guide.md), and [acceptance evidence](Web-Client-Acceptance.md).
