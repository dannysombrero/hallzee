import importlib.util
import contextlib
import io
from pathlib import Path
import unittest
import urllib.error

SPEC = importlib.util.spec_from_file_location(
    "cloudflare_bind_join", Path(__file__).resolve().parents[1] / "scripts/cloudflare-bind-join.py")
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class FakeCloudflare:
    account = "a" * 32

    def __init__(self, existing=False, conflict=False, wrong_account=False):
        self.calls = []
        self.domains = [{"name": "web.hallzee.com", "status": "active"}]
        self.records = []
        self.relay_domains = []
        self.relay_records = []
        self.relay_bindings = [{"name": "ROOM_DO", "type": "durable_object_namespace",
                                "class_name": "RoomDurableObject"}]
        self.wrong_account = wrong_account
        if existing:
            self.domains.append({"name": MODULE.HOST, "status": "active"})
            self.records = [{"name": MODULE.HOST, "type": "CNAME", "content": MODULE.TARGET}]
        if conflict:
            self.records = [{"name": MODULE.HOST, "type": "A", "content": "192.0.2.1"}]

    def request(self, method, path, body=None):
        self.calls.append((method, path, body))
        if path.endswith("/workers/domains") and method == "PUT":
            self.relay_domains.append(body)
            return body
        if "/workers/domains?" in path:
            return list(self.relay_domains)
        if path.endswith("/settings"):
            return {"bindings": self.relay_bindings}
        if path.endswith("/dns_records?name=" + MODULE.RELAY_HOST):
            return list(self.relay_records)
        if method == "POST":
            if path.endswith("/domains"):
                self.domains.append({"name": body["name"], "status": "pending"})
            else:
                self.records.append(body)
            return {}
        if path.endswith("/projects/" + MODULE.PROJECT):
            return {"name": MODULE.PROJECT, "subdomain": MODULE.TARGET, "production_branch": "main"}
        if path.endswith("/domains"):
            return list(self.domains)
        if path.startswith("/zones?"):
            return [{"id": "b" * 32, "name": "hallzee.com", "status": "active",
                     "account": {"id": "c" * 32 if self.wrong_account else self.account}}]
        if "/dns_records?" in path:
            return list(self.records)
        raise AssertionError("Unexpected request")


class CloudflareBindingTests(unittest.TestCase):
    def setUp(self):
        redirect = contextlib.redirect_stdout(io.StringIO())
        redirect.__enter__()
        self.addCleanup(redirect.__exit__, None, None, None)

    def test_plan_never_writes(self):
        api = FakeCloudflare()
        MODULE.bind(api)
        self.assertTrue(all(method == "GET" for method, _, _ in api.calls))

    def test_apply_associates_pages_before_creating_dns_and_is_idempotent(self):
        api = FakeCloudflare()
        MODULE.bind(api, True)
        writes = [call for call in api.calls if call[0] != "GET"]
        self.assertTrue(writes[0][1].endswith("/domains"))
        self.assertEqual(writes[0][2], {"name": MODULE.HOST})
        self.assertTrue(writes[1][1].endswith("/dns_records"))
        self.assertEqual(writes[1][2]["content"], MODULE.TARGET)
        api.calls.clear()
        MODULE.bind(api, True)
        self.assertTrue(all(method == "GET" for method, _, _ in api.calls))

    def test_conflict_and_wrong_account_stop_before_any_write(self):
        for api in (FakeCloudflare(conflict=True), FakeCloudflare(wrong_account=True)):
            with self.assertRaises(MODULE.DeploymentError):
                MODULE.bind(api, True)
            self.assertTrue(all(method == "GET" for method, _, _ in api.calls))

    def test_errors_do_not_disclose_credentials_or_response_body(self):
        api = MODULE.Cloudflare("synthetic-secret-value", "a" * 32)
        class FailingOpener:
            def open(self, request, timeout):
                raise urllib.error.HTTPError(request.full_url, 403, "private-details", {},
                                             io.BytesIO(b"private-response"))
        api.opener = FailingOpener()
        with self.assertRaises(MODULE.DeploymentError) as raised:
            api.request("POST", "/synthetic-path", {})
        self.assertEqual(str(raised.exception), "Cloudflare POST failed with HTTP 403.")

    def test_relay_plan_is_read_only_and_apply_is_idempotent(self):
        api = FakeCloudflare()
        MODULE.bind_relay(api)
        self.assertTrue(all(method == "GET" for method, _, _ in api.calls))
        MODULE.bind_relay(api, True)
        writes = [call for call in api.calls if call[0] != "GET"]
        self.assertEqual(len(writes), 1)
        self.assertEqual(writes[0][0], "PUT")
        self.assertEqual(writes[0][2], {"hostname": MODULE.RELAY_HOST,
                                      "service": MODULE.RELAY_WORKER, "zone_name": "hallzee.com"})
        api.calls.clear()
        MODULE.bind_relay(api, True)
        self.assertTrue(all(method == "GET" for method, _, _ in api.calls))

    def test_relay_conflicts_or_missing_room_binding_prevent_writes(self):
        for scenario in ("worker", "dns", "binding", "account"):
            with self.subTest(scenario=scenario):
                api = FakeCloudflare(wrong_account=scenario == "account")
                if scenario == "worker":
                    api.relay_domains = [{"hostname": MODULE.RELAY_HOST, "service": "another-worker",
                                          "zone_name": "hallzee.com"}]
                if scenario == "dns":
                    api.relay_records = [{"name": MODULE.RELAY_HOST, "type": "A", "content": "192.0.2.1"}]
                if scenario == "binding":
                    api.relay_bindings = []
                with self.assertRaises(MODULE.DeploymentError):
                    MODULE.bind_relay(api, True)
                self.assertTrue(all(method == "GET" for method, _, _ in api.calls))


if __name__ == "__main__":
    unittest.main()
