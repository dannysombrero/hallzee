# Third-party components in Hallzee

Hallzee's AGPL license applies to original Hallzee code. Dependencies and fonts
retain their upstream copyrights and licenses. The model license does not
relicense software, fonts, or third-party artwork.

## Included fonts

DM Sans is copyright 2014 The DM Sans Project Authors
(https://github.com/googlefonts/dm-fonts), under the SIL Open Font License 1.1.
This covers the TTF files in `receiver/universal/Assets/Fonts/` and the converted
bitmap fonts in `fonts/`. The full copyright and license notice is in
`licenses/DM-Sans-OFL.txt`. Font conversion does not make the fonts AGPL.
Avalonia.Fonts.Inter also supplies Inter; preserve its upstream notices from
the restored package when distributing desktop builds.

## Desktop and USB tool dependencies

The resolved NuGet inventory includes Avalonia (MIT), Microsoft .NET components,
Microsoft.Data.Sqlite, SQLitePCLRaw, and native graphics/text libraries through
SkiaSharp and HarfBuzzSharp. Their licenses and upstream links are recorded in
`docs/dependency-inventory.json`. SQLitePCLRaw's wrapper and the SQLite engine
are separate components; do not assume a wrapper's license covers every native
dependency. Preserve upstream notices in self-contained runtime distributions.

## Firmware and bundled USB tools

Review the exact toolchain versions used by each release:

| Component | Upstream license/source location |
| --- | --- |
| Arduino-ESP32 3.3.11 | https://github.com/espressif/arduino-esp32/tree/3.3.11 — LGPL-2.1 and component-specific licenses |
| ESP-IDF and bundled SDK components | https://github.com/espressif/esp-idf — Apache-2.0 and component-specific notices; use the version bundled with the pinned Arduino core |
| Adafruit GFX, ST7735/ST7789, ILI9341, BusIO | Upstream `license.txt` files under https://github.com/adafruit; retain each library's notices |
| Keypad 3.1.1 | https://github.com/Chris--A/Keypad/blob/master/LICENSE — installed LICENSE contains GPL-3.0; source headers must also be reviewed before concluding the applicable grant |
| esptool | https://github.com/espressif/esptool — GPL-2.0-or-later; distributed USB tool |
| mklittlefs and littlefs | https://github.com/earlephilhower/mklittlefs and https://github.com/littlefs-project/littlefs — retain their separate licenses |

Arduino libraries are currently installed without explicit version pins. Record
their installed versions and retain their full notices/source when assembling a
release. The NuGet/npm inventory does not cover all native ESP32 SDK components.
For copyleft dependencies, a license file alone is not a substitute for the
applicable corresponding-source and installation-information obligations.

## Website dependencies

The npm inventory is derived from `preview-site/package-lock.json`, including
development/transitive dependencies. It is an inventory, not a claim that every
listed package is shipped to website visitors. Preserve upstream notices for
the subset actually redistributed. Entries without license metadata are marked
for review, not silently assigned Hallzee's license.

## Regenerating the inventory

After restoring the .NET projects, run from the checkout:

```sh
python3 scripts/generate-dependency-inventory.py --output docs/dependency-inventory.json
```

Use `--notices PATH` to collect available NuGet license/copyright files alongside
release output. Inspect unresolved entries and native toolchain notices before
redistribution. This inventory does not certify that all licensing obligations
have been satisfied.
