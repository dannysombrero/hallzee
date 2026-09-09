#!/usr/bin/env python3
"""Restore and transfer platform-specific NuGet graphs for dependency submission."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import subprocess
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parents[1]
TOKEN = '__HALLZEE_REPOSITORY_ROOT__'
CORE = 'receiver/windows/BathroomSync.Core/BathroomSync.Core.csproj'
PROJECTS = {
    'windows': [
        CORE,
        'receiver/windows/BathroomSync.Windows.csproj',
        'receiver/windows/BathroomSync.Tests/BathroomSync.Tests.csproj',
        'receiver/universal.tests/BathroomSync.Universal.Tests.csproj',
        'receiver/universal/BathroomSync.Universal.csproj',
        'tools/FirmwareTool/FirmwareTool.csproj',
    ],
    'macos': [
        CORE,
        'receiver/MacBLEAgent/BathroomSync.MacBLEAgent.csproj',
        'receiver/universal/BathroomSync.Universal.csproj',
        'tools/FirmwareTool/FirmwareTool.csproj',
    ],
}


def rebase(value, old, new):
    """Replace a complete path prefix in values AND project-reference keys."""
    if isinstance(value, dict):
        return {rebase(key, old, new): rebase(item, old, new) for key, item in value.items()}
    if isinstance(value, list):
        return [rebase(item, old, new) for item in value]
    if isinstance(value, str):
        normalized = value.replace('\\', '/')
        if normalized == old or normalized.startswith(old + '/'):
            return new + normalized[len(old):]
    return value


def project_path(root, relative):
    path = PurePosixPath(relative)
    if path.is_absolute() or '..' in path.parts or path.suffix != '.csproj':
        raise ValueError('Invalid dependency manifest project path')
    project = (root / relative).resolve()
    if root.resolve() not in project.parents or not project.is_file():
        raise ValueError(f'Dependency manifest must name a real repository project: {relative}')
    return project


def validate_assets(data, expected_project):
    if not isinstance(data.get('targets'), dict) or not data['targets']:
        raise ValueError('Restored assets contain no dependency targets')
    actual = data.get('project', {}).get('restore', {}).get('projectPath', '').replace('\\', '/')
    if actual != expected_project:
        raise ValueError('Restored dependency manifest points outside its expected repository project')


def stage(root, platform, destination):
    if destination.exists() and any(destination.iterdir()):
        raise ValueError('Dependency staging directory must be empty')
    records = []
    for relative in PROJECTS[platform]:
        project = project_path(root, relative)
        assets = project.parent / 'obj/project.assets.json'
        data = json.loads(assets.read_text())
        validate_assets(data, project.as_posix())
        data = rebase(data, root.resolve().as_posix(), TOKEN)
        # These cache/config locations are irrelevant to the graph parser and
        # must not carry absolute runner paths into the transfer artifact.
        data['packageFolders'] = {TOKEN + '/.dependency-packages/': {}}
        restore = data['project']['restore']
        restore['packagesPath'] = TOKEN + '/.dependency-packages'
        restore['configFilePaths'] = []
        restore['fallbackFolders'] = []
        for framework in data.get('project', {}).get('frameworks', {}).values():
            framework.pop('runtimeIdentifierGraphPath', None)
        validate_assets(data, TOKEN + '/' + relative)
        target = destination / relative / 'project.assets.json'
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(data, indent=2) + '\n')
        records.append(dict(project=relative, sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
    (destination / 'graphs.json').write_text(json.dumps(dict(platform=platform, projects=records), indent=2) + '\n')
    return len(records)


def activate(root, platform, source):
    manifest = json.loads((source / 'graphs.json').read_text())
    if manifest.get('platform') != platform:
        raise ValueError('Dependency artifact platform mismatch')
    records = manifest.get('projects', [])
    if [item.get('project') for item in records] != PROJECTS[platform]:
        raise ValueError('Dependency artifact omitted or added a project')
    prepared = []
    for record in records:
        relative = record['project']
        project = project_path(root, relative)
        assets = source / relative / 'project.assets.json'
        if hashlib.sha256(assets.read_bytes()).hexdigest() != record['sha256']:
            raise ValueError(f'Dependency artifact checksum mismatch: {relative}')
        data = json.loads(assets.read_text())
        validate_assets(data, TOKEN + '/' + relative)
        data = rebase(data, TOKEN, root.resolve().as_posix())
        validate_assets(data, project.as_posix())
        prepared.append((project.parent / 'obj/project.assets.json', data))
    # Check every graph before writing any file into the fresh Linux checkout.
    for target, data in prepared:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(data, indent=2) + '\n')
    return len(prepared)


def verify_scan(root, platform, path):
    """Check the same manifest identities the official upload action consumes."""
    report = json.loads(path.read_text())
    if report.get('resultCode') != 'Success':
        raise ValueError('Official detector did not report a successful scan')
    graphs = report.get('dependencyGraphs')
    components = report.get('componentsFound')
    if not isinstance(graphs, dict) or not isinstance(components, list) or not components:
        raise ValueError('Detector did not produce dependency graphs and components')
    expected = set(PROJECTS[platform])
    found_graphs = set()
    for graph in graphs:
        absolute = Path(graph).resolve()
        try:
            relative = absolute.relative_to(root.resolve()).as_posix()
        except ValueError:
            raise ValueError('Detector graph contains an absolute path outside the repository')
        project_path(root, relative)
        found_graphs.add(relative)
    if found_graphs != expected:
        raise ValueError('Detector graph omitted a restored project or included a temporary artifact path')
    locations = set()
    for component in components:
        if not component.get('component', {}).get('packageUrl'):
            raise ValueError('Detector component has no package URL for submission')
        for location in component.get('locationsFoundAt', []):
            # The official action removes one leading slash and URL-decodes
            # these values to create GitHub manifest names/file paths.
            relative = unquote(location[1:] if location.startswith('/') else location)
            if relative not in expected:
                raise ValueError('Detector component location is not a repository project path')
            locations.add(relative)
    if locations != expected:
        raise ValueError('Detector did not associate dependencies with every restored project')
    return len(locations)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('restore', 'stage', 'activate', 'verify-scan'))
    parser.add_argument('--platform', choices=PROJECTS, required=True)
    parser.add_argument('--directory', type=Path)
    parser.add_argument('--scan', type=Path)
    args = parser.parse_args()
    try:
        if args.command == 'restore':
            for relative in PROJECTS[args.platform]:
                # Referencing projects restore Core automatically.
                if relative != CORE:
                    subprocess.run(['dotnet', 'restore', str(project_path(ROOT, relative))], check=True)
            return
        if args.command == 'verify-scan':
            if args.scan is None:
                parser.error('--scan is required for verify-scan')
            count = verify_scan(ROOT, args.platform, args.scan)
            print(f'verify-scan: official detector produced {count} real repository manifest paths')
            return
        if args.directory is None:
            parser.error('--directory is required for stage/activate')
        count = (stage if args.command == 'stage' else activate)(ROOT, args.platform, args.directory.resolve())
        print(f'{args.command}: verified {count} {args.platform} dependency graphs with repository project paths')
    except (ValueError, KeyError, TypeError, OSError, subprocess.CalledProcessError) as error:
        parser.error(str(error))


if __name__ == '__main__':
    main()
