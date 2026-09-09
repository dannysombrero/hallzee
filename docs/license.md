# Hallzee Licensing and Terms

Hallzee is an open-source, open-hardware project developed for schools and educators.

## Overview

The repository is dual-licensed by media type to provide strong copyleft protection for code while using an established open hardware standard for physical designs:

| Component | Scope | License | Copyright |
| --- | --- | --- | --- |
| **Software & Firmware** | ESP32 C++ firmware, Avalonia desktop receiver (.NET/C#), tools, and test suites | [GNU Affero General Public License v3.0 (AGPL-3.0)](../LICENSE) | Copyright © 2026 Hallzee Labs |
| **Hardware & 3D Models** | Enclosure models, mounting plates, CAD files, and `.3mf`/`.stl`/`.step` assets in `models/` | [Creative Commons Attribution-ShareAlike 4.0 International (CC BY-SA 4.0)](../LICENSE) | Copyright © 2026 Hallzee Labs |

---

## Software & Firmware: AGPL-3.0

The software and firmware components are licensed under the **GNU Affero General Public License Version 3 (AGPL-3.0)**.

### For Teachers and Schools
- **Free to Use:** Individual teachers, schools, and districts may freely download, install, self-host, and use Hallzee across their classrooms without license fees.
- **Customization:** You are free to modify the firmware or desktop application to meet your classroom's needs.

### For Commercial Entities and Competitors
- **Copyleft / Reciprocal Sharing:** Any modifications, derivatives, or distributions of Hallzee software must also be licensed under AGPL-3.0.
- **No SaaS Loophole (Section 13):** If you run modified Hallzee software on a network server or cloud service that users interact with remotely, you must make the complete corresponding source code available to those users at no charge under AGPL-3.0.
- **No Closed-Source Forks:** Third parties cannot bundle Hallzee into a proprietary closed-source commercial product or kiosk without releasing the full source code under AGPL-3.0.

### Hardware Freedom & Anti-Tivoization (Section 6)
In accordance with AGPL-3.0 Section 6 (Installation Information):
- Hallzee firmware is designed to run on open hardware (standard ESP32 microcontrollers).
- Hardware devices are not locked to proprietary vendor signing keys via hardware eFuses.
- End users retain the ability to flash custom and modified firmware to physical terminals over a standard USB connection using open-source tools such as `esptool.py`.

---

## Hardware & 3D Models: CC BY-SA 4.0

All physical designs and CAD/print files located in `models/` are licensed under the **Creative Commons Attribution-ShareAlike 4.0 International License (CC BY-SA 4.0)**.

- **You are free to:**
  - **Share:** Copy, redistribute, and 3D print the models in any medium or format.
  - **Adapt:** Remix, transform, and build upon the designs for your own hardware enclosures.
- **Under the following terms:**
  - **Attribution:** You must give appropriate credit to **Hallzee Labs**, provide a link to the license, and indicate if changes were made.
  - **ShareAlike:** If you remix, transform, or build upon the hardware models, you must distribute your contributions under the same license as the original (CC BY-SA 4.0).

---

## Inquiries and Commercial Licensing

For questions regarding compliance, custom licensing, enterprise district agreements, or commercial hardware partnerships, contact **Hallzee Labs**.
