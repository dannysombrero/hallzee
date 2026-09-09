"""Automated test suite to enforce repository hygiene and prevent secret leaks."""
import pathlib
import re
import subprocess
import unittest

ROOT = pathlib.Path(__file__).resolve().parent.parent

FORBIDDEN_FILE_PATTERNS = [
    (re.compile(r'(^|/)\.DS_Store$'), ".DS_Store file"),
    (re.compile(r'^Library/'), "Host Library/ directory"),
    (re.compile(r'TestResults'), "TestResults / coverage output"),
    (re.compile(r'\.cobertura\.xml$'), "Cobertura coverage file"),
    (re.compile(r'(^|/)\.local/'), "Local user state directory (.local/)"),
    (re.compile(r'^receiver/.*\.csv$'), "Trip or student CSV export"),
    (re.compile(r'(^|/)\.openai/'), "OpenAI hosting metadata"),
    (re.compile(r'(^|/)\.env(\..+)?$'), "Environment / secret file (.env)"),
]

ALLOWED_KEY_FILES = {
    "firmware/release-public-key.pem",
}

KEY_FILE_EXTENSION_PATTERN = re.compile(r'\.(pem|key|pfx|p12|pkcs12)$', re.IGNORECASE)
PRIVATE_KEY_CONTENT_PATTERN = re.compile(r'-----BEGIN [A-Z0-9 ]*PRIVATE KEY-----', re.IGNORECASE)


class TestRepositoryHygiene(unittest.TestCase):
    def get_tracked_files(self):
        result = subprocess.run(
            ["git", "ls-files"],
            cwd=ROOT,
            capture_output=True,
            text=True,
            check=True
        )
        return [line.strip() for line in result.stdout.splitlines() if line.strip()]

    def test_no_forbidden_files_tracked(self):
        tracked = self.get_tracked_files()
        violations = []
        for path in tracked:
            for pattern, desc in FORBIDDEN_FILE_PATTERNS:
                if pattern.search(path):
                    violations.append(f"{path} ({desc})")
        self.assertEqual([], violations, f"Forbidden files currently tracked in git:\n" + "\n".join(violations))

    def test_only_allowed_public_keys_tracked(self):
        tracked = self.get_tracked_files()
        key_files = [p for p in tracked if KEY_FILE_EXTENSION_PATTERN.search(p)]
        for k in key_files:
            self.assertIn(
                k,
                ALLOWED_KEY_FILES,
                f"Untrusted cryptographic key file tracked in Git: {k}. Only {ALLOWED_KEY_FILES} permitted."
            )

    def test_no_private_key_content_in_tracked_files(self):
        tracked = self.get_tracked_files()
        violations = []
        for rel_path in tracked:
            full_path = ROOT / rel_path
            if not full_path.is_file():
                continue
            # Skip large files or binaries
            if full_path.stat().st_size > 1024 * 1024:
                continue
            try:
                content = full_path.read_text(encoding='utf-8', errors='ignore')
            except Exception:
                continue
            if PRIVATE_KEY_CONTENT_PATTERN.search(content):
                violations.append(rel_path)
        self.assertEqual([], violations, f"Tracked files contain private key material:\n" + "\n".join(violations))

    def test_gitignore_contains_critical_safeguards(self):
        gitignore = (ROOT / ".gitignore").read_text(encoding='utf-8')
        required_patterns = [
            ".DS_Store",
            "Library/",
            "TestResults",
            ".local",
            "*.pem",
            "*.key",
            "!firmware/release-public-key.pem",
            "receiver/*.csv",
            ".env",
        ]
        for pat in required_patterns:
            self.assertIn(
                pat,
                gitignore,
                f"Missing safety pattern in .gitignore: {pat}"
            )


if __name__ == '__main__':
    unittest.main()
