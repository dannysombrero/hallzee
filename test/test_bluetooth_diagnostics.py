"""Verify that USB troubleshooting never exposes ordinary serial data."""
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parent.parent
MONITOR = ROOT / "scripts/monitor-terminal-macos.sh"


class BluetoothDiagnosticsTests(unittest.TestCase):
    def test_only_exact_allowlisted_events_are_forwarded(self):
        allowed = ["BLE_DIAG READY v1", "BLE_DIAG CONNECTED",
                   "BLE_DIAG SECURITY_REQUEST", "BLE_DIAG AUTH_OK",
                   "BLE_DIAG AUTH_FAILED 0x05", "BLE_DIAG DISCONNECTED 0x13",
                   "BLE_DIAG DISCONNECTED 0x100",
                   "BLE_DIAG READ_REQUEST", "BLE_DIAG WRITE_REQUEST"]
        # Synthetic sensitive-looking data; never use a real log as a fixture.
        rejected = ["CHECK OUT: SYNTHETIC-STUDENT", "BLE_DIAG AUTH_OK extra-data",
                    "prefix BLE_DIAG AUTH_OK", "BLE_DIAG AUTH_FAILED 0x1234",
                    "BLE_DIAG DISCONNECTED 0x12345",
                    "BLE_DIAG PASSKEY synthetic-code", "BLE_DIAG KEY synthetic-key",
                    "BLE_DIAG ADDRESS synthetic-address", "BLE_DIAG AUTH_OK\x1b[31m"]
        result = subprocess.run(["bash", str(MONITOR), "--filter-stdin"],
                                input="\r\n".join(allowed + rejected) + "\r\n",
                                capture_output=True, text=True, check=True)
        self.assertEqual(result.stdout.splitlines(), allowed)
        self.assertEqual(result.stderr, "")

    def test_monitor_errors_are_filtered_and_reported_without_payloads(self):
        with tempfile.TemporaryDirectory() as directory:
            cli = Path(directory) / "fake-cli"
            cli.write_text("#!/bin/sh\nprintf '%s\\n' 'BLE_DIAG AUTH_FAILED 0x05'\n"
                           "printf '%s\\n' 'synthetic private error payload' >&2\nexit 7\n")
            cli.chmod(0o755)
            result = subprocess.run(["bash", str(MONITOR), "/dev/synthetic-terminal"],
                                    env=dict(os.environ, HALLZEE_ARDUINO_CLI=str(cli)),
                                    capture_output=True, text=True)
            self.assertEqual(result.returncode, 7)
            self.assertIn("BLE_DIAG AUTH_FAILED 0x05", result.stdout)
            self.assertIn("USB monitor stopped", result.stdout)
            self.assertNotIn("synthetic private", result.stdout + result.stderr)

    def test_compile_only_cannot_open_a_hardware_monitor(self):
        result = subprocess.run(["bash", str(ROOT / "scripts/flash-terminal-macos.sh"),
                                 "--fast", "--compile-only", "--monitor"],
                                capture_output=True, text=True)
        self.assertEqual(result.returncode, 1)
        self.assertIn("--monitor requires a USB flash", result.stdout)

    def test_keyboard_input_and_eof_do_not_reach_the_serial_monitor(self):
        with tempfile.TemporaryDirectory() as directory:
            cli = Path(directory) / "fake-cli"
            cli.write_text(f"#!{sys.executable}\nimport select, sys\n"
                           "ready = select.select([sys.stdin], [], [], 0.1)[0]\n"
                           "print('BLE_DIAG AUTH_OK')\n"
                           "sys.exit(9 if ready else 0)\n")
            cli.chmod(0o755)
            for keyboard in ("", "synthetic serial command\n"):
                with self.subTest(keyboard=bool(keyboard)):
                    result = subprocess.run(["bash", str(MONITOR), "/dev/synthetic-terminal"],
                                            env=dict(os.environ, HALLZEE_ARDUINO_CLI=str(cli)),
                                            input=keyboard, capture_output=True, text=True, timeout=5)
                    self.assertEqual(result.returncode, 0)
                    self.assertIn("BLE_DIAG AUTH_OK", result.stdout)


if __name__ == "__main__":
    unittest.main()
