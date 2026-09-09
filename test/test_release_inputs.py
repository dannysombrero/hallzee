import importlib.util
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SCRIPT = Path(__file__).parents[1] / 'scripts/validate-release-inputs.py'
spec = importlib.util.spec_from_file_location('release_inputs', SCRIPT)
inputs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(inputs)


class ReleaseInputTests(unittest.TestCase):
    def test_common_version_forms_produce_same_build_and_tag_version(self):
        for value in ['1.0.0', 'v1.0.0', '1.0', 'v1.0', '  v1.0.0  ']:
            with self.subTest(value=value):
                self.assertEqual(inputs.normalize_version(value), '1.0.0')
        self.assertEqual(inputs.normalize_version('1.10.12'), '1.10.12')
        self.assertEqual(inputs.normalize_version('999.999.999'), '999.999.999')

    def test_invalid_versions_have_actionable_errors(self):
        for value in ['', '1', '1.0.0-beta', '01.0.0', '1.0.0.0', '1000.0.0', '1x0x0', '1.0\nrepository=other/repo']:
            with self.subTest(value=value), self.assertRaisesRegex(ValueError, 'Invalid release version.*Use 1.0.0'):
                inputs.normalize_version(value)

    def test_repository_requires_owner_and_name(self):
        self.assertEqual(inputs.normalize_repository(' school/hallzee-releases '), 'school/hallzee-releases')
        for value in ['', 'https://github.com/school/hallzee', 'school/hallzee/', 'school', 'school/repo\nextra=value']:
            with self.subTest(value=value), self.assertRaisesRegex(ValueError, 'owner/repository'):
                inputs.normalize_repository(value)

    def test_cli_emits_normalized_job_outputs(self):
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / 'output'
            result = subprocess.run([sys.executable, str(SCRIPT)], capture_output=True, text=True,
                env={**os.environ, 'RELEASE_VERSION': ' v1.0 ', 'RELEASE_REPOSITORY': 'school/hallzee', 'GITHUB_OUTPUT': str(output)})
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(output.read_text(), 'version=1.0.0\nrepository=school/hallzee\n')

    def test_cli_fails_without_partial_outputs(self):
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / 'output'
            result = subprocess.run([sys.executable, str(SCRIPT)], capture_output=True, text=True,
                env={**os.environ, 'RELEASE_VERSION': '1.0.0', 'RELEASE_REPOSITORY': 'https://github.com/school/hallzee', 'GITHUB_OUTPUT': str(output)})
            self.assertEqual(result.returncode, 1)
            self.assertIn('owner/repository', result.stderr)
            self.assertNotIn('AssertionError', result.stderr)
            self.assertFalse(output.exists())


if __name__ == '__main__':
    unittest.main()
