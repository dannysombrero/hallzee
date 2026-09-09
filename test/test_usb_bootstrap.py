#!/usr/bin/env python3
"""Exercise USB migration against a simulated flash chip and the real LittleFS tool."""
import argparse, hashlib, os, pathlib, struct, subprocess, tempfile
p=argparse.ArgumentParser(); p.add_argument('--dotnet',default='dotnet'); p.add_argument('--mklittlefs',required=True); a=p.parse_args()
root=pathlib.Path(__file__).resolve().parent.parent

def table(new):
    rows=[(1,2,0x9000,0x5000,'nvs'),(1,0,0xe000,0x2000,'otadata'),(0,0x10,0x10000,0x180000 if new else 0x140000,'app0'),(0,0x11,0x190000 if new else 0x150000,0x180000 if new else 0x140000,'app1'),(1,0x82,0x310000 if new else 0x290000,0xe0000 if new else 0x160000,'spiffs')]
    return b''.join(struct.pack('<HBBII16sI',0x50aa,t,s,o,n,label.encode(),0) for t,s,o,n,label in rows).ljust(0xc00,b'\xff')
with tempfile.TemporaryDirectory(prefix='hallzee-usb-test-') as temporary:
    base=pathlib.Path(temporary); files=base/'files'; files.mkdir()
    (files/'trips.csv').write_text('1,001234,2026-09-08,08:00:00,08:01:00,60,COMPLETED\n')
    (files/'hallzee-owner.bin').write_bytes(b'test-only-owner-record')
    oldfs=base/'old.bin'; subprocess.run([a.mklittlefs,'-c',str(files),'-b','4096','-p','256','-s',str(0x160000),str(oldfs)],check=True)
    original=bytearray(b'\xff'*0x400000); original[0x8000:0x8c00]=table(False)
    original[0x9000:0xe000]=bytes(range(256))*80; original[0x290000:0x3f0000]=oldfs.read_bytes()
    flash=base/'flash.bin'; flash.write_bytes(original)
    build=base/'build'; build.mkdir()
    (build/'test.ino.bin').write_bytes(b'\xe9'+b'\0'*1023)
    (build/'test.ino.bootloader.bin').write_bytes(b'\xe9'+b'\0'*255)
    (build/'test.ino.partitions.bin').write_bytes(table(True))
    (build/'boot_app0.bin').write_bytes(b'\xff'*0x2000)
    fake=base/'esptool'
    fake.write_text('''#!/usr/bin/env python3
import sys,os,pathlib
args=sys.argv[1:]; path=pathlib.Path(os.environ['HALLZEE_TEST_FLASH']); data=bytearray(path.read_bytes())
commands=['flash-id','read-flash','write-flash','verify-flash','run']
command=next(c for c in commands if c in args); rest=args[args.index(command)+1:]
if command=='flash-id': print('MAC: 01:23:45:67:89:ab'); print('Detected flash size: 4MB')
elif command=='read-flash': pathlib.Path(rest[2]).write_bytes(data[int(rest[0],0):int(rest[0],0)+int(rest[1],0)])
elif command in ('write-flash','verify-flash'):
 if rest[:1]==['--flash-size']: rest=rest[2:]
 for offset,file in zip(rest[::2],rest[1::2]):
  offset=int(offset,0); content=pathlib.Path(file).read_bytes()
  if command=='write-flash': data[offset:offset+len(content)]=content
  elif data[offset:offset+len(content)]!=content: raise SystemExit('Verify failed')
 if command=='write-flash': path.write_bytes(data)
'''); fake.chmod(0o755)
    env=dict(os.environ,HALLZEE_TEST_FLASH=str(flash))
    command=[a.dotnet,'run','--no-build','--project',str(root/'tools/FirmwareTool/FirmwareTool.csproj'),'--','usb','--esptool',str(fake),'--mklittlefs',a.mklittlefs,'--port','SIMULATED','--build',str(build),'--backup',str(base/'backups')]
    subprocess.run(command,env=env,check=True)
    updated=flash.read_bytes(); assert updated[0x9000:0xe000]==original[0x9000:0xe000]
    newfs=base/'new.bin'; newfs.write_bytes(updated[0x310000:0x3f0000]); extracted=base/'restored'; extracted.mkdir()
    subprocess.run([a.mklittlefs,'-u',str(extracted),'-b','4096','-p','256','-s',str(0xe0000),str(newfs)],check=True)
    assert {f.name:f.read_bytes() for f in files.iterdir()}=={f.name:f.read_bytes() for f in extracted.iterdir()}
    backups=list((base/'backups').glob('*/original-flash.bin')); assert backups[0].read_bytes()==original
    # An unfamiliar partition layout must be rejected before write-flash.
    invalid=bytearray(original); invalid[0x8000:0x8002]=b'\0\0'; flash.write_bytes(invalid)
    failed=subprocess.run(command,env=env)
    assert failed.returncode!=0 and flash.read_bytes()==invalid
    recovery=[a.dotnet,'run','--no-build','--project',str(root/'tools/FirmwareTool/FirmwareTool.csproj'),'--','recover','--esptool',str(fake),'--port','SIMULATED','--backup',str(backups[0].parent)]
    subprocess.run(recovery,env=env,check=True); assert flash.read_bytes()==original
    # Existing records that cannot fit the smaller data partition must never be dropped.
    (files/'large.log').write_bytes(b'x'*1_000_000)
    fullfs=base/'full.bin'
    subprocess.run([a.mklittlefs,'-c',str(files),'-b','4096','-p','256','-s',str(0x160000),str(fullfs)],check=True)
    full=bytearray(original); full[0x290000:0x3f0000]=fullfs.read_bytes(); flash.write_bytes(full)
    failed=subprocess.run(command,env=env)
    assert failed.returncode!=0 and flash.read_bytes()==full
    print('USB migration/recovery preserved files and NVS; unknown and oversized layouts never changed flash.')
