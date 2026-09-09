"""Offline checks: publishing must promote tested files and fail closed."""
import importlib.util
import hashlib
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
import warnings
import zipfile
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('publish_release', Path(__file__).parents[1] / 'scripts/publish-release.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class PublishTests(unittest.TestCase):
    def run_publish(self, *, conclusion='success', private=False, missing=False, existing=False, bad_sha=False, flat_windows=False, missing_windows=False, branch='main', head_repo='school/source', comparison='ahead'):
        calls = []
        source = destination = 'school/source'

        def fake_gh(*args, payload=None, output_file=None):
            calls.append((args, payload))
            if args[:2] == ('api', f'repos/{source}/actions/runs/123'):
                return json.dumps(dict(conclusion=conclusion, event='workflow_dispatch',
                                       path='.github/workflows/release-client.yml', head_sha='a' * 40,
                                       head_branch=branch, head_repository=dict(full_name=head_repo)))
            if args[:2] == ('api', f'repos/{source}/compare/{"a" * 40}...main'):
                return json.dumps(dict(status=comparison))
            if args[:2] == ('run', 'download'):
                root = Path(args[-1])
                metadata = root / 'Release-metadata'
                metadata.mkdir()
                (metadata / 'release.json').write_text(json.dumps(dict(product='client', version='1.2.3', repository=destination, sha='bad' if bad_sha else 'a' * 40)))
                (metadata / 'teacher-guide.md').write_text('Guide: https://github.com/dannysombrero/hallzee-mono#readme')
                (metadata / 'notes.md').write_text('Notes')
                for rid, name in [('win-x64', 'Hallzee-Windows-win-x64.zip'), ('osx-arm64', 'Hallzee-Mac-osx-arm64.zip'), ('osx-x64', 'Hallzee-Mac-osx-x64.zip')]:
                    if rid == 'win-x64' and (flat_windows or missing_windows):
                        continue
                    folder = root / f'Desktop-{rid}'
                    folder.mkdir()
                    if not missing or rid != 'osx-x64':
                        (folder / name).write_bytes(b'tested bytes')
                return ''
            if args[:2] == ('api', f'repos/{source}/actions/runs/123/artifacts'):
                return '' if missing_windows else '456\n'
            if args[:2] == ('api', f'repos/{source}/actions/artifacts/456/zip'):
                output_file.write_bytes(b'original artifact archive bytes')
                return ''
            if args[:2] == ('release', 'create') and flat_windows:
                windows = next(Path(arg) for arg in args if arg.endswith('Hallzee-Windows-win-x64.zip'))
                self.assertEqual(windows.read_bytes(), b'original artifact archive bytes')
            if args[:2] == ('api', f'repos/{destination}'):
                return json.dumps(dict(private=private, default_branch='main'))
            if args[:2] == ('api', f'repos/{destination}/git/matching-refs/tags/client-v1.2.3'):
                return json.dumps([dict(ref='refs/tags/client-v1.2.3')] if existing else [])
            if args[:2] == ('api', f'repos/{destination}/readme'):
                return json.dumps(dict(path='README.md', sha='readme-sha'))
            return ''

        with patch.object(module, 'gh', fake_gh), patch.dict(os.environ, GITHUB_REPOSITORY=source), patch.object(sys, 'argv', ['publish-release.py', '--run', '123', '--product', 'client']):
            if conclusion != 'success' or private or missing or existing or bad_sha or missing_windows or branch != 'main' or head_repo != source or comparison not in ['ahead', 'identical']:
                with self.assertRaises(SystemExit):
                    module.main()
                self.assertFalse(any(args[:2] == ('release', 'create') for args, _ in calls))
            else:
                module.main()
                create = next(i for i, (args, _) in enumerate(calls) if args[:2] == ('release', 'create'))
                edit = next(i for i, (args, _) in enumerate(calls) if args[:2] == ('release', 'edit'))
                self.assertLess(create, edit)
                self.assertFalse(any(payload for _, payload in calls))
                self.assertFalse(any('/contents/' in arg or arg.endswith('/readme') for args, _ in calls for arg in args))
                target = calls[create][0].index('--target')
                self.assertEqual(calls[create][0][target + 1], 'a' * 40)
                self.assertIn('--draft', calls[create][0])
                self.assertIn('--draft=false', calls[edit][0])
                self.assertFalse(any('clobber' in arg for args, _ in calls for arg in args))

    def test_promotes_tested_commit_without_overwriting_readme(self): self.run_publish()
    def test_promotes_original_flat_windows_archive(self): self.run_publish(flat_windows=True)
    def test_missing_windows_artifact_never_publishes(self): self.run_publish(missing_windows=True)
    def test_failed_build_never_publishes(self): self.run_publish(conclusion='failure')
    def test_private_destination_never_publishes(self): self.run_publish(private=True)
    def test_missing_mac_asset_never_publishes(self): self.run_publish(missing=True)
    def test_existing_version_never_overwritten(self): self.run_publish(existing=True)
    def test_mismatched_source_never_publishes(self): self.run_publish(bad_sha=True)
    def test_feature_branch_never_publishes(self): self.run_publish(branch='unreviewed')
    def test_fork_artifacts_never_publish(self): self.run_publish(head_repo='someone/fork')
    def test_rewritten_history_requires_new_build(self): self.run_publish(comparison='diverged')
    def test_current_default_branch_commit_can_publish(self): self.run_publish(comparison='identical')


class FirmwareSourceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / 'licenses').mkdir()
        (self.root / 'sources').mkdir()
        (self.root / 'Hallzee-Firmware-Licenses.zip').write_bytes(b'license archive')
        for scope in ['firmware', 'usb']:
            path = self.root / 'sources' / f'{scope}.tar.gz'
            path.write_bytes(f'corresponding {scope} source'.encode())
            manifest = dict(scope=scope, sources=[dict(name=scope, archive=path.name, sha256=module.sha256(path))])
            (self.root / 'licenses' / f'{scope}-source-manifest.json').write_text(json.dumps(manifest))

    def test_attaches_both_source_sets_and_notices(self):
        assets = module.firmware_sources(self.root)
        self.assertEqual({p.name for p in assets}, {'firmware.tar.gz', 'usb.tar.gz', 'Hallzee-Firmware-Licenses.zip'})

    def test_altered_source_never_publishes(self):
        (self.root / 'sources' / 'usb.tar.gz').write_bytes(b'changed after build')
        with self.assertRaisesRegex(SystemExit, 'altered'):
            module.firmware_sources(self.root)

    def test_missing_scope_never_publishes(self):
        (self.root / 'licenses' / 'usb-source-manifest.json').unlink()
        with self.assertRaisesRegex(SystemExit, 'missing'):
            module.firmware_sources(self.root)

    def test_missing_notices_never_publishes(self):
        (self.root / 'Hallzee-Firmware-Licenses.zip').unlink()
        with self.assertRaisesRegex(SystemExit, 'license archive'):
            module.firmware_sources(self.root)

    def test_source_path_must_stay_in_source_directory(self):
        manifest = self.root / 'licenses' / 'usb-source-manifest.json'
        data = json.loads(manifest.read_text())
        data['sources'][0]['archive'] = '../usb.tar.gz'
        manifest.write_text(json.dumps(data))
        with self.assertRaisesRegex(SystemExit, 'Invalid archive'):
            module.firmware_sources(self.root)

    def test_cannot_replace_shipped_source_with_reference_only(self):
        manifest = self.root / 'licenses' / 'usb-source-manifest.json'
        manifest.write_text(json.dumps(dict(scope='usb', sources=[dict(name='esptool', reference_only=True)])))
        with self.assertRaisesRegex(SystemExit, 'Invalid archive'):
            module.firmware_sources(self.root)


def write_usb_pair(folder, rid, change=None):
    """Tiny real ZIPs, with distinct native executables and sources per platform."""
    folder.mkdir(parents=True, exist_ok=True)
    executable = 'esptool.exe' if rid == 'win-x64' else 'esptool'
    binary = ('tested executable for ' + rid).encode()
    source_name = 'esptool-5.3.1.tar.gz'
    source = ('corresponding source for ' + rid).encode()
    interpreter_name = 'Python-3.13.7.tar.xz'
    interpreter = b'corresponding CPython source'
    manifest = dict(scope='usb-python', python='3.13.7',
                    platform='Windows' if rid == 'win-x64' else 'Darwin',
                    architecture='AMD64' if rid == 'win-x64' else 'arm64',
                    binary=dict(name=executable, sha256=hashlib.sha256(binary).hexdigest()),
                    sources=[dict(name='esptool', archive=source_name, sha256=hashlib.sha256(source).hexdigest()),
                             dict(name='CPython', archive=interpreter_name, sha256=hashlib.sha256(interpreter).hexdigest())])
    if change and change[0] == 'wrong-target':
        manifest['platform'], manifest['architecture'] = 'Linux', 'x86_64'
    if change and change[0] == 'unsafe-source-path':
        source_name = '../' + source_name
        manifest['sources'][0]['archive'] = source_name
    manifest_bytes = json.dumps(manifest).encode()
    entries = {
        'source': [('licenses/usb-python-source-manifest.json', manifest_bytes),
                   (source_name, source), (interpreter_name, interpreter)],
        'binary': [('licenses/python/usb-python-source-manifest.json', manifest_bytes),
                   ('tool/' + executable, binary)],
    }
    members = {
        'source-manifest': ('source', 'licenses/usb-python-source-manifest.json'),
        'binary-manifest': ('binary', 'licenses/python/usb-python-source-manifest.json'),
        'source-archive': ('source', source_name),
        'executable': ('binary', 'tool/' + executable),
    }
    if change and change[0] in ('missing-member', 'duplicate-member', 'tampered-member'):
        operation, member = change
        group, name = members[member]
        original = next(pair for pair in entries[group] if pair[0] == name)
        entries[group] = [pair for pair in entries[group] if pair[0] != name]
        if operation == 'duplicate-member':
            entries[group].extend([original, original])
        elif operation == 'tampered-member':
            entries[group].append((name, b'changed after the successful build'))
    if change and change[0] == 'mismatched-manifests':
        entries['binary'][0] = (entries['binary'][0][0], json.dumps(dict(manifest, python='3.13.8')).encode())
    files = {'source': folder / f'Hallzee-USB-Python-Sources-{rid}.zip',
             'binary': folder / f'Hallzee-USB-{rid}.zip'}
    for group, path in files.items():
        if change == ('missing-archive', group):
            continue
        if change == ('invalid-zip', group):
            path.write_bytes(b'not a ZIP archive')
            continue
        with warnings.catch_warnings():
            warnings.filterwarnings('ignore', message='Duplicate name:', category=UserWarning)
            with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as archive:
                for name, data in entries[group]:
                    archive.writestr(name, data)
    return files


class FirmwarePublishTests(unittest.TestCase):
    def run_firmware_publish(self, *, rid='osx-arm64', change=None):
        calls, uploaded, original_artifacts = [], {}, {}
        source, commit = 'school/source', 'b' * 40

        def fake_gh(*args, payload=None, output_file=None):
            calls.append(args)
            if args == ('api', f'repos/{source}'):
                return json.dumps(dict(private=False, default_branch='main'))
            if args == ('api', f'repos/{source}/actions/runs/456'):
                return json.dumps(dict(conclusion='success', event='workflow_dispatch',
                                       path='.github/workflows/release-firmware.yml',
                                       head_sha=commit, head_branch='main',
                                       head_repository=dict(full_name=source)))
            if args == ('api', f'repos/{source}/compare/{commit}...main'):
                return json.dumps(dict(status='identical'))
            if args[:2] == ('run', 'download'):
                root = Path(args[-1])
                metadata = root / 'Release-metadata'
                metadata.mkdir()
                (metadata / 'release.json').write_text(json.dumps(dict(product='firmware', version='1.2.3', repository=source, sha=commit)))
                (metadata / 'teacher-guide.md').write_text('Teacher instructions')
                (metadata / 'notes.md').write_text('Firmware release notes')
                firmware = root / 'Firmware-and-USB-images'
                (firmware / 'sources').mkdir(parents=True)
                (firmware / 'licenses').mkdir()
                (firmware / 'Hallzee-Firmware-1.2.3.hallzee-fw').write_bytes(b'exact tested signed firmware')
                (firmware / 'Hallzee-Firmware-Licenses.zip').write_bytes(b'exact tested firmware notices')
                for scope in ['firmware', 'usb']:
                    path = firmware / 'sources' / f'{scope}.tar.gz'
                    content = ('exact source ' + scope).encode()
                    path.write_bytes(content)
                    manifest = dict(scope=scope, sources=[dict(name=scope, archive=path.name, sha256=hashlib.sha256(content).hexdigest())])
                    (firmware / 'licenses' / f'{scope}-source-manifest.json').write_text(json.dumps(manifest))
                for target in ['win-x64', 'osx-arm64']:
                    write_usb_pair(root / f'USB-{target}', target, change if target == rid else None)
                original_artifacts.update({path.name: path.read_bytes() for path in root.rglob('*')
                                           if path.is_file() and path.name.endswith(('.zip', '.tar.gz', '.hallzee-fw'))})
                return ''
            if args == ('api', f'repos/{source}/git/matching-refs/tags/firmware-v1.2.3'):
                return '[]'
            if args[:2] == ('release', 'create'):
                self.assertEqual('firmware-v1.2.3', args[2])
                self.assertEqual(commit, args[args.index('--target') + 1])
                self.assertIn('--draft', args)
                for path in map(Path, args[3:args.index('--repo')]):
                    self.assertNotIn(path.name, uploaded, 'Release asset names must be distinct across platforms')
                    uploaded[path.name] = path.read_bytes()
                notes = Path(args[args.index('--notes-file') + 1]).read_text()
                self.assertIn(f'https://github.com/{source}/archive/{commit}.zip', notes)
                return ''
            if args[:2] == ('release', 'edit'):
                self.assertIn('--draft=false', args)
                return ''
            self.fail(f'Unexpected GitHub operation: {args!r}')

        with patch.object(module, 'gh', fake_gh), patch.dict(os.environ, GITHUB_REPOSITORY=source), patch.object(sys, 'argv', ['publish-release.py', '--run', '456', '--product', 'firmware']):
            if change:
                with self.assertRaises(SystemExit):
                    module.main()
                self.assertFalse(any(args[0] == 'release' for args in calls),
                                 'Invalid binaries or source companions must fail before creating even a draft')
            else:
                module.main()
                self.assertEqual({'Hallzee-Firmware-1.2.3.hallzee-fw', 'Hallzee-Firmware-Licenses.zip',
                                  'firmware.tar.gz', 'usb.tar.gz', 'Hallzee-USB-win-x64.zip',
                                  'Hallzee-USB-osx-arm64.zip', 'Hallzee-USB-Python-Sources-win-x64.zip',
                                  'Hallzee-USB-Python-Sources-osx-arm64.zip', 'Hallzee-Teacher-Guide.md',
                                  'SHA256SUMS.txt'}, set(uploaded))
                for name, data in original_artifacts.items():
                    self.assertEqual(data, uploaded[name], f'Publication must not alter {name}')
                checksums = dict(line.split('  ', 1)[::-1] for line in uploaded['SHA256SUMS.txt'].decode().splitlines())
                self.assertEqual(set(uploaded) - {'SHA256SUMS.txt'}, set(checksums))
                for name, digest in checksums.items():
                    self.assertEqual(hashlib.sha256(uploaded[name]).hexdigest(), digest)
                self.assertLess(next(i for i, args in enumerate(calls) if args[:2] == ('release', 'create')),
                                next(i for i, args in enumerate(calls) if args[:2] == ('release', 'edit')))

    def test_verified_usb_source_pairs_have_distinct_platform_filenames(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            companions = []
            for rid in ['win-x64', 'osx-arm64']:
                write_usb_pair(root, rid)
                companions.append(module.usb_python_sources(root, rid).name)
            self.assertEqual(['Hallzee-USB-Python-Sources-win-x64.zip',
                              'Hallzee-USB-Python-Sources-osx-arm64.zip'], companions)

    def test_complete_firmware_release_promotes_exact_binaries_sources_and_checksums(self):
        self.run_firmware_publish()

    def test_missing_binary_or_source_pair_never_creates_release(self):
        for rid in ['win-x64', 'osx-arm64']:
            for group in ['source', 'binary']:
                with self.subTest(rid=rid, missing=group):
                    self.run_firmware_publish(rid=rid, change=('missing-archive', group))

    def test_tampered_executable_or_corresponding_source_never_creates_release(self):
        for rid in ['win-x64', 'osx-arm64']:
            for member in ['source-archive', 'executable']:
                with self.subTest(rid=rid, tampered=member):
                    self.run_firmware_publish(rid=rid, change=('tampered-member', member))

    def test_binary_and_source_manifests_must_match(self):
        self.run_firmware_publish(change=('mismatched-manifests', None))

    def test_missing_or_duplicate_members_never_create_release(self):
        for operation in ['missing-member', 'duplicate-member']:
            for member in ['source-manifest', 'binary-manifest', 'source-archive', 'executable']:
                with self.subTest(operation=operation, member=member):
                    self.run_firmware_publish(change=(operation, member))

    def test_source_archive_path_cannot_escape_its_companion(self):
        self.run_firmware_publish(change=('unsafe-source-path', None))

    def test_self_consistent_wrong_platform_pair_never_creates_release(self):
        for rid in ['win-x64', 'osx-arm64']:
            with self.subTest(rid=rid):
                self.run_firmware_publish(rid=rid, change=('wrong-target', None))

    def test_invalid_zip_never_creates_release(self):
        for group in ['source', 'binary']:
            with self.subTest(invalid=group):
                self.run_firmware_publish(change=('invalid-zip', group))


if __name__ == '__main__':
    unittest.main()
