#!/usr/bin/env python3
"""Check fast USB write boundaries/failures using a simulated ESP32; no hardware."""
import argparse
import json
import os
from pathlib import Path
import struct
import subprocess
import tempfile

parser = argparse.ArgumentParser()
parser.add_argument('--dotnet', default='dotnet')
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent

with tempfile.TemporaryDirectory(prefix='hallzee-usb-fast-test-') as temporary:
    base = Path(temporary)
    build = base / 'build'
    build.mkdir()
    rows = [(1, 2, 0x9000, 0x5000), (1, 0, 0xe000, 0x2000),
            (0, 0x10, 0x10000, 0x180000), (0, 0x11, 0x190000, 0x180000),
            (1, 0x82, 0x310000, 0xe0000)]
    table = b''.join(struct.pack('<HBBII16sI', 0x50aa, t, s, o, n, b'', 0)
                     for t, s, o, n in rows).ljust(0xc00, b'\xff')
    app = b'\xe9' + b'new-application' * 1000
    boot = b'\xe9' + b'bootloader' * 100
    selector = b'\xff' * 0x2000
    (build / 'test.ino.bin').write_bytes(app)
    (build / 'test.ino.bootloader.bin').write_bytes(boot)
    (build / 'test.ino.partitions.bin').write_bytes(table)
    (build / 'boot_app0.bin').write_bytes(selector)
    original = bytearray(bytes(range(256)) * (0x400000 // 256))
    original[0x1000:0x1000 + len(boot)] = boot
    original[0x8000:0x8c00] = table
    # Model an existing app1 selection; fast USB must replace it only after app0 verifies.
    original[0xe000:0x10000] = b'\x02' * 0x2000
    flash = base / 'flash.bin'
    log = base / 'commands.jsonl'
    fake = base / 'esptool'
    fake.write_text('''#!/usr/bin/env python3
import json, os, pathlib, sys
args = sys.argv[1:]
path = pathlib.Path(os.environ['HALLZEE_TEST_FLASH'])
data = bytearray(path.read_bytes())
with open(os.environ['HALLZEE_TEST_LOG'], 'a') as log: log.write(json.dumps(args) + '\\n')
assert args[args.index('--chip') + 1] == 'esp32'
command = next(c for c in ['verify-flash', 'write-flash', 'run'] if c in args)
if command == 'run': raise SystemExit(0)
assert args[:2] == ['--after', 'no-reset']
rest = args[args.index(command) + 1:]
for offset, file in zip(rest[::2], rest[1::2]):
 offset = int(offset, 0); content = pathlib.Path(file).read_bytes()
 if command == 'write-flash':
  # Real writes erase entire 4 KiB sectors, not only the supplied bytes.
  start = offset // 4096 * 4096; end = (offset + len(content) + 4095) // 4096 * 4096
  data[start:end] = b'\\xff' * (end-start)
  data[offset:offset+len(content)] = content
  path.write_bytes(data)
 elif os.environ.get('HALLZEE_FAIL_VERIFY') == str(offset) or data[offset:offset+len(content)] != content:
  raise SystemExit('Simulated verification failure')
''')
    fake.chmod(0o755)
    env = dict(os.environ, HALLZEE_TEST_FLASH=str(flash), HALLZEE_TEST_LOG=str(log))
    command = [args.dotnet, 'run', '--no-build', '--project',
               str(root / 'tools/FirmwareTool/FirmwareTool.csproj'), '--', 'usb-fast',
               '--esptool', str(fake), '--port', 'SIMULATED', '--build', str(build)]

    def run(initial, succeeds=True, **extra):
        flash.write_bytes(initial)
        log.write_text('')
        result = subprocess.run(command, env=dict(env, **extra), capture_output=True, text=True)
        assert (result.returncode == 0) == succeeds, result.stdout + result.stderr
        return flash.read_bytes(), [json.loads(line) for line in log.read_text().splitlines()]

    updated, calls = run(original)
    app_end = (0x10000 + len(app) + 4095) // 4096 * 4096
    assert updated[:0xe000] == original[:0xe000]  # bootloader, table, NVS
    assert updated[0xe000:0x10000] == selector
    assert updated[0x10000:0x10000 + len(app)] == app
    assert updated[app_end:] == original[app_end:]  # app1, LittleFS, coredump
    writes = [c[c.index('write-flash') + 1] for c in calls if 'write-flash' in c]
    assert writes == ['0x10000', '0xe000']
    assert calls[-1][-1] == 'run'
    assert next(i for i, c in enumerate(calls) if 'verify-flash' in c and c[-2] == '0x10000') < next(i for i, c in enumerate(calls) if 'write-flash' in c and c[-2] == '0xe000')

    # Repeat when already on app0, and fill the slot to its exact limit without
    # erasing any part of app1 or the filesystem.
    run(updated)
    (build / 'test.ino.bin').write_bytes(b'\xe9' * 0x180000)
    full, _ = run(original)
    assert full[0x190000:] == original[0x190000:]
    (build / 'test.ino.bin').write_bytes(app)

    for offset in (0x1000, 0x8000):
        invalid = bytearray(original)
        invalid[offset] ^= 0xff
        unchanged, calls = run(invalid, succeeds=False)
        assert unchanged == invalid and not any('write-flash' in c for c in calls)
    unchanged, calls = run(b'\xff' * 0x400000, succeeds=False)
    assert unchanged == b'\xff' * 0x400000 and not any('write-flash' in c for c in calls)

    failed, calls = run(original, succeeds=False, HALLZEE_FAIL_VERIFY=str(0x10000))
    assert failed[0xe000:0x10000] == original[0xe000:0x10000]
    assert not any('run' in c for c in calls)
    failed, calls = run(original, succeeds=False, HALLZEE_FAIL_VERIFY=str(0xe000))
    assert not any('run' in c for c in calls)

    for name, content in [('test.ino.bin', b'x' * (0x180000 + 1)),
                          ('test.ino.bin', b''), ('boot_app0.bin', b'x'),
                          ('test.ino.partitions.bin', b'\xff' * 0xc00)]:
        path = build / name
        saved = path.read_bytes()
        path.write_bytes(content)
        unchanged, calls = run(original, succeeds=False)
        assert unchanged == original and not calls
        path.write_bytes(saved)
    print('Fast USB: only app0/otadata written; mismatches and invalid builds rejected; verification failures never reboot.')
