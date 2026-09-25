#!/usr/bin/env python3
"""Plan or bind Hallzee's student and relay hosts without replacing other services."""

import argparse
import json
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

API = "https://api.cloudflare.com/client/v4"
PROJECT = "hallzee-web-client"
HOST = "pass.hallzee.com"
TARGET = PROJECT + ".pages.dev"
RELAY_HOST = "relay.hallzee.com"
RELAY_WORKER = "hallzee-relay"


class DeploymentError(Exception):
    pass


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class Cloudflare:
    def __init__(self, token, account):
        if not token or not re.fullmatch(r"[a-fA-F0-9]{32}", account):
            raise DeploymentError("Missing or invalid deployment secrets.")
        self.token = token
        self.account = account
        self.opener = urllib.request.build_opener(NoRedirect())

    def request(self, method, path, body=None):
        # API responses may contain secrets. Emit only our fixed error messages.
        request = urllib.request.Request(
            API + path, method=method,
            data=json.dumps(body).encode() if body is not None else None,
            headers={"Authorization": "Bearer " + self.token, "Content-Type": "application/json"},
        )
        try:
            with self.opener.open(request, timeout=30) as response:
                data = json.load(response)
            if data.get("success") is not True:
                raise DeploymentError("Cloudflare rejected the requested operation.")
            return data["result"]
        except urllib.error.HTTPError as error:
            raise DeploymentError(f"Cloudflare {method} failed with HTTP {error.code}.") from None
        except DeploymentError:
            raise
        except Exception:
            raise DeploymentError("Cloudflare connection or response failed.") from None


def plan(api):
    project_path = f"/accounts/{api.account}/pages/projects/{PROJECT}"
    project = api.request("GET", project_path)
    if (project.get("name") != PROJECT or project.get("subdomain") != TARGET
            or project.get("production_branch") != "main"):
        raise DeploymentError("Pages project differs from the reviewed production target.")
    domains = api.request("GET", project_path + "/domains")
    if not any(d.get("name") == "web.hallzee.com" and d.get("status") == "active" for d in domains):
        raise DeploymentError("The expected teacher domain is not active on this project.")
    zones = api.request("GET", "/zones?" + urllib.parse.urlencode(
        {"name": "hallzee.com", "account.id": api.account}))
    matching = [z for z in zones if z.get("name") == "hallzee.com"
                and z.get("account", {}).get("id") == api.account
                and z.get("status") == "active"
                and re.fullmatch(r"[a-fA-F0-9]{32}", z.get("id", ""))]
    if len(matching) != 1:
        raise DeploymentError("An active hallzee.com zone is required in the Pages account.")
    dns_path = "/zones/" + matching[0]["id"] + "/dns_records"
    records = api.request("GET", dns_path + "?name=" + HOST)
    validate_records(records)
    return project_path, dns_path, domains, records


def validate_records(records):
    if records and (len(records) != 1 or records[0].get("name") != HOST
                    or records[0].get("type") != "CNAME"
                    or records[0].get("content", "").rstrip(".") != TARGET):
        raise DeploymentError("Conflicting pass.hallzee.com DNS record; no record was replaced.")


def bind(api, apply=False):
    project_path, dns_path, domains, records = plan(api)
    domain_exists = any(d.get("name") == HOST for d in domains)
    print(json.dumps({"host": HOST, "target": TARGET, "apply": apply,
                      "add_pages_domain": not domain_exists, "add_cname": not records}))
    if not apply:
        return
    if not domain_exists:
        api.request("POST", project_path + "/domains", {"name": HOST})
    # Cloudflare may provision DNS during domain association; recheck before creating.
    records = api.request("GET", dns_path + "?name=" + HOST)
    validate_records(records)
    if not records:
        api.request("POST", dns_path, {"type": "CNAME", "name": HOST,
                    "content": TARGET, "ttl": 1, "proxied": True})
    print("Student hostname associated with the existing Hallzee Pages project.")


def wait_active(api, attempts=60):
    path = f"/accounts/{api.account}/pages/projects/{PROJECT}/domains/{HOST}"
    for attempt in range(attempts):
        domain = api.request("GET", path)
        status = domain.get("status")
        if status == "active":
            print("pass.hallzee.com is active.")
            return
        if status in {"blocked", "error", "deactivated"}:
            raise DeploymentError("Student hostname validation requires attention in Cloudflare Pages.")
        if attempt + 1 < attempts:
            time.sleep(10)
    raise DeploymentError("Student hostname is associated; certificate validation is still pending.")


def bind_relay(api, apply=False):
    # Reuse the project/account/zone checks before touching the relay hostname.
    _, dns_path, _, _ = plan(api)
    domains_path = f"/accounts/{api.account}/workers/domains"
    domains = api.request("GET", domains_path + "?hostname=" + RELAY_HOST)
    matching = [d for d in domains if d.get("hostname") == RELAY_HOST]
    if matching and (len(matching) != 1 or matching[0].get("service") != RELAY_WORKER
                     or matching[0].get("zone_name") != "hallzee.com"):
        raise DeploymentError("The relay hostname belongs to another Worker or zone; no binding was replaced.")
    if not matching and api.request("GET", dns_path + "?name=" + RELAY_HOST):
        raise DeploymentError("Conflicting relay.hallzee.com DNS record; no record was replaced.")
    print(json.dumps({"host": RELAY_HOST, "worker": RELAY_WORKER, "apply": apply,
                      "add_worker_domain": not matching}))
    if not apply or matching:
        return
    settings = api.request("GET", f"/accounts/{api.account}/workers/scripts/{RELAY_WORKER}/settings")
    if not any(b.get("name") == "ROOM_DO" and b.get("type") == "durable_object_namespace"
               and b.get("class_name") == "RoomDurableObject" for b in settings.get("bindings", [])):
        raise DeploymentError("Deploy the expected relay and room binding before attaching its hostname.")
    result = api.request("PUT", domains_path, {"hostname": RELAY_HOST,
                         "service": RELAY_WORKER, "zone_name": "hallzee.com"})
    if result.get("hostname") != RELAY_HOST or result.get("service") != RELAY_WORKER:
        raise DeploymentError("Cloudflare returned an unexpected relay domain binding.")
    print("relay.hallzee.com is associated with hallzee-relay.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true", help="Create the reviewed domain and missing CNAME")
    parser.add_argument("--wait", action="store_true", help="Wait up to ten minutes for domain activation")
    parser.add_argument("--relay", action="store_true", help="Bind the relay Worker hostname instead of the student Pages hostname")
    args = parser.parse_args()
    if args.relay and args.wait:
        parser.error("Use verify-relay.mjs to verify relay availability; --wait is for Pages.")
    try:
        api = Cloudflare(os.environ.get("CLOUDFLARE_API_TOKEN", ""),
                         os.environ.get("CLOUDFLARE_ACCOUNT_ID", ""))
        (bind_relay if args.relay else bind)(api, args.apply)
        if args.wait:
            wait_active(api)
    except DeploymentError as error:
        print(str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
