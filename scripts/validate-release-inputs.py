#!/usr/bin/env python3
"""Normalize desktop and firmware release inputs before any builds start."""
import os
import re
import sys


def normalize_version(value):
    match = re.fullmatch(r'v?(0|[1-9][0-9]{0,2})\.(0|[1-9][0-9]{0,2})(?:\.(0|[1-9][0-9]{0,2}))?', value.strip())
    if not match:
        raise ValueError(f'Invalid release version {value!r}. Use 1.0.0, v1.0.0, 1.0, or v1.0. '
                         'Each number must be 0–999 without leading zeros; prerelease labels are not supported.')
    return '.'.join([match[1], match[2], match[3] or '0'])


def normalize_repository(value):
    repository = value.strip()
    if not re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', repository):
        raise ValueError(f'Invalid release repository {value!r}. Set HALLZEE_RELEASE_REPOSITORY '
                         'to owner/repository, without https://github.com/ or a trailing slash.')
    return repository


def main():
    try:
        version = normalize_version(os.environ.get('RELEASE_VERSION', ''))
        repository = normalize_repository(os.environ.get('RELEASE_REPOSITORY', ''))
    except ValueError as error:
        print(error, file=sys.stderr)
        return 1
    # Both values are validated before writing any outputs. Every consumer uses
    # this canonical version, including the assembly version and publish metadata.
    if os.environ.get('GITHUB_OUTPUT'):
        with open(os.environ['GITHUB_OUTPUT'], 'a', encoding='utf-8') as output:
            output.write(f'version={version}\nrepository={repository}\n')
    print(f'Release version: {version}; release repository: {repository}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
