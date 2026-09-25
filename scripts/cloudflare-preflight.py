#!/usr/bin/env python3
"""Read-only Hallzee deployment checks; never print credentials or raw API data."""

import json
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

API = "https://api.cloudflare.com/client/v4"
PROJECT = "hallzee-web-client"
WORKER = "hallzee-relay"
HOSTS = ("web.hallzee.com", "pass.hallzee.com", "relay.hallzee.com")


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def preflight(token, account, opener=None):
    if not token or not re.fullmatch(r"[a-fA-F0-9]{32}", account):
        return [{"check": "credentials", "ok": False,
                 "status": "Set CLOUDFLARE_API_TOKEN and CLOUDFLARE_ACCOUNT_ID as secrets."}]
    opener = opener or urllib.request.build_opener(NoRedirect())
    checks = []

    def get(label, path, select):
        request = urllib.request.Request(
            API + path, method="GET",
            headers={"Authorization": "Bearer " + token, "Accept": "application/json"},
        )
        try:
            with opener.open(request, timeout=20) as response:
                payload = json.load(response)
            if payload.get("success") is not True:
                checks.append({"check": label, "ok": False, "status": "API rejected request"})
                return None
            result = payload["result"]
            checks.append({"check": label, "ok": True, "details": select(result)})
            return result
        except urllib.error.HTTPError as error:
            # Do not emit URLs, response bodies, headers or exception strings.
            checks.append({"check": label, "ok": False, "http_status": error.code})
        except Exception:
            checks.append({"check": label, "ok": False, "status": "Network or response error"})
        return None

    prefix = "/accounts/" + account
    get("Pages project", prefix + "/pages/projects/" + PROJECT,
        lambda r: {"expected_project": r.get("name") == PROJECT,
                   "expected_subdomain": r.get("subdomain") == PROJECT + ".pages.dev",
                   "production_is_main": r.get("production_branch") == "main"})
    get("Pages domains", prefix + "/pages/projects/" + PROJECT + "/domains",
        lambda r: [{"name": d["name"], "active": d.get("status") == "active"}
                   for d in r if d.get("name") in HOSTS])
    scripts = get("Workers scripts", prefix + "/workers/scripts",
                  lambda r: {"relay_exists": any(s.get("id") == WORKER for s in r)})
    if scripts is not None and any(s.get("id") == WORKER for s in scripts):
        current = next(s for s in scripts if s.get("id") == WORKER)
        checks.append({"check": "Relay migration", "ok": True, "details": {
            "initial_migration_applied": current.get("migration_tag") == "v1",
            "has_migration": bool(current.get("migration_tag")),
        }})
        get("Relay settings", prefix + "/workers/scripts/" + WORKER + "/settings",
            lambda r: {"expected_room_binding": any(
                b.get("name") == "ROOM_DO" and b.get("type") == "durable_object_namespace"
                and b.get("class_name") == "RoomDurableObject"
                for b in r.get("bindings", []))})
    get("Worker custom domain", prefix + "/workers/domains?hostname=relay.hallzee.com",
        lambda r: {"relay_domain_bound": any(
            d.get("hostname") == HOSTS[2] and d.get("service") == WORKER for d in r),
            "conflicting_service": any(d.get("hostname") == HOSTS[2]
                                       and d.get("service") != WORKER for d in r)})
    zones = get("Hallzee zone", "/zones?" + urllib.parse.urlencode(
        {"name": "hallzee.com", "account.id": account}),
        lambda r: {"matching_zone_count": sum(z.get("name") == "hallzee.com"
                                               and z.get("account", {}).get("id") == account
                                               for z in r)})
    matching = [z for z in zones or [] if z.get("name") == "hallzee.com"
                and z.get("account", {}).get("id") == account
                and re.fullmatch(r"[a-fA-F0-9]{32}", z.get("id", ""))]
    if zones is not None and len(matching) != 1:
        checks.append({"check": "Hallzee zone access", "ok": False,
                       "status": "Exactly one hallzee.com zone must be accessible in the deployment account."})
    if len(matching) == 1:
        for host in HOSTS:
            get("DNS " + host, "/zones/" + matching[0]["id"] + "/dns_records?"
                + urllib.parse.urlencode({"name": host}),
                lambda r: {"record_count": len(r), "pages_cname": any(
                    d.get("type") == "CNAME"
                    and d.get("content", "").rstrip(".") == PROJECT + ".pages.dev"
                    for d in r)})
    return checks


def main():
    checks = preflight(os.environ.get("CLOUDFLARE_API_TOKEN", ""),
                       os.environ.get("CLOUDFLARE_ACCOUNT_ID", ""))
    print(json.dumps({"read_only": True, "checks": checks}, indent=2))
    return 0 if all(c["ok"] for c in checks) else 1


if __name__ == "__main__":
    sys.exit(main())
