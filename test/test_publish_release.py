"""Offline checks: publishing must promote tested files and fail closed."""
import importlib.util
import json
import os
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('publish_release', Path(__file__).parents[1] / 'scripts/publish-release.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class PublishTests(unittest.TestCase):
    def run_publish(self, *, conclusion='success', private=False, missing=False, existing=False, bad_sha=False, flat_windows=False, missing_windows=False):
        calls = []
        source = destination = 'school/source'

        def fake_gh(*args, payload=None, output_file=None):
            calls.append((args, payload))
            if args[:2] == ('api', f'repos/{source}/actions/runs/123'):
                return json.dumps(dict(conclusion=conclusion, event='workflow_dispatch',
                                       path='.github/workflows/release-client.yml', head_sha='abc'))
            if args[:2] == ('run', 'download'):
                root = Path(args[-1])
                metadata = root / 'Release-metadata'
                metadata.mkdir()
                (metadata / 'release.json').write_text(json.dumps(dict(product='client', version='1.2.3', repository=destination, sha='bad' if bad_sha else 'abc')))
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
                return json.dumps(dict(private=private))
            if args[:2] == ('api', f'repos/{destination}/git/matching-refs/tags/client-v1.2.3'):
                return json.dumps([dict(ref='refs/tags/client-v1.2.3')] if existing else [])
            if args[:2] == ('api', f'repos/{destination}/readme'):
                return json.dumps(dict(path='README.md', sha='readme-sha'))
            return ''

        with patch.object(module, 'gh', fake_gh), patch.dict(os.environ, GITHUB_REPOSITORY=source), patch.object(sys, 'argv', ['publish-release.py', '--run', '123', '--product', 'client']):
            if conclusion != 'success' or private or missing or existing or bad_sha or missing_windows:
                with self.assertRaises(SystemExit):
                    module.main()
                self.assertFalse(any(args[:2] == ('release', 'create') for args, _ in calls))
            else:
                module.main()
                create = next(i for i, (args, _) in enumerate(calls) if args[:2] == ('release', 'create'))
                edit = next(i for i, (args, _) in enumerate(calls) if args[:2] == ('release', 'edit'))
                write = next(i for i, (_, payload) in enumerate(calls) if payload)
                self.assertLess(create, write)
                self.assertLess(write, edit)
                self.assertIn('--draft', calls[create][0])
                self.assertIn('--draft=false', calls[edit][0])
                self.assertFalse(any('clobber' in arg for args, _ in calls for arg in args))

    def test_promotes_complete_draft_after_guide(self): self.run_publish()
    def test_promotes_original_flat_windows_archive(self): self.run_publish(flat_windows=True)
    def test_missing_windows_artifact_never_publishes(self): self.run_publish(missing_windows=True)
    def test_failed_build_never_publishes(self): self.run_publish(conclusion='failure')
    def test_private_destination_never_publishes(self): self.run_publish(private=True)
    def test_missing_mac_asset_never_publishes(self): self.run_publish(missing=True)
    def test_existing_version_never_overwritten(self): self.run_publish(existing=True)
    def test_mismatched_source_never_publishes(self): self.run_publish(bad_sha=True)


if __name__ == '__main__':
    unittest.main()
