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
Avalonia.Fonts.Inter also supplies Inter 3.019 (font metadata git-0a5106e0b),
copyright 2020 The Inter Project Authors (https://github.com/rsms/inter), under
SIL OFL 1.1. Its separate font license is in `licenses/Inter-OFL.txt`; the MIT
declaration on the NuGet wrapper does not replace the font's OFL terms.

## Desktop and USB tool dependencies

The resolved NuGet inventory includes Avalonia (MIT), Microsoft .NET components,
Microsoft.Data.Sqlite, SQLitePCLRaw, and native graphics/text libraries through
SkiaSharp and HarfBuzzSharp. Their licenses and upstream links are recorded in
the release's `licenses/dependency-inventory.json`; `docs/dependency-inventory.json`
is the repository-wide reference snapshot. SQLitePCLRaw's wrapper and the SQLite engine
are separate components; do not assume a wrapper's license covers every native
dependency. The current native SQLite package, SourceGear.sqlite3, distributes
the public-domain SQLite engine and its own upstream public-domain notice.
SQLitePCLRaw has Apache-2.0 terms. Release builds collect exact NuGet notices,
copyright metadata, self-contained .NET runtime notices, and the Mac helper's
resolved framework-pack license. Packages declaring MIT/Apache-2.0 without a
physical notice receive those full terms plus their original package metadata.

## Firmware and bundled USB tools

Review the exact toolchain versions used by each release:

| Component | Upstream license/source location |
| --- | --- |
| Arduino-ESP32 3.3.11 | https://github.com/espressif/arduino-esp32/tree/3.3.11 — LGPL-2.1 and component-specific licenses |
| ESP-IDF and bundled SDK components | https://github.com/espressif/esp-idf — Apache-2.0 and component-specific notices; use the version bundled with the pinned Arduino core |
| Adafruit GFX, ST7735/ST7789, ILI9341, BusIO | Upstream `license.txt` files under https://github.com/adafruit; retain each library's notices |
| Keypad 3.1.1 | https://github.com/Chris--A/Keypad — source headers grant LGPL-2.1, root LICENSE contains GPL-3.0; preserve both texts and complete source (see the decision below) |
| esptool | https://github.com/espressif/esptool — GPL-2.0-or-later; distributed USB tool |
| mklittlefs and littlefs | https://github.com/earlephilhower/mklittlefs and https://github.com/littlefs-project/littlefs — retain their separate licenses |

All declared Arduino libraries are pinned in `firmware/arduino-libraries.txt`.
The release builder verifies them and supplies their source, exact Arduino/IDF
source with recursive submodules, linked managed-component sources, toolchain
notices, SDK configuration, and the esptool/mklittlefs sources. Download these
source archives beside the firmware/USB files, together with
`Hallzee-Firmware-Licenses.zip`. Keypad's own differing notices remain intact;
Hallzee provides the source/rebuild information instead of claiming a permissive
or newly clarified upstream grant. See [Release licenses and source](docs/release-licensing.md)
for evidence, USB installation of modified firmware, and Espressif radio source
availability. The rebuilt esptool executable uses a hash-locked Python graph;
each USB platform's `Hallzee-USB-Python-Sources-<rid>.zip` supplies its dependency
sources and notices separately from the downloadable application.

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

Release workflows use explicit `--assets`, `--rid`, and `--no-npm` options so a
desktop/USB build inventories its own restored target. `--notices PATH` collects
NuGet license/copyright files; `--framework-references` adds the Mac helper's
native workload runtime pack. Missing package metadata or required license text
fails release collection. This inventory does not certify legal compatibility
of every third-party component.
