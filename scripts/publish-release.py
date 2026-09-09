#!/usr/bin/env python3
"""Publish the exact tested artifacts from a successful manual Actions build."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
import zipfile
from urllib.parse import quote


def sha256(path):
    digest = hashlib.sha256()
    with path.open('rb') as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def firmware_sources(root):
    """Retain the exact corresponding sources that accompanied the build."""
    notices = root / 'Hallzee-Firmware-Licenses.zip'
    if not notices.is_file() or not notices.stat().st_size:
        raise SystemExit('Build is missing its firmware/USB license archive. Rebuild with current release tooling.')
    archives = {}
    for scope in ['firmware', 'usb']:
        manifest_path = root / 'licenses' / f'{scope}-source-manifest.json'
        if not manifest_path.is_file():
            raise SystemExit(f'Build is missing the {scope} source manifest.')
        manifest = json.loads(manifest_path.read_text())
        if manifest.get('scope') != scope or not manifest.get('sources'):
            raise SystemExit(f'Invalid {scope} source manifest.')
        included = 0
        for item in manifest['sources']:
            # This build-only upstream repository has no standalone license.
            # Its pinned provenance is retained; it contributes no shipped code.
            if item.get('reference_only') is True and item.get('name') == 'esp32-arduino-lib-builder':
                continue
            name, digest = item.get('archive', ''), item.get('sha256', '')
            if (not name or Path(name).name != name or not name.endswith('.tar.gz')
                    or not re.fullmatch(r'[0-9a-f]{64}', digest)):
                raise SystemExit(f'Invalid archive entry in the {scope} source manifest.')
            path = root / 'sources' / name
            if not path.is_file() or sha256(path) != digest:
                raise SystemExit(f'Missing or altered corresponding-source archive: {name}')
            archives[name] = path
            included += 1
        if not included:
            raise SystemExit(f'No corresponding sources included for {scope}.')
    return [notices] + [archives[name] for name in sorted(archives)]


def zip_sha256(archive, name):
    # Some Windows ZIP producers retain backslashes in member names. Compare
    # canonical names and reject duplicates even when their separators differ.
    entries = [entry for entry in archive.infolist() if entry.filename.replace('\\', '/') == name]
    if len(entries) != 1:
        raise SystemExit(f'Expected one archive member: {name}')
    digest = hashlib.sha256()
    with archive.open(entries[0]) as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def usb_python_sources(root, rid):
    companion = root / f'Hallzee-USB-Python-Sources-{rid}.zip'
    binary = root / f'Hallzee-USB-{rid}.zip'
    if not companion.is_file() or not binary.is_file():
        raise SystemExit(f'Build is missing the {rid} USB binary/source pair.')
    try:
        with zipfile.ZipFile(companion) as sources, zipfile.ZipFile(binary) as usb:
            manifest_name = 'licenses/usb-python-source-manifest.json'
            usb_manifest = 'licenses/python/usb-python-source-manifest.json'
            if zip_sha256(sources, manifest_name) != zip_sha256(usb, usb_manifest):
                raise SystemExit(f'{rid} USB source manifest does not match the binary package.')
            manifest = json.loads(sources.read(manifest_name))
            executable = 'esptool.exe' if rid == 'win-x64' else 'esptool'
            expected_platform = 'Windows' if rid == 'win-x64' else 'Darwin'
            expected_architectures = {'amd64', 'x86_64'} if rid == 'win-x64' else {'arm64', 'aarch64'}
            if (manifest.get('scope') != 'usb-python' or not manifest.get('sources')
                    or manifest.get('platform') != expected_platform
                    or manifest.get('architecture', '').lower() not in expected_architectures
                    or manifest.get('binary', {}).get('name') != executable
                    or zip_sha256(usb, f'tool/{executable}') != manifest['binary'].get('sha256')):
                raise SystemExit(f'{rid} USB executable does not match its recorded build.')
            for item in manifest['sources']:
                name = item.get('archive', '')
                if not name or Path(name).name != name or zip_sha256(sources, name) != item.get('sha256'):
                    raise SystemExit(f'{rid} USB corresponding source is missing or altered.')
    except (zipfile.BadZipFile, KeyError, ValueError) as error:
        raise SystemExit(f'Invalid {rid} USB source companion: {error}') from error
    return companion


def gh(*args, payload=None, output_file=None):
    if output_file is not None:
        with output_file.open('wb') as output:
            subprocess.run(['gh', *args], stdout=output, check=True)
        return ''
    result = subprocess.run(['gh', *args], input=json.dumps(payload) if payload else None,
                            text=True, check=True, capture_output=True)
    return result.stdout


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--run', required=True, type=int)
    parser.add_argument('--product', required=True, choices=['client', 'firmware'])
    args = parser.parse_args()
    source = os.environ['GITHUB_REPOSITORY']
    destination = json.loads(gh('api', f'repos/{source}'))
    branch = destination['default_branch']
    if destination['private']:
        raise SystemExit('The download repository must be public. No release was published.')
    run = json.loads(gh('api', f'repos/{source}/actions/runs/{args.run}'))
    workflow = 'release-client.yml' if args.product == 'client' else 'release-firmware.yml'
    if (run['conclusion'] != 'success' or run['event'] != 'workflow_dispatch'
            or run['path'] != f'.github/workflows/{workflow}'
            or run.get('head_branch') != branch
            or run.get('head_repository', {}).get('full_name') != source
            or not re.fullmatch(r'[0-9a-f]{40}', run.get('head_sha', ''))):
        raise SystemExit('Choose a successful manual release build from this repository\'s default branch for this product.')
    comparison = json.loads(gh('api', f'repos/{source}/compare/{run["head_sha"]}...{quote(branch, safe="")}'))
    if comparison.get('status') not in ['ahead', 'identical']:
        raise SystemExit('The tested commit is no longer on the default branch. Rebuild after any history cleanup.')
    with tempfile.TemporaryDirectory() as temp:
        root = Path(temp)
        gh('run', 'download', str(args.run), '--repo', source, '--dir', temp)
        metadata_path = root / 'Release-metadata' / 'release.json'
        metadata = json.loads(metadata_path.read_text())
        version, repo = metadata['version'], metadata['repository']
        if repo != source:
            raise SystemExit('Rebuild with this repository as the release destination.')
        if not re.fullmatch(r'(0|[1-9][0-9]{0,2})\.(0|[1-9][0-9]{0,2})\.(0|[1-9][0-9]{0,2})', version):
            raise SystemExit('Invalid version in build metadata.')
        if not re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', repo):
            raise SystemExit('Invalid public repository in build metadata.')
        if metadata['sha'] != run['head_sha'] or metadata['product'] != args.product:
            raise SystemExit('Build metadata does not match this run.')
        # The open-source repository is also the public download repository.
        # The workflow grants this job Contents write only for this publication.
        if args.product == 'client':
            assets = [root / f'Desktop-{rid}' / name for rid, name in [
                ('win-x64', 'Hallzee-Windows-win-x64.zip'),
                ('osx-arm64', 'Hallzee-Mac-osx-arm64.zip'),
                ('osx-x64', 'Hallzee-Mac-osx-x64.zip')]]
            # New Windows builds upload app files directly. Preserve GitHub's
            # original artifact ZIP, which is the same one reviewers downloaded.
            # Older builds still contain a prebuilt ZIP under Desktop-win-x64.
            if not assets[0].is_file():
                ids = gh('api', f'repos/{source}/actions/runs/{args.run}/artifacts',
                         '--paginate', '--jq',
                         '.artifacts[] | select(.name == "Hallzee-Windows-win-x64") | .id').split()
                if len(ids) != 1 or not ids[0].isdigit():
                    raise SystemExit('Expected exactly one Windows app artifact.')
                assets[0] = root / 'Hallzee-Windows-win-x64.zip'
                gh('api', f'repos/{source}/actions/artifacts/{ids[0]}/zip', output_file=assets[0])
        else:
            packages = list((root / 'Firmware-and-USB-images').glob('*.hallzee-fw'))
            if len(packages) != 1:
                raise SystemExit('Expected exactly one signed firmware package.')
            assets = packages + [root / f'USB-{rid}' / f'Hallzee-USB-{rid}.zip'
                                 for rid in ['win-x64', 'osx-arm64']]
            assets += firmware_sources(root / 'Firmware-and-USB-images')
            assets += [usb_python_sources(root / f'USB-{rid}', rid) for rid in ['win-x64', 'osx-arm64']]
        if any(not p.is_file() or not p.stat().st_size for p in assets):
            raise SystemExit('Build is missing required release assets.')
        guide = (root / 'Release-metadata' / 'teacher-guide.md').read_text().replace('https://github.com/dannysombrero/hallzee-mono', f'https://github.com/{repo}')
        guide_path = root / 'Hallzee-Teacher-Guide.md'
        guide_path.write_text(guide)
        assets.append(guide_path)
        checksums = root / 'SHA256SUMS.txt'
        checksums.write_text(''.join(f'{sha256(p)}  {p.name}\n' for p in assets))
        assets.append(checksums)
        tag = f'{args.product}-v{version}'
        # Refuse every existing tag, including a draft or a partially completed attempt.
        tags = json.loads(gh('api', f'repos/{repo}/git/matching-refs/tags/{tag}'))
        if any(t['ref'] == f'refs/tags/{tag}' for t in tags):
            raise SystemExit('Version tag already exists; it will not be overwritten.')
        notes = root / 'notes.md'
        notes.write_text((root / 'Release-metadata' / 'notes.md').read_text()
                         + f'\n\nVersion: {version}\n\n[Teacher guide](https://github.com/{repo}/blob/{run["head_sha"]}/docs/getting-started-users.md)'
                         + f'\n\n[Corresponding source](https://github.com/{repo}/archive/{run["head_sha"]}.zip)'
                         + f'\n\nSource build: `{run["head_sha"]}`\n')
        gh('release', 'create', tag, *map(str, assets), '--repo', repo, '--draft',
           '--target', run['head_sha'],
           '--title', f'Hallzee {args.product.title()} {version}', '--notes-file', str(notes))
        # Source documentation changes go through pull requests. The tested
        # teacher guide is attached to the release, never written over README.
        gh('release', 'edit', tag, '--repo', repo, '--draft=false', '--latest=false')
        print(f'Published https://github.com/{repo}/releases/tag/{tag}')


if __name__ == '__main__':
    main()
