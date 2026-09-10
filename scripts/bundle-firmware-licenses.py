#!/usr/bin/env python3
"""Retain pinned upstream source, notices, and build evidence with a release.

Uses only the Python standard library and Git. Downloads happen before signing;
missing sources, library drift, and newly linked unmanaged components fail closed.
The generated sources are published beside the binaries, not committed to Git.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tarfile

ROOT = Path(__file__).resolve().parents[1]


def run(*arguments, cwd=None):
    return subprocess.check_output(arguments, cwd=cwd, text=True).strip()


def sha256(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def source_files(folder):
    for directory, children, files in os.walk(folder, followlinks=False):
        children[:] = sorted(child for child in children if child not in {'.git', '__pycache__'})
        for name in children:
            path = Path(directory) / name
            if path.is_symlink():
                if Path(os.readlink(path)).is_absolute() or not path.resolve().is_relative_to(folder.resolve()):
                    raise ValueError(f'Unsafe source symlink: {path}')
                yield path
        for name in sorted(files):
            if name == '.git' or name.endswith('.pyc'):
                continue
            path = Path(directory) / name
            if path.is_symlink() and (Path(os.readlink(path)).is_absolute()
                                      or not path.resolve().is_relative_to(folder.resolve())):
                raise ValueError(f'Unsafe source symlink: {path}')
            yield path


def is_notice(path):
    return (path.name.lower().startswith(('license', 'copying', 'copyright', 'notice', 'third-party-notice'))
            or any(part.lower() == 'licenses' for part in path.parts))


def archive_source(folder, destination, name):
    with tarfile.open(destination, 'w:gz') as archive:
        for path in source_files(folder):
            archive.add(path, arcname=name + '/' + path.relative_to(folder).as_posix(), recursive=False)


def copy_notices(folder, destination):
    count = 0
    for path in source_files(folder):
        if is_notice(path.relative_to(folder)):
            target = destination / path.relative_to(folder)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
            count += 1
    return count


def checkout_source(item, cache):
    folder = cache / (item['name'] + '-' + item['commit'])
    if not folder.exists():
        folder.mkdir(parents=True)
        run('git', 'init', '--quiet', str(folder))
        run('git', 'remote', 'add', 'origin', item['repository'], cwd=folder)
    if run('git', 'remote', 'get-url', 'origin', cwd=folder) != item['repository']:
        raise ValueError(f'Unexpected source cache remote: {folder}')
    try:
        commit = run('git', 'rev-parse', '--verify', '--quiet', 'HEAD', cwd=folder)
    except subprocess.CalledProcessError:
        commit = ''
    if commit != item['commit']:
        run('git', 'fetch', '--quiet', '--depth=1', 'origin', item['commit'], cwd=folder)
        run('git', 'checkout', '--quiet', '--detach', 'FETCH_HEAD', cwd=folder)
    if run('git', 'rev-parse', 'HEAD', cwd=folder) != item['commit']:
        raise ValueError(f'Source revision mismatch: {folder}')
    if item.get('recursive'):
        run('git', '-c', 'protocol.file.allow=never', 'submodule', 'update', '--init',
            '--recursive', '--depth=1', '--jobs=4', cwd=folder)
    if run('git', 'status', '--porcelain', '--ignored', '--untracked-files=all', cwd=folder):
        raise ValueError(f'Modified source cache; use a fresh --cache: {folder}')
    if item.get('recursive') and run('git', 'submodule', 'foreach', '--quiet', '--recursive',
                                     'git status --porcelain --ignored --untracked-files=all', cwd=folder):
        raise ValueError(f'Modified submodule source cache; use a fresh --cache: {folder}')
    return folder


def linked_archives(map_text):
    # Only archive members used to satisfy symbols count. The later LOAD list
    # includes every SDK component, even those removed by the linker.
    if not map_text.startswith('Archive member included to satisfy reference'):
        raise ValueError('Unrecognized linker map; cannot determine linked SDK components')
    section = re.split(r'Allocating common symbols|Discarded input sections', map_text, maxsplit=1)[0]
    return sorted(set(re.findall(r'\b(lib[\w.-]+\.a)\(', section)))


def check_managed_components(archives, sources):
    known = {item['managed_component'] for item in sources if item.get('managed_component')}
    linked = {name[3:-2] for name in archives if '__' in name}
    if linked - known:
        raise ValueError('Add exact source and license coverage for newly linked SDK components: '
                         + ', '.join(sorted(linked - known)))


def verify_sdk(sdk, lock):
    versions = (sdk / 'versions.txt').read_text()
    for name, expected in lock['sdk_versions'].items():
        line = next((line for line in versions.splitlines() if line.startswith(name + ':')), '')
        if expected not in line.split(':', 1)[-1].strip().split():
            raise ValueError(f'Pinned SDK provenance mismatch: expected {name} {expected}; got {line!r}')


def installed_libraries(cli):
    pins = [line.rsplit('@', 1) for line in (ROOT / 'firmware/terminal/arduino-libraries.txt').read_text().splitlines()
            if line and not line.startswith('#')]
    installed = json.loads(run(cli, 'lib', 'list', '--format', 'json'))
    libraries = {entry['library']['name']: entry['library']
                 for entry in installed['installed_libraries']}
    result = []
    for name, version in pins:
        library = libraries.get(name)
        if library is None or library['version'] != version:
            raise ValueError(f'Install the pinned Arduino library {name}@{version} before releasing')
        result.append(library)
    return result


def bundle(args):
    lock = json.loads((ROOT / 'firmware/release-sources.json').read_text())
    args.output.mkdir(parents=True, exist_ok=True)
    notices = args.output / 'licenses'
    notices.mkdir(exist_ok=True)
    sources = args.output / 'sources'
    sources.mkdir(exist_ok=True)
    manifest = dict(scope=args.scope, core_version=lock['core_version'], sources=[])
    selected = [item for item in lock['sources'] if item['scope'] == args.scope]
    if args.scope == 'firmware':
        if not args.build_map:
            raise ValueError('Firmware source bundling requires --build-map for every release variant')
        sdk = args.arduino_data / 'packages/esp32/tools/esp32-libs' / lock['core_version']
        verify_sdk(sdk, lock)
        manifest['linked_archives'] = {}
        for path in args.build_map:
            archives = linked_archives(path.read_text())
            check_managed_components(archives, selected)
            manifest['linked_archives'][path.parent.name] = archives
        for name in ('versions.txt', 'sdkconfig'):
            shutil.copy2(sdk / name, notices / name)
        # Keep all declared Arduino dependencies, including their source headers.
        for library in installed_libraries(args.cli):
            folder = Path(library['install_dir'])
            name = folder.name + '-' + library['version']
            archive = sources / (name + '.tar.gz')
            archive_source(folder, archive, name)
            count = copy_notices(folder, notices / name)
            manifest['sources'].append(dict(name=library['name'], version=library['version'],
                                            source=library.get('website'), archive=archive.name,
                                            sha256=sha256(archive), notice_files=count))
        # The core binaries link compiler runtimes; retain their own notices too.
        toolchains = list((args.arduino_data / 'packages/esp32/tools').glob('esp-x32/*/share/licenses'))
        toolchains += list((args.arduino_data / 'packages/esp32/tools').glob('xtensa-esp-elf-gcc/*/share/licenses'))
        if not toolchains:
            raise ValueError('Compiler runtime notices are missing from the installed ESP32 toolchain')
        for folder in toolchains:
            copy_notices(folder, notices / 'compiler-runtime')
    for item in selected:
        if item.get('reference_only'):
            manifest['sources'].append(item)
            continue
        print(f"Collecting {item['name']} sources at {item['commit']}", flush=True)
        folder = checkout_source(item, args.cache)
        archive = sources / (item['name'] + '-' + item['commit'][:12] + '.tar.gz')
        archive_source(folder, archive, item['name'])
        count = copy_notices(folder, notices / item['name'])
        if not count:
            raise ValueError(f"No upstream license/notice files found for {item['name']}")
        record = dict(item, archive=archive.name, sha256=sha256(archive), notice_files=count)
        if item.get('recursive'):
            record['submodules'] = run('git', 'submodule', 'status', '--recursive', cwd=folder).splitlines()
        manifest['sources'].append(record)
    for name in ('LICENSE', 'COPYRIGHT', 'THIRD-PARTY-NOTICES.md'):
        shutil.copy2(ROOT / name, args.output / name)
    shutil.copy2(ROOT / 'docs/release-licensing.md', args.output / 'SOURCE-AND-LICENSES.md')
    shutil.copy2(ROOT / 'licenses/DM-Sans-OFL.txt', notices / 'DM-Sans-OFL.txt')
    if args.scope == 'usb':
        for name in ('MinGW-winpthreads-COPYING.txt', 'GCC-Runtime-Exception.txt', 'GPL-3.0.txt'):
            shutil.copy2(ROOT / 'licenses' / name, notices / name)
    (notices / (args.scope + '-source-manifest.json')).write_text(json.dumps(manifest, indent=2) + '\n')
    print(f'Preserved {len(manifest["sources"])} source archives in {sources}', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--scope', choices=('firmware', 'usb'), required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--arduino-data', type=Path, required=True)
    parser.add_argument('--build-map', type=Path, action='append')
    parser.add_argument('--cli', default='arduino-cli')
    parser.add_argument('--cache', type=Path, default=ROOT / '.tools/release-sources')
    args = parser.parse_args()
    try:
        bundle(args)
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        parser.error(str(error))


if __name__ == '__main__':
    main()
