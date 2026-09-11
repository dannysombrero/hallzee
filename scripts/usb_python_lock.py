"""Read the hash-locked requirements format generated for USB release tools.

Extras select dependencies, but installed metadata and OSV use the base
distribution name. Keep the original lines for pip, including extras/markers.
"""
import re


def locked_requirements(text):
    result = {}
    current = None
    identifier = r'[A-Za-z0-9][A-Za-z0-9_.-]*'
    header = re.compile(
        rf'({identifier})(?:\[{identifier}(?:\s*,\s*{identifier})*\])?'
        r'==([A-Za-z0-9][A-Za-z0-9.!+_-]*)(?=\s|;|\\|$)')
    for line in text.splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith('#'):
            continue
        if not line[0].isspace():
            match = header.match(line)
            if not match:
                raise ValueError(f'Expected a pinned requirement: {line}')
            current = re.sub(r'[-_.]+', '-', match.group(1)).lower()
            if current in result:
                raise ValueError(f'Duplicate locked distribution: {current}')
            result[current] = dict(version=match.group(2), lines=[line], hashes=set())
        elif current and stripped.startswith('--hash='):
            result[current]['lines'].append(line)
        else:
            raise ValueError(f'Unexpected requirement continuation: {line}')
        result[current]['hashes'].update(re.findall(r'--hash=sha256:([a-f0-9]{64})\b', line))
    if not result:
        raise ValueError('USB lockfile contains no pinned dependencies')
    for name, requirement in result.items():
        if not requirement['hashes']:
            raise ValueError(f'No locked SHA-256 hashes for {name}')
    return result
