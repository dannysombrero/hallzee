"""Cross-runner dependency graphs must retain real repository manifest paths."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location('submission', ROOT / 'scripts/prepare-dependency-submission.py')
submission = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(submission)


class DependencySubmissionTests(unittest.TestCase):
    def test_rebase_handles_windows_paths_and_reference_keys_with_boundary_guard(self):
        value = {'D:\\a\\repo\\Core.csproj': ['D:\\a\\repo\\obj', 'D:/a/repository/not-ours']}
        actual = submission.rebase(value, 'D:/a/repo', '/checkout')
        self.assertEqual({'/checkout/Core.csproj': ['/checkout/obj', 'D:/a/repository/not-ours']}, actual)

    def fixture(self, root, platform):
        for relative in submission.PROJECTS[platform]:
            project = root / relative
            project.parent.mkdir(parents=True, exist_ok=True)
            project.write_text('<Project />')
            assets = project.parent / 'obj/project.assets.json'
            assets.parent.mkdir(exist_ok=True)
            assets.write_text(json.dumps({'targets': {'net8.0': {'Example/1.0.0': {}}},
                'libraries': {'Example/1.0.0': {'type': 'package'}},
                'project': {'frameworks': {'net8.0': {
                    'dependencies': {'Example': {'target': 'Package', 'version': '1.0.0'}},
                    'runtimeIdentifierGraphPath': '/runner/dotnet/RuntimeIdentifierGraph.json'}},
                    'restore': {'projectPath': project.as_posix(),
                    'projectUniqueName': project.as_posix(), 'packagesPath': '/runner/cache/packages',
                    'configFilePaths': ['/runner/config/NuGet.Config']}}}))

    def test_graphs_move_between_workspaces_without_artifact_manifest_paths(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            old, new, staged = root / 'old', root / 'new', root / 'graphs'
            self.fixture(old, 'macos')
            self.fixture(new, 'macos')
            self.assertEqual(4, submission.stage(old, 'macos', staged))
            self.assertEqual(4, submission.activate(new, 'macos', staged))
            for relative in submission.PROJECTS['macos']:
                staged_text = (staged / relative / 'project.assets.json').read_text()
                self.assertNotIn(str(old), staged_text)
                self.assertNotIn('/runner/', staged_text)
                assets = json.loads((new / Path(relative).parent / 'obj/project.assets.json').read_text())
                self.assertEqual((new / relative).as_posix(), assets['project']['restore']['projectPath'])
                self.assertEqual({'Example/1.0.0': {'type': 'package'}}, assets['libraries'])

    def test_unexpected_project_or_tampered_graph_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            source, staged = root / 'repo', root / 'graphs'
            self.fixture(source, 'windows')
            submission.stage(source, 'windows', staged)
            with self.assertRaisesRegex(ValueError, 'platform mismatch'):
                submission.activate(source, 'macos', staged)
            asset = staged / submission.PROJECTS['windows'][0] / 'project.assets.json'
            asset.write_text('{}')
            with self.assertRaisesRegex(ValueError, 'checksum mismatch'):
                submission.activate(source, 'windows', staged)

    def test_portable_artifact_inside_scanned_checkout_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            self.fixture(root, 'macos')
            staged = root / 'dependency-graphs'
            submission.stage(root, 'macos', staged)
            with self.assertRaisesRegex(ValueError, 'outside the repository scan tree'):
                submission.activate(root, 'macos', staged)

    def test_project_path_must_stay_in_checkout(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            for relative in ('../outside.csproj', '/outside.csproj', 'missing.csproj'):
                with self.subTest(relative=relative), self.assertRaises(ValueError):
                    submission.project_path(root, relative)
            with self.assertRaisesRegex(ValueError, 'outside its expected'):
                submission.validate_assets({'targets': {'net8.0': {}}, 'project': {'restore': {
                    'projectPath': '/outside/elsewhere.csproj'}}}, '/checkout/expected.csproj')

    def test_official_detector_paths_must_match_real_projects(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            self.fixture(root, 'macos')
            report = {
                'resultCode': 'Success',
                'dependencyGraphs': {str(root / path): {} for path in submission.PROJECTS['macos']},
                'componentsFound': [{'component': {'packageUrl': {'Type': 'nuget', 'Name': 'Example', 'Version': '1.0.0'}},
                    'locationsFoundAt': ['/' + path for path in submission.PROJECTS['macos']]}],
            }
            scan = root / 'scan.json'
            scan.write_text(json.dumps(report))
            self.assertEqual(4, submission.verify_scan(root, 'macos', scan))
            report['resultCode'] = 'Error'
            scan.write_text(json.dumps(report))
            with self.assertRaisesRegex(ValueError, 'did not report a successful scan'):
                submission.verify_scan(root, 'macos', scan)
            report['resultCode'] = 'Success'
            report['componentsFound'][0]['locationsFoundAt'][0] = '/tmp/artifact/wrong.csproj'
            scan.write_text(json.dumps(report))
            with self.assertRaisesRegex(ValueError, 'not a repository project'):
                submission.verify_scan(root, 'macos', scan)

    def test_project_reference_only_root_can_be_omitted_without_losing_packages(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            self.fixture(root, 'macos')
            reference_only = 'tools/FirmwareTool/FirmwareTool.csproj'
            assets_path = root / Path(reference_only).parent / 'obj/project.assets.json'
            assets = json.loads(assets_path.read_text())
            assets['project']['frameworks']['net8.0']['dependencies'] = {}
            assets_path.write_text(json.dumps(assets))
            package_projects = set(submission.PROJECTS['macos']) - {reference_only}
            report = {'resultCode': 'Success',
                'dependencyGraphs': {str(root / path): {} for path in package_projects},
                'componentsFound': [{'component': {'packageUrl': {
                    'Type': 'nuget', 'Name': 'Example', 'Version': '1.0.0'}},
                    'locationsFoundAt': sorted(package_projects)}]}
            scan = root / 'scan.json'
            scan.write_text(json.dumps(report))
            self.assertEqual(3, submission.verify_scan(root, 'macos', scan))
            assets['libraries']['Lost.Transitive/2.0.0'] = {'type': 'package'}
            assets_path.write_text(json.dumps(assets))
            with self.assertRaisesRegex(ValueError, 'omitted restored packages: lost.transitive/2.0.0'):
                submission.verify_scan(root, 'macos', scan)


if __name__ == '__main__':
    unittest.main()
