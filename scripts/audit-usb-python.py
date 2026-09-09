#!/usr/bin/env python3
"""Check every version in the USB Python lock against OSV without installing it."""
import argparse
import datetime
import json
from pathlib import Path
import re
import urllib.request

ROOT = Path(__file__).resolve().parents[1]


def parse_results(payload, packages):
    """Accept only a complete OSV batch, never interpret an error as no findings."""
    if not isinstance(payload, dict) or 'error' in payload or not isinstance(payload.get('results'), list):
        raise ValueError('OSV returned an invalid batch response')
    results = payload['results']
    if len(results) != len(packages):
        raise ValueError('OSV did not return a result for every locked dependency')
    findings = []
    for (name, version), result in zip(packages, results):
        if not isinstance(result, dict) or set(result) - {'vulns', 'next_page_token'}:
            raise ValueError(f'OSV returned an invalid result for {name}')
        token = result.get('next_page_token', '')
        if not isinstance(token, str) or token:
            # The small lock should fit in one page. Refuse a partial audit if
            # OSV changes that, rather than silently omitting remaining results.
            raise ValueError(f'OSV returned incomplete/paginated results for {name}')
        advisories = result.get('vulns', [])
        if (not isinstance(advisories, list)
                or any(not isinstance(item, dict) or not isinstance(item.get('id'), str)
                       or not item['id'].strip() for item in advisories)):
            raise ValueError(f'OSV returned invalid advisories for {name}')
        if advisories:
            findings.append(dict(name=name, version=version, advisories=advisories))
    return findings


def audit(lock):
    packages = re.findall(r'(?m)^([\w.-]+)==([^\s;]+)', lock.read_text())
    if not packages:
        raise ValueError('USB lockfile contains no pinned dependencies')
    queries = [{'package': {'name': name, 'ecosystem': 'PyPI'}, 'version': version}
               for name, version in packages]
    request = urllib.request.Request('https://api.osv.dev/v1/querybatch',
                                     data=json.dumps({'queries': queries}).encode(),
                                     headers={'Content-Type': 'application/json'})
    with urllib.request.urlopen(request, timeout=60) as response:
        findings = parse_results(json.load(response), packages)
    return dict(source='https://osv.dev', checked_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                packages_scanned=len(packages), findings=findings)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    try:
        report = audit(ROOT / 'firmware/requirements.txt')
    except (OSError, ValueError, KeyError) as error:
        parser.error(f'Dependency audit did not complete: {error}')
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))
    return 1 if report['findings'] else 0


if __name__ == '__main__':
    raise SystemExit(main())
