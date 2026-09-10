#!/usr/bin/env python3
"""CI/developer release builder. Requires the pinned Arduino core and .NET 8."""
import argparse, pathlib, re, shutil, subprocess, sys, tempfile, zipfile
p=argparse.ArgumentParser()
for arg in ('version','build','output','key','notes','arduino-data'): p.add_argument('--'+arg,required=True)
p.add_argument('--cli',default='arduino-cli'); p.add_argument('--dotnet',default='dotnet')
p.add_argument('--source-cache', help='Optional reusable checkout cache for pinned third-party source')
a=p.parse_args(); root=pathlib.Path(__file__).resolve().parent.parent
a.dotnet=shutil.which(a.dotnet) or str(root/'.tools/dotnet'/('dotnet.exe' if __import__('os').name=='nt' else 'dotnet'))
if not re.fullmatch(r'(0|[1-9][0-9]{0,2})\.(0|[1-9][0-9]{0,2})\.(0|[1-9][0-9]{0,2})',a.version):
 p.error(f'Invalid firmware version {a.version!r}. Use major.minor.patch, such as 1.0.0, with each number 0-999 and no leading zeros. The release workflow normalizes v1.0 and 1.0 automatically.')
if not re.fullmatch(r'[a-zA-Z0-9._-]{1,40}',a.build):
 p.error(f'Invalid build ID {a.build!r}. Use 1-40 ASCII letters, digits, dots, underscores, or hyphens (a Git commit SHA is valid).')
out=pathlib.Path(a.output).resolve(); out.mkdir(parents=True,exist_ok=False)
data=pathlib.Path(a.arduino_data)
sdk=(data/'packages/esp32/tools/esp32-libs/3.3.11/sdkconfig').read_text()
if 'CONFIG_BOOTLOADER_APP_ROLLBACK_ENABLE=y' not in sdk or 'CONFIG_APP_ROLLBACK_ENABLE=y' not in sdk: raise RuntimeError('Pinned rollback-enabled core required')
with tempfile.TemporaryDirectory(prefix='hallzee-release-') as temporary:
 maps=[]
 stage=pathlib.Path(temporary)/'bathroom-signin'; stage.mkdir()
 for pattern in ('*.h','*.cpp','*.ino'):
  for source in root.glob(pattern): shutil.copy2(source,stage/source.name)
 shutil.copytree(root/'fonts',stage/'fonts'); shutil.copy2(root/'firmware/partitions.csv',stage/'partitions.csv')
 header=(stage/'FirmwareRelease.h').read_text()
 header=re.sub(r'#define HALLZEE_FW_VERSION .*',f'#define HALLZEE_FW_VERSION "{a.version}"',header)
 header=re.sub(r'#define HALLZEE_FW_BUILD .*',f'#define HALLZEE_FW_BUILD "{a.build}"',header)
 (stage/'FirmwareRelease.h').write_text(header)
 for variant in ['esp32-st7735-r1']+[f'esp32-ili9341-r{r}' for r in range(4)]:
  build=pathlib.Path(temporary)/variant
  command=[a.cli,'compile','--fqbn','esp32:esp32:esp32',str(stage),'--build-path',str(build),'--build-property','upload.maximum_size=1572864']
  if 'ili9341' in variant: command+=['--build-property',f'compiler.cpp.extra_flags=-DHALLZEE_ILI9341 -DHALLZEE_DISPLAY_ROTATION={variant[-1]}']
  subprocess.run(command,check=True)
  maps.append(build/'bathroom-signin.ino.map')
  shutil.copy2(build/'bathroom-signin.ino.bin',out/(variant+'.bin'))
  usb=out/('USB-'+variant); usb.mkdir()
  for name in ['bathroom-signin.ino.bin','bathroom-signin.ino.bootloader.bin','bathroom-signin.ino.partitions.bin']: shutil.copy2(build/name,usb/name)
  shutil.copy2(data/'packages/esp32/hardware/esp32/3.3.11/tools/partitions/boot_app0.bin',usb/'boot_app0.bin')
 # Collect sources/notices before signing. A release without required source
 # material must fail here, before it can be promoted to the public download.
 bundle=[sys.executable,str(root/'scripts/bundle-firmware-licenses.py'),
         '--output',str(out),'--arduino-data',str(data),'--cli',a.cli]
 if a.source_cache: bundle+=['--cache',a.source_cache]
 subprocess.run(bundle+['--scope','firmware']+[part for path in maps for part in ['--build-map',str(path)]],check=True)
 subprocess.run(bundle+['--scope','usb'],check=True)
 with zipfile.ZipFile(out/'Hallzee-Firmware-Licenses.zip','w',zipfile.ZIP_DEFLATED) as licenses:
  for name in ['LICENSE','COPYRIGHT','THIRD-PARTY-NOTICES.md','SOURCE-AND-LICENSES.md']:
   licenses.write(out/name,name)
  for path in sorted((out/'licenses').rglob('*')):
   if path.is_file(): licenses.write(path,path.relative_to(out).as_posix())
 subprocess.run([a.dotnet,'run','--project',str(root/'tools/FirmwareTool/FirmwareTool.csproj'),'--','pack','--version',a.version,'--build',a.build,'--input',str(out),'--output',str(out/f'Hallzee-Firmware-{a.version}.hallzee-fw'),'--key',str(pathlib.Path(a.key).resolve()),'--notes',str(pathlib.Path(a.notes).resolve())],check=True)
