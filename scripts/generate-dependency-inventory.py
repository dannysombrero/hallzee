#!/usr/bin/env python3
"""Inventory resolved packages and retain locally available upstream notices.

Run after restoring .NET projects; npm entries come from the committed lockfile.
This is a license inventory, not an assertion of license compatibility.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def nuget_metadata(name, version, package_folders, package_path):
    record = dict(ecosystem='nuget', name=name, version=version,
                  license='REVIEW REQUIRED', source=f'https://www.nuget.org/packages/{name}/{version}')
    found = set()
    for folder in package_folders:
        package = Path(folder) / package_path
        for spec in package.glob('*.nuspec'):
            xml = ET.parse(spec)
            for element in xml.iter():
                key = element.tag.rsplit('}', 1)[-1]
                if key == 'license':
                    record['license'] = element.text or 'REVIEW REQUIRED'
                    record['license_type'] = element.get('type', '')
                elif key == 'licenseUrl':
                    record['license_url'] = element.text
                elif key in ('copyright', 'authors'):
                    record[key] = element.text
                elif key == 'repository' and element.get('url'):
                    record['repository'] = element.get('url')
                    if element.get('commit'):
                        record['repository_commit'] = element.get('commit')
            found.add((name, version, package))
    if not found:
        raise ValueError(f'Restored package metadata missing for {name}/{version}; restore before collecting notices')
    return record, found


def collect(assets=None, npm=True, rid=None, root=ROOT, framework_references=None):
    records = {}
    notice_dirs = set()
    lock = json.loads((root / 'preview-site/package-lock.json').read_text()) if npm else {}
    for location, item in lock.get('packages', {}).items():
        if not location or not item.get('version'):
            continue
        name = item.get('name') or location.rsplit('node_modules/', 1)[-1]
        records[('npm', name, item['version'])] = dict(
            ecosystem='npm', name=name, version=item['version'],
            license=item.get('license', 'REVIEW REQUIRED'),
            source='https://www.npmjs.com/package/' + name + '/v/' + item['version'])
    if assets is None:
        assets = [asset for tree in ['receiver', 'tools']
                  for asset in (root / tree).glob('**/obj/project.assets.json')]
    for asset in assets:
        if not asset.is_file():
            raise ValueError(f'Restore the release project first; assets file missing: {asset}')
        data = json.loads(asset.read_text())
        selected = None
        if rid:
            targets = [value for name, value in data.get('targets', {}).items()
                       if name.endswith('/' + rid)]
            if not targets:
                raise ValueError(f'{asset} has no target for {rid}; restore/publish with -r {rid} first.')
            selected = {name for target in targets for name in target}
        packages = {}
        for identifier, item in data.get('libraries', {}).items():
            if item.get('type') != 'package' or (selected is not None and identifier not in selected):
                continue
            name, version = identifier.rsplit('/', 1)
            packages[(name, version)] = item['path']
        # Self-contained .NET runtime/host packs are downloadDependencies, not
        # ordinary library entries. Their third-party notices matter too.
        for framework in data.get('project', {}).get('frameworks', {}).values():
            for item in framework.get('downloadDependencies', []):
                name = item['name']
                if rid and '.Runtime.' in name and not name.lower().endswith('.' + rid.lower()):
                    continue
                version = re.fullmatch(r'\[([^,\]]+)(?:,\s*\1)?\]', item['version'])
                if not version:
                    raise ValueError(f'Expected an exact resolved download dependency: {item}')
                version = version.group(1)
                packages[(name, version)] = name.lower() + '/' + version.lower()
        for (name, version), path in packages.items():
            record, found = nuget_metadata(name, version, data.get('packageFolders', {}), path)
            records[('nuget', name, version)] = record
            notice_dirs.update(found)
    for framework_file in framework_references or []:
        references = json.loads(framework_file.read_text())['Items']['ResolvedFrameworkReference']
        for reference in references:
            if not reference.get('RuntimePackPath'):
                continue
            folder = Path(reference['RuntimePackPath'])
            name, version = reference['RuntimePackName'], reference['RuntimePackVersion']
            record, found = nuget_metadata(name, version, [folder.parent], folder.name)
            records[('nuget', name, version)] = record
            notice_dirs.update(found)
    return [records[key] for key in sorted(records)], notice_dirs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--notices', type=Path, help='Copy package copyright/license files for distribution')
    parser.add_argument('--assets', type=Path, action='append', help='Exact release project.assets.json (repeatable); fails if absent')
    parser.add_argument('--no-npm', action='store_true', help='Omit website dependencies from a desktop/USB release')
    parser.add_argument('--rid', help='Only the restored runtime target, for example osx-arm64')
    parser.add_argument('--publish-dir', type=Path, help='Record SHA-256 of the actual published files, including native/runtime files')
    parser.add_argument('--framework-references', type=Path, action='append', help='MSBuild -t:ResolveReferences -getItem:ResolvedFrameworkReference JSON; includes native workload runtime packs')
    args = parser.parse_args()
    if args.rid and not args.assets:
        parser.error('--rid requires explicit --assets to avoid inventorying unrelated restores')
    try:
        records, notice_dirs = collect(args.assets, not args.no_npm, args.rid,
                                      framework_references=args.framework_references)
    except ValueError as error:
        parser.error(str(error))
    payload = dict(
        scope='Resolved build dependencies; not every build dependency is redistributed. Firmware/native tool sources are described in THIRD-PARTY-NOTICES.md.',
        runtime_identifier=args.rid, packages=records)
    if args.publish_dir:
        if not args.publish_dir.is_dir():
            parser.error(f'Publish directory does not exist: {args.publish_dir}')
        payload['published_files'] = [dict(path=path.relative_to(args.publish_dir).as_posix(),
                                         sha256=hashlib.sha256(path.read_bytes()).hexdigest())
                                      for path in sorted(args.publish_dir.rglob('*'))
                                      if path.is_file() and path.resolve() != args.output.resolve()
                                      and 'licenses' not in path.relative_to(args.publish_dir).parts]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, indent=2) + '\n')
    if args.notices:
        package_records = {(record['name'], record['version']): record for record in records if record['ecosystem'] == 'nuget'}
        for name, version, folder in sorted(notice_dirs):
            target = args.notices / (name + '-' + version)
            target.mkdir(parents=True, exist_ok=True)
            record = package_records[(name, version)]
            (target / 'PACKAGE-METADATA.json').write_text(json.dumps(record, indent=2) + '\n')
            copied = False
            for path in folder.rglob('*'):
                if path.is_file() and path.name.lower().startswith(('license', 'copying', 'copyright', 'notice', 'third-party-notice')):
                    destination = target / path.relative_to(folder)
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copyfile(path, destination)
                    copied = True
            # NuGet license expressions do not guarantee the nupkg contains the
            # actual terms. Retain those terms plus original package attribution.
            if not copied and record.get('license_type') == 'expression':
                license_file = ROOT / 'licenses/license-texts' / (record['license'] + '.txt')
                if not license_file.is_file():
                    parser.error(f'No notice text for {name}/{version} ({record["license"]}); add reviewed upstream terms first')
                shutil.copy2(license_file, target / 'LICENSE.txt')
    unknown = sum(p['license'] == 'REVIEW REQUIRED' and not p.get('license_url') for p in records)
    print(f'Inventoried {len(records)} resolved packages; {unknown} have no license declaration or license URL.')


if __name__ == '__main__':
    main()
