"""Exercise the Mac flash wrapper with synthetic tools and no serial hardware."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parent.parent


class FlashOptionsTests(unittest.TestCase):
    def test_invalid_baud_fails_before_setup_or_hardware(self):
        for options in (["--baud", "0"], ["--baud=-1"], ["--baud=invalid"], ["--baud"]):
            with self.subTest(options=options):
                result = subprocess.run(["bash", str(ROOT / "scripts/flash-terminal-macos.sh"), *options],
                                        capture_output=True, text=True, timeout=5)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("--baud", result.stdout + result.stderr)
                self.assertNotIn("Building Hallzee", result.stdout)

    def test_selected_baud_reaches_fast_and_regular_usb_tools(self):
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory)
            scripts = base / "scripts"
            scripts.mkdir()
            shutil.copyfile(ROOT / "scripts/flash-terminal-macos.sh", scripts / "flash-terminal-macos.sh")
            source = base / "firmware/terminal"
            source.mkdir(parents=True)
            (source / "synthetic.ino").write_text("// Synthetic sketch; never compiled.\n")
            (source / "partitions.csv").write_text("# Synthetic partition table\n")
            (source / "arduino-libraries.txt").write_text("SyntheticLibrary@1.0.0\n")
            arduino_data = base / "arduino-data"
            partitions = arduino_data / "packages/esp32/hardware/esp32/3.3.11/tools/partitions"
            partitions.mkdir(parents=True)
            (partitions / "boot_app0.bin").write_bytes(b"synthetic-boot-selector")
            cli = base / ".tools/arduino-cli/bin/arduino-cli"
            cli.parent.mkdir(parents=True)
            cli.write_text("#!/usr/bin/env python3\nimport pathlib, sys\n"
                           "if '--build-path' in sys.argv:\n"
                           " pathlib.Path(sys.argv[sys.argv.index('--build-path')+1]).mkdir()\n")
            cli.chmod(0o755)
            fake_bin = base / "bin"
            fake_bin.mkdir()
            dotnet = fake_bin / "dotnet"
            dotnet.write_text("#!/usr/bin/env python3\nimport json, os, pathlib, sys\n"
                              "if '--list-sdks' in sys.argv: print('8.0.999')\n"
                              "else: pathlib.Path(os.environ['HALLZEE_TEST_ARGUMENTS']).write_text(json.dumps(sys.argv[1:]))\n")
            dotnet.chmod(0o755)
            log = base / "arguments.json"
            env = dict(os.environ, PATH=str(fake_bin) + os.pathsep + os.environ["PATH"],
                       ARDUINO_DIRECTORIES_DATA=str(arduino_data), HALLZEE_TEST_ARGUMENTS=str(log))
            for options, operation, baud in ((["--fast"], "usb-fast", "460800"),
                                             (["--fast", "--baud", "115200"], "usb-fast", "115200"),
                                             (["--baud=115200"], "usb", "115200")):
                with self.subTest(options=options):
                    log.unlink(missing_ok=True)
                    result = subprocess.run(["bash", str(scripts / "flash-terminal-macos.sh"),
                                             *options, "/dev/synthetic-terminal"],
                                            env=env, capture_output=True, text=True, timeout=10)
                    self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                    self.assertTrue(log.exists(), result.stdout + result.stderr)
                    arguments = json.loads(log.read_text())
                    self.assertEqual(arguments[arguments.index("--") + 1], operation)
                    self.assertEqual(arguments[arguments.index("--baud") + 1], baud)


if __name__ == "__main__":
    unittest.main()
