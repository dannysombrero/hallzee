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
    run = json.loads(gh('api', f'repos/{source}/actions/runs/{args.run}'))
    workflow = 'release-client.yml' if args.product == 'client' else 'release-firmware.yml'
    if (run['conclusion'] != 'success' or run['event'] != 'workflow_dispatch'
            or run['path'] != f'.github/workflows/{workflow}'):
        raise SystemExit('Choose a successful manual release build for this product.')
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
        destination = json.loads(gh('api', f'repos/{repo}'))
        if destination['private']:
            raise SystemExit('The download repository must be public. No release was published.')
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
        if any(not p.is_file() or not p.stat().st_size for p in assets):
            raise SystemExit('Build is missing required release assets.')
        guide = (root / 'Release-metadata' / 'teacher-guide.md').read_text().replace('https://github.com/dannysombrero/hallzee-mono', f'https://github.com/{repo}')
        guide_path = root / 'Hallzee-Teacher-Guide.md'
        guide_path.write_text(guide)
        assets.append(guide_path)
        checksums = root / 'SHA256SUMS.txt'
        checksums.write_text(''.join(f'{hashlib.sha256(p.read_bytes()).hexdigest()}  {p.name}\n' for p in assets))
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
