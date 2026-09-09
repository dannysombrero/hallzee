#!/usr/bin/env python3
"""Inventory resolved packages and retain locally available upstream notices.

Run after restoring .NET projects; npm entries come from the committed lockfile.
This is a license inventory, not an assertion of license compatibility.
"""
import argparse
import json
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def collect():
    records = {}
    notice_dirs = set()
    lock = json.loads((ROOT / 'preview-site/package-lock.json').read_text())
    for location, item in lock.get('packages', {}).items():
        if not location or not item.get('version'):
            continue
        name = item.get('name') or location.rsplit('node_modules/', 1)[-1]
        records[('npm', name, item['version'])] = dict(
            ecosystem='npm', name=name, version=item['version'],
            license=item.get('license', 'REVIEW REQUIRED'),
            source='https://www.npmjs.com/package/' + name + '/v/' + item['version'])
    for tree in ['receiver', 'tools']:
        for asset in (ROOT / tree).glob('**/obj/project.assets.json'):
            data = json.loads(asset.read_text())
            for identifier, item in data.get('libraries', {}).items():
                if item.get('type') != 'package':
                    continue
                name, version = identifier.rsplit('/', 1)
                record = dict(ecosystem='nuget', name=name, version=version,
                              license='REVIEW REQUIRED', source=f'https://www.nuget.org/packages/{name}/{version}')
                for folder in data.get('packageFolders', {}):
                    package = Path(folder) / item['path']
                    for spec in package.glob('*.nuspec'):
                        xml = ET.parse(spec)
                        for element in xml.iter():
                            key = element.tag.rsplit('}', 1)[-1]
                            if key == 'license':
                                record['license'] = element.text or 'REVIEW REQUIRED'
                                record['license_type'] = element.get('type', '')
                            elif key == 'licenseUrl':
                                record['license_url'] = element.text
                            elif key == 'repository' and element.get('url'):
                                record['repository'] = element.get('url')
                        notice_dirs.add((name, version, package))
                records[('nuget', name, version)] = record
    return [records[key] for key in sorted(records)], notice_dirs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--notices', type=Path, help='Copy package copyright/license files for distribution')
    args = parser.parse_args()
    records, notice_dirs = collect()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(dict(
        scope='npm lockfile and locally restored NuGet assets; native Arduino/SDK components are described in THIRD-PARTY-NOTICES.md',
        packages=records), indent=2) + '\n')
    if args.notices:
        for name, version, folder in sorted(notice_dirs):
            for path in folder.rglob('*'):
                if path.is_file() and path.name.lower().startswith(('license', 'copying', 'copyright', 'notice', 'third-party-notice')):
                    destination = args.notices / (name + '-' + version) / path.relative_to(folder)
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copyfile(path, destination)
    unknown = sum(p['license'] == 'REVIEW REQUIRED' and not p.get('license_url') for p in records)
    print(f'Inventoried {len(records)} resolved packages; {unknown} have no license declaration or license URL.')


if __name__ == '__main__':
    main()
