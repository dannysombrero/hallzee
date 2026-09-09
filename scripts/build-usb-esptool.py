#!/usr/bin/env python3
"""Build an auditable standalone esptool from hash-locked Python packages.

Run with Python 3.13 on the target OS. The fresh venv prevents unrelated local
packages entering the binary. Every locked distribution's source and original
notices are retained; PyInstaller build dependencies are explicitly identified.
"""
import argparse
import ast
import hashlib
import importlib.metadata
import json
import os
from pathlib import Path, PurePosixPath
import platform
import re
import shutil
import ssl
import subprocess
import sys
import tarfile
import tempfile
import urllib.request
import venv
import zipfile

ROOT = Path(__file__).resolve().parents[1]
LOCK = ROOT / 'firmware/requirements.txt'


def download(url, target, expected=None):
    digest = hashlib.sha256()
    with urllib.request.urlopen(url, timeout=120) as response, target.open('wb') as stream:
        for block in iter(lambda: response.read(1024 * 1024), b''):
            stream.write(block)
            digest.update(block)
    actual = digest.hexdigest()
    if expected and actual != expected:
        target.unlink()
        raise ValueError(f'Source checksum mismatch: {url}')
    return actual


def locked_requirements(text):
    result = {}
    current = None
    for line in text.splitlines():
        if line and not line.startswith((' ', '#')):
            match = re.match(r'([A-Za-z0-9_.-]+)==([^\s;]+)', line)
            if not match:
                raise ValueError(f'Expected a pinned requirement: {line}')
            current = re.sub(r'[-_.]+', '-', match.group(1)).lower()
            result[current] = dict(version=match.group(2), lines=[line], hashes=set())
        elif current and line.lstrip().startswith('--hash='):
            result[current]['lines'].append(line)
        for digest in re.findall(r'--hash=sha256:([a-f0-9]{64})', line):
            result[current]['hashes'].add(digest)
    return result


def notice_name(path):
    path = PurePosixPath(str(path).replace('\\', '/'))
    return (path.name.lower().startswith(('license', 'copying', 'copyright', 'notice', 'third-party-notice'))
            or 'licenses' in [part.lower() for part in path.parts])


def archive_notices(archive_path, destination):
    """Copy only regular notice files; never extract symlinks or arbitrary paths."""
    count = 0
    if zipfile.is_zipfile(archive_path):
        with zipfile.ZipFile(archive_path) as archive:
            for member in archive.infolist():
                path = PurePosixPath(member.filename)
                if member.is_dir() or not notice_name(path):
                    continue
                if path.is_absolute() or '..' in path.parts:
                    raise ValueError(f'Unsafe source archive member: {member.filename}')
                target = destination.joinpath(*path.parts)
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(archive.read(member))
                count += 1
        return count
    with tarfile.open(archive_path, 'r:*') as archive:
        for member in archive.getmembers():
            path = PurePosixPath(member.name)
            if not member.isfile() or not notice_name(path):
                continue
            if path.is_absolute() or '..' in path.parts:
                raise ValueError(f'Unsafe source archive member: {member.name}')
            target = destination.joinpath(*path.parts)
            target.parent.mkdir(parents=True, exist_ok=True)
            with archive.extractfile(member) as source, target.open('wb') as output:
                shutil.copyfileobj(source, output)
            count += 1
    return count


def collect(args):
    locked = locked_requirements(LOCK.read_text())
    args.licenses.mkdir(parents=True, exist_ok=True)
    args.sources.mkdir(parents=True, exist_ok=True)
    records = []
    installed = list(importlib.metadata.distributions())
    for distribution in sorted(installed, key=lambda item: item.metadata['Name'].lower()):
        name, version = distribution.metadata['Name'], distribution.version
        key = re.sub(r'[-_.]+', '-', name).lower()
        if key == 'pip':
            continue  # installer; not frozen into esptool
        if key not in locked or locked[key]['version'] != version:
            raise ValueError(f'Unexpected build environment distribution: {name} {version}')
        print(f'Collecting {name} {version} source and notices', flush=True)
        metadata = json.load(urllib.request.urlopen(f'https://pypi.org/pypi/{name}/{version}/json', timeout=60))
        source = next((item for item in metadata['urls'] if item['packagetype'] == 'sdist'), None)
        if source is None or source['digests']['sha256'] not in locked[key]['hashes']:
            raise ValueError(f'No source distribution with a locked checksum for {name}/{version}')
        filename = source['filename']
        if Path(filename).name != filename or not source['url'].startswith('https://files.pythonhosted.org/'):
            raise ValueError(f'Unexpected PyPI source location for {name}')
        archive = args.sources / filename
        checksum = download(source['url'], archive, source['digests']['sha256'])
        notice_dir = args.licenses / (name + '-' + version)
        notice_dir.mkdir(parents=True, exist_ok=True)
        notices = 0
        for path in distribution.files or []:
            if notice_name(path):
                original = Path(distribution.locate_file(path))
                if original.is_file():
                    # Preserve source paths relative to the package; RECORD paths
                    # can reference executables outside site-packages, not notices.
                    relative = PurePosixPath(str(path).replace('\\', '/'))
                    if relative.is_absolute() or '..' in relative.parts:
                        raise ValueError(f'Unsafe package notice path: {path}')
                    target = notice_dir.joinpath(*relative.parts)
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(original, target)
                    notices += 1
        # Retain source-only notices too, e.g. native code vendored into wheels.
        notices += archive_notices(archive, notice_dir / 'source-notices')
        if not notices:
            raise ValueError(f'No copyright/license text found for {name}/{version}')
        record = dict(name=name, version=version, source=source['url'], archive=filename,
                      sha256=checksum, license=metadata['info'].get('license_expression') or metadata['info'].get('license'),
                      notice_files=notices)
        (notice_dir / 'PACKAGE-METADATA.json').write_text(json.dumps(record, indent=2) + '\n')
        records.append(record)
    # CPython's own source contains its license and the notices for vendored
    # stdlib components. Keep the exact interpreter version used by this runner.
    python_version = platform.python_version()
    python_archive = args.sources / ('Python-' + python_version + '.tar.xz')
    python_url = f'https://www.python.org/ftp/python/{python_version}/{python_archive.name}'
    python_hash = download(python_url, python_archive)
    archive_notices(python_archive, args.licenses / 'CPython')
    records.append(dict(name='CPython', version=python_version, source=python_url,
                        archive=python_archive.name, sha256=python_hash))
    interpreter = Path(sys.base_prefix)
    locations = [interpreter, interpreter / 'share/licenses',
                 interpreter / 'lib' / f'python{sys.version_info.major}.{sys.version_info.minor}']
    for directory in locations:
        if not directory.is_dir():
            continue
        candidates = directory.rglob('*') if directory.name == 'licenses' else directory.iterdir()
        for path in candidates:
            if path.is_file() and notice_name(path.name):
                target = args.licenses / 'CPython-distribution' / path.relative_to(interpreter)
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(path, target)
    # CPython dynamically supplies OpenSSL to its _ssl/_hashlib extensions;
    # OpenSSL's Apache terms/attribution must be retained separately.
    openssl_version = ssl.OPENSSL_VERSION.split()[1]
    openssl_url = f'https://raw.githubusercontent.com/openssl/openssl/openssl-{openssl_version}/LICENSE.txt'
    openssl_license = args.licenses / 'OpenSSL-LICENSE.txt'
    download(openssl_url, openssl_license)
    manifest = dict(scope='usb-python', python=python_version, openssl=ssl.OPENSSL_VERSION,
                    platform=platform.system(), architecture=platform.machine(),
                    scope_note='Fresh locked build environment, including PyInstaller build dependencies; the frozen esptool may use a subset.',
                    requirements_sha256=hashlib.sha256(LOCK.read_bytes()).hexdigest(), sources=records)
    (args.licenses / 'usb-python-source-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    shutil.copy2(LOCK, args.licenses / LOCK.name)


def frozen_native_files(toc):
    """Record the actual native inputs selected by PyInstaller, without local paths."""
    result = {}

    def visit(value):
        if isinstance(value, (list, tuple)):
            if (len(value) == 3 and value[-1] in ('BINARY', 'EXTENSION')
                    and isinstance(value[1], str) and Path(value[1]).is_file()):
                path = Path(value[1])
                result[value[0]] = dict(name=value[0], sha256=hashlib.sha256(path.read_bytes()).hexdigest())
            else:
                for item in value:
                    visit(item)

    visit(ast.literal_eval(toc.read_text()))
    if not result:
        raise ValueError('PyInstaller did not report its native inputs')
    return [result[key] for key in sorted(result)]


def build(args):
    if sys.version_info[:2] != (3, 13):
        raise ValueError('Build USB esptool with Python 3.13 (the release workflow installs it automatically)')
    if (platform.system(), platform.machine().lower()) not in {('Darwin', 'arm64'), ('Windows', 'amd64')}:
        raise ValueError('USB esptool releases currently support Mac ARM64 and Windows x64; desktop Intel Mac support is separate')
    for directory in (args.licenses, args.sources):
        if directory.exists() and any(directory.iterdir()):
            raise ValueError(f'Use an empty licenses/sources directory to avoid stale dependencies: {directory}')
    args.output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='hallzee-esptool-') as temporary:
        stage = Path(temporary)
        environment = stage / 'venv'
        venv.EnvBuilder(with_pip=True).create(environment)
        python = environment / ('Scripts/python.exe' if os.name == 'nt' else 'bin/python')
        locked = locked_requirements(LOCK.read_text())
        bootstrap = stage / 'bootstrap.txt'
        bootstrap.write_text('\n'.join(locked['setuptools']['lines']) + '\n')
        pip = [str(python), '-m', 'pip', 'install', '--disable-pip-version-check', '--no-cache-dir', '--require-hashes']
        subprocess.run(pip + ['--no-deps', '-r', str(bootstrap)], check=True)
        subprocess.run(pip + ['--no-build-isolation', '-r', str(LOCK)], check=True)
        subprocess.run([str(python), str(Path(__file__).resolve()), '--collect', '--output', str(args.output),
                        '--licenses', str(args.licenses), '--sources', str(args.sources)], check=True)
        subprocess.run([str(python), '-m', 'PyInstaller', '--noconfirm', '--clean', '--onefile', '--name', 'esptool',
                        '--distpath', str(stage / 'dist'), '--workpath', str(stage / 'work'), '--specpath', str(stage),
                        '--collect-all', 'esptool', '--collect-submodules', 'rich._unicode_data',
                        '--collect-submodules', 'bitstring', '--collect-submodules', 'serial.urlhandler',
                        str(ROOT / 'scripts/esptool-entrypoint.py')], check=True, cwd=stage)
        binary = stage / 'dist' / ('esptool.exe' if os.name == 'nt' else 'esptool')
        subprocess.run([str(binary), 'version'], check=True)
        subprocess.run([str(binary), 'read-flash', '--help'], check=True, stdout=subprocess.DEVNULL)
        subprocess.run([str(binary), 'write-flash', '--help'], check=True, stdout=subprocess.DEVNULL)
        shutil.copy2(binary, args.output / binary.name)
        manifest_path = args.licenses / 'usb-python-source-manifest.json'
        manifest = json.loads(manifest_path.read_text())
        manifest['binary'] = dict(name=binary.name, sha256=hashlib.sha256(binary.read_bytes()).hexdigest())
        manifest['frozen_native_inputs'] = frozen_native_files(stage / 'work/esptool/Analysis-00.toc')
        manifest_path.write_text(json.dumps(manifest, indent=2) + '\n')
    # One self-contained companion source ZIP per platform. Publish next to the
    # USB binary ZIP, instead of nesting the large source archives inside it.
    source_zip = args.sources / 'Hallzee-USB-Python-Sources.zip'
    with zipfile.ZipFile(source_zip, 'w', zipfile.ZIP_STORED) as output:
        for path in sorted(args.sources.iterdir()):
            if path.is_file() and path != source_zip:
                output.write(path, path.name)
        for path in sorted(args.licenses.rglob('*')):
            if path.is_file():
                output.write(path, 'licenses/' + path.relative_to(args.licenses).as_posix())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--licenses', type=Path, required=True)
    parser.add_argument('--sources', type=Path, required=True)
    parser.add_argument('--collect', action='store_true', help=argparse.SUPPRESS)
    args = parser.parse_args()
    args.output, args.licenses, args.sources = (path.resolve() for path in (args.output, args.licenses, args.sources))
    try:
        (collect if args.collect else build)(args)
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        parser.error(str(error))


if __name__ == '__main__':
    main()
