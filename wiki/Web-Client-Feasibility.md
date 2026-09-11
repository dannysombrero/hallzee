# Web client feasibility

Recorded 2026-09-11. The user has Chrome on a Mac, but no district-managed
Chromebook available. Physical terminal acceptance was deferred; software work
continued under that explicit instruction. No deployment is authorized by this
record.

| Capability | Evidence | Status |
| --- | --- | --- |
| Mac Chrome local dashboard | Opened in the selected regular Chrome profile; empty real classroom rendered; Ready offline displayed | Observed |
| IndexedDB durable transactions and CryptoKey structured clone | Production Chromium integration test stores a nonextractable key and authenticates after offline reload | Automated pass |
| Browser GATT software adapter | Simulated GATT with actual 20-byte writes, fragmented notifications, full claim/auth and durable ACK | Automated pass; hardware excluded |
| Offline shell and controlled updates | Actual worker A/B install, interrupted download, explicit activation and data/key survival | Automated pass |
| Managed Chromebook secure pairing, district policies, persistence/eviction, sleep and projection | No managed device available | Not run |
| Windows Chrome BLE and installed PWA | Requires Windows BLE PC and ESP32 | Not run |
| Mac Chrome physical BLE, installed PWA and projector | Requires ESP32 and display testing | Not run |

The implementation therefore remains a development client. M0's managed
hardware gate has not passed. Do not advertise Chromebook/Windows browser support
from software-only evidence. Node is pinned to 22.23.2 in `web-client/toolchain.json`;
all frontend/test packages are pinned in `package-lock.json`. CI installs its
matching Playwright Chromium build. The tested local computer is macOS arm64;
clean-machine Mac and Windows bootstrap images have not been tested.

See [acceptance evidence](Web-Client-Acceptance.md) and the
[design](Design-Chromebook-Web-Client.md) for the exact remaining hardware
sequence. A Mac is sufficient for shared software tests; a Windows BLE PC is
required for Chrome's Windows OS pairing, encrypted GATT, reconnect/sleep and PWA
behavior. Windows behavior remains unverified.
