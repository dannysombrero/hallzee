import importlib.util
import io
import json
from pathlib import Path
import unittest
import urllib.error

SPEC = importlib.util.spec_from_file_location(
    "cloudflare_preflight", Path(__file__).resolve().parents[1] / "scripts/cloudflare-preflight.py")
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class CloudflarePreflightTests(unittest.TestCase):
    def test_only_gets_to_cloudflare_and_no_sensitive_response_values(self):
        account = "a" * 32
        private = "synthetic-private-value-do-not-output"
        requests = []

        class FakeOpener:
            def open(self, request, timeout):
                requests.append(request)
                path = request.full_url.split(MODULE.API, 1)[1]
                if path.endswith("/projects/hallzee-web-client"):
                    result = {"name": MODULE.PROJECT, "subdomain": MODULE.PROJECT + ".pages.dev",
                              "production_branch": "main", "secrets": private}
                elif path.endswith("/workers/scripts"):
                    result = [{"id": MODULE.WORKER, "migration_tag": "v1", "private": private}]
                elif path.endswith("/settings"):
                    result = {"bindings": [{"type": "secret_text", "text": private}]}
                elif path.startswith("/zones?"):
                    result = [{"id": "b" * 32, "name": "hallzee.com", "account": {"id": account}}]
                else:
                    result = []
                return io.BytesIO(json.dumps({"success": True, "result": result}).encode())

        checks = MODULE.preflight(private, account, FakeOpener())
        self.assertEqual(len(requests), 9)
        self.assertTrue(all(r.get_method() == "GET" for r in requests))
        self.assertTrue(all(r.full_url.startswith(MODULE.API + "/") for r in requests))
        output = json.dumps(checks)
        self.assertNotIn(private, output)
        self.assertNotIn(account, output)
        self.assertNotIn("b" * 32, output)

    def test_http_failure_does_not_log_request_or_body(self):
        class FailingOpener:
            def open(self, request, timeout):
                raise urllib.error.HTTPError(request.full_url, 403, "private-error-detail", {},
                                             io.BytesIO(b"private-response-body"))
        checks = MODULE.preflight("synthetic-secret", "a" * 32, FailingOpener())
        self.assertTrue(all(c.get("http_status") == 403 for c in checks))
        self.assertNotIn("private", json.dumps(checks))
        self.assertNotIn("a" * 32, json.dumps(checks))

    def test_redirects_are_not_followed(self):
        handler = MODULE.NoRedirect()
        self.assertIsNone(handler.redirect_request(None, None, 302, "", {}, "https://example.com"))

    def test_missing_credentials_fail_before_network(self):
        checks = MODULE.preflight("", "")
        self.assertEqual(len(checks), 1)
        self.assertFalse(checks[0]["ok"])


if __name__ == "__main__":
    unittest.main()
