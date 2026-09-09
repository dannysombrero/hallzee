#!/usr/bin/env python3
"""Publish the exact tested artifacts from a successful manual Actions build."""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile


def gh(*args, payload=None):
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
        if not re.fullmatch(r'(0|[1-9][0-9]{0,2})\.(0|[1-9][0-9]{0,2})\.(0|[1-9][0-9]{0,2})', version):
            raise SystemExit('Invalid version in build metadata.')
        if not re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', repo):
            raise SystemExit('Invalid public repository in build metadata.')
        if metadata['sha'] != run['head_sha'] or metadata['product'] != args.product:
            raise SystemExit('Build metadata does not match this run.')
        destination = json.loads(gh('api', f'repos/{repo}'))
        if destination['private']:
            raise SystemExit('The download repository must be public. No release was published.')
        # Changing the source repository README is not part of release publication.
        if repo == source:
            raise SystemExit('Use a separate public releases repository to publish the teacher README safely.')
        if args.product == 'client':
            assets = [root / f'Desktop-{rid}' / name for rid, name in [
                ('win-x64', 'Hallzee-Windows-win-x64.zip'),
                ('osx-arm64', 'Hallzee-Mac-osx-arm64.zip'),
                ('osx-x64', 'Hallzee-Mac-osx-x64.zip')]]
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
                         + f'\n\nVersion: {version}\n\n[Teacher guide](https://github.com/{repo}#readme)'
                         + f'\n\nSource build: `{run["head_sha"]}`\n')
        gh('release', 'create', tag, *map(str, assets), '--repo', repo, '--draft',
           '--title', f'Hallzee {args.product.title()} {version}', '--notes-file', str(notes))
        # Only update the public instructions after a complete draft has been uploaded.
        existing = json.loads(gh('api', f'repos/{repo}/readme'))
        gh('api', '--method', 'PUT', f'repos/{repo}/contents/{existing["path"]}', '--input', '-', payload={
            'message': f'Update teacher guide for {tag}', 'sha': existing['sha'],
            'content': base64.b64encode(guide.encode()).decode()})
        gh('release', 'edit', tag, '--repo', repo, '--draft=false', '--latest=false')
        print(f'Published https://github.com/{repo}/releases/tag/{tag}')


if __name__ == '__main__':
    main()
