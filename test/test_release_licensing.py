"""Release regressions: wrong-target inventories and incomplete native sources."""
import importlib.util
import json
import io
from pathlib import Path
import tarfile
import tempfile
import unittest
from unittest.mock import patch
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def script(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / 'scripts' / (name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


inventory = script('generate-dependency-inventory')
firmware = script('bundle-firmware-licenses')
usb = script('build-usb-esptool')
usb_audit = script('audit-usb-python')


class InventoryTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.packages = self.root / 'packages'

    def package(self, name, version):
        path = self.packages / name.lower() / version
        path.mkdir(parents=True)
        (path / (name + '.nuspec')).write_text(
            f'<package><metadata><id>{name}</id><version>{version}</version>'
            '<license type="expression">MIT</license><copyright>Original Author</copyright>'
            '<repository url="https://github.com/example/library" commit="012345"/>'
            '</metadata></package>')
        return {'type': 'package', 'path': name.lower() + '/' + version}

    def assets(self):
        data = dict(libraries={'Shared/1.0.0': self.package('Shared', '1.0.0'),
                               'WindowsOnly/2.0.0': self.package('WindowsOnly', '2.0.0')},
                    targets={'net8.0/osx-arm64': {'Shared/1.0.0': {}},
                             'net8.0/win-x64': {'WindowsOnly/2.0.0': {}}},
                    packageFolders={str(self.packages): {}})
        path = self.root / 'project.assets.json'
        path.write_text(json.dumps(data))
        return path, data

    def test_selects_release_target_and_retains_original_metadata(self):
        path, _ = self.assets()
        records, _ = inventory.collect([path], False, 'osx-arm64')
        self.assertEqual(['Shared'], [record['name'] for record in records])
        self.assertEqual('Original Author', records[0]['copyright'])
        self.assertEqual('012345', records[0]['repository_commit'])

    def test_missing_restore_or_target_fails_instead_of_stale_inventory(self):
        with self.assertRaisesRegex(ValueError, 'assets file missing'):
            inventory.collect([self.root / 'missing.json'], False)
        path, _ = self.assets()
        with self.assertRaisesRegex(ValueError, 'no target'):
            inventory.collect([path], False, 'linux-x64')

    def test_runtime_download_and_workload_packs_are_included(self):
        path, data = self.assets()
        self.package('Microsoft.NETCore.App.Runtime.osx-arm64', '8.0.30')
        data['project'] = {'frameworks': {'net8.0': {'downloadDependencies': [
            {'name': 'Microsoft.NETCore.App.Runtime.osx-arm64', 'version': '[8.0.30, 8.0.30]'}]}}}
        path.write_text(json.dumps(data))
        pack = self.package('Microsoft.macOS.Runtime.osx-arm64.net8.0_15.0', '15.0.8319')
        frameworks = self.root / 'frameworks.json'
        frameworks.write_text(json.dumps({'Items': {'ResolvedFrameworkReference': [{
            'RuntimePackName': 'Microsoft.macOS.Runtime.osx-arm64.net8.0_15.0',
            'RuntimePackVersion': '15.0.8319', 'RuntimePackPath': str(self.packages / pack['path'])}]}}))
        records, _ = inventory.collect([path], False, 'osx-arm64', framework_references=[frameworks])
        self.assertEqual(3, len(records))
        self.assertTrue(any(record['name'].startswith('Microsoft.macOS') for record in records))


class FirmwareSourceTests(unittest.TestCase):
    def test_only_used_map_archive_members_count(self):
        text = ('Archive member included to satisfy reference by file (symbol)\n'
                '/sdk/lib/libjoltwallet__littlefs.a(lfs.c.o)\n'
                'Discarded input sections\nLOAD /sdk/lib/libnotlinked__component.a\n')
        self.assertEqual(['libjoltwallet__littlefs.a'], firmware.linked_archives(text))

    def test_new_managed_component_requires_source_coverage(self):
        with self.assertRaisesRegex(ValueError, 'newly linked SDK components'):
            firmware.check_managed_components(['libunknown__codec.a'], [])
        with self.assertRaisesRegex(ValueError, 'Unrecognized linker map'):
            firmware.linked_archives('LOAD libunknown__codec.a')

    def test_archive_preserves_internal_symlinks_but_excludes_git_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / 'source'
            source.mkdir()
            (source / '.git').mkdir()
            (source / '.git/config').write_text('not public metadata')
            (source / 'real.h').write_text('/* source */')
            (source / 'alias.h').symlink_to('real.h')
            target = root / 'source.tar.gz'
            firmware.archive_source(source, target, 'source')
            with tarfile.open(target) as archive:
                self.assertEqual(['source/alias.h', 'source/real.h'], archive.getnames())
                self.assertTrue(archive.getmember('source/alias.h').issym())
            (source / 'outside.h').symlink_to('../external.h')
            with self.assertRaisesRegex(ValueError, 'Unsafe source symlink'):
                list(firmware.source_files(source))


class UsbSourceTests(unittest.TestCase):
    def test_nonempty_source_or_license_directory_fails_before_installing(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            from argparse import Namespace
            args = Namespace(output=root / 'tool', licenses=root / 'licenses', sources=root / 'sources')
            for target in (args.licenses, args.sources):
                target.mkdir()
                stale = target / 'old-vulnerable-package.tar.gz'
                stale.write_bytes(b'stale')
                with patch.object(usb.sys, 'version_info', (3, 13)), \
                        patch.object(usb.platform, 'system', return_value='Darwin'), \
                        patch.object(usb.platform, 'machine', return_value='arm64'), \
                        patch.object(usb.venv, 'EnvBuilder') as builder:
                    with self.assertRaisesRegex(ValueError, 'empty licenses/sources'):
                        usb.build(args)
                    builder.assert_not_called()
                stale.unlink()

    def test_requirement_hashes_keep_platform_markers_out_of_version(self):
        requirements = usb.locked_requirements('example_pkg==1.2.3 ; sys_platform == "win32" ' + '\\' + '\n'
                                              + '    --hash=sha256:' + 'a' * 64 + '\n')
        self.assertEqual('1.2.3', requirements['example-pkg']['version'])
        self.assertEqual({'a' * 64}, requirements['example-pkg']['hashes'])

    def test_tar_and_zip_notices_do_not_extract_other_source_files(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            archive_path = root / 'package.tar.gz'
            with tarfile.open(archive_path, 'w:gz') as archive:
                for name in ['package/LICENSE', 'package/program.py']:
                    item = tarfile.TarInfo(name)
                    item.size = 4
                    archive.addfile(item, io.BytesIO(b'text'))
            self.assertEqual(1, usb.archive_notices(archive_path, root / 'notices'))
            self.assertFalse((root / 'notices/package/program.py').exists())
            with zipfile.ZipFile(root / 'package.zip', 'w') as archive:
                archive.writestr('package/LICENSE', 'terms')
            self.assertEqual(1, usb.archive_notices(root / 'package.zip', root / 'zip-notices'))

    def test_notice_path_traversal_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with zipfile.ZipFile(root / 'malicious.zip', 'w') as archive:
                archive.writestr('../LICENSE', 'outside')
            with self.assertRaisesRegex(ValueError, 'Unsafe source archive member'):
                usb.archive_notices(root / 'malicious.zip', root / 'notices')
            self.assertFalse((root / 'LICENSE').exists())

    def test_frozen_native_inputs_record_hashes_without_host_paths(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            binary = root / 'libpython.dylib'
            binary.write_bytes(b'binary')
            toc = root / 'Analysis-00.toc'
            toc.write_text(repr(('other', [('libpython.dylib', str(binary), 'BINARY')], [])))
            records = usb.frozen_native_files(toc)
            self.assertEqual(1, len(records))
            self.assertEqual('libpython.dylib', records[0]['name'])
            self.assertEqual(64, len(records[0]['sha256']))
            self.assertNotIn(directory, json.dumps(records))


class UsbAuditTests(unittest.TestCase):
    packages = [('example', '1.0.0')]

    def test_complete_empty_results_are_clear(self):
        for result in ({}, {'vulns': []}, {'vulns': [], 'next_page_token': ''}):
            self.assertEqual([], usb_audit.parse_results({'results': [result]}, self.packages))

    def test_advisories_are_reported_against_the_locked_package(self):
        advisories = [{'id': 'GHSA-example', 'modified': '2026-09-09T00:00:00Z'}]
        self.assertEqual([dict(name='example', version='1.0.0', advisories=advisories)],
                         usb_audit.parse_results({'results': [{'vulns': advisories}]}, self.packages))

    def test_errors_malformed_and_incomplete_results_never_pass(self):
        payloads = [None, [], {}, {'error': 'Unavailable'}, {'results': {}}, {'results': []},
                    {'results': [{}, {}]}, {'results': [None]}, {'results': [[]]},
                    {'results': [{'error': 'Unavailable'}]}, {'results': [{'unexpected': 'schema'}]},
                    {'results': [{'vulns': None}]}, {'results': [{'vulns': {}}]},
                    {'results': [{'vulns': [None]}]}, {'results': [{'vulns': [{}]}]},
                    {'results': [{'vulns': [{'id': ''}]}]}, {'results': [{'vulns': [{'id': 123}]}]},
                    {'results': [{'next_page_token': 'more'}]}, {'results': [{'next_page_token': None}]}]
        for payload in payloads:
            with self.subTest(payload=payload), self.assertRaises(ValueError):
                usb_audit.parse_results(payload, self.packages)


if __name__ == '__main__':
    unittest.main()
