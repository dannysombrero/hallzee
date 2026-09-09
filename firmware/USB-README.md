# One-time Hallzee OTA USB setup

Extract the entire matching platform ZIP. Connect the powered terminal by USB
with no active passes. Open Terminal/PowerShell in this extracted folder. The
bundle includes its runtime, esptool, and LittleFS tools; no compiler, .NET,
Arduino CLI, or Git installation is required.

Mac (Apple silicon):

```sh
bash install-usb-macos.sh esp32-st7735-r1 /dev/cu.usbserial-XXXX
```

Windows x64:

```powershell
powershell -ExecutionPolicy Bypass -File .\install-usb-windows.ps1 -Variant esp32-st7735-r1 -Port COM5
```

Use `esp32-ili9341-r1` for the larger display; use `r3` if mounted upside down.
Only the initial USB setup requires choosing the physical display/rotation.
Future firmware packages automatically select that variant.

The installer reads and verifies a 4 MB flash backup, migrates LittleFS into the
new layout, and verifies restored files and NVS. It stops before writing when
existing data cannot be safely migrated. Keep the private `backups/` directory:
it contains pairing credentials and trip records. Never attach it to a release
or public issue. A failure after writing may require another USB setup attempt;
preserve the original backup first. Do not erase the entire flash.

Once setup succeeds, reconnect in the desktop client and open Settings → Device.
Choose a newer `.hallzee-fw` package using Install firmware from file.


If initial setup was interrupted after writing, restore a verified backup to the
same physical board (its hardware MAC must match), then retry setup:

```sh
bash recover-usb-macos.sh backups/<backup-folder> /dev/cu.usbserial-XXXX
```

```powershell
powershell -ExecutionPolicy Bypass -File .\recover-usb-windows.ps1 -BackupFolder backups/<backup-folder> -Port COM5
```

Recovery returns firmware and terminal data to the snapshot's timestamp. Sync
newer trips first if the terminal is still usable. The original backup remains
on the computer after recovery.
