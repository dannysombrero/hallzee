# Desktop dependency maintenance

The desktop clients and USB firmware tool share their database packages through
`receiver/windows/BathroomSync.Core/BathroomSync.Core.csproj`. Update them there so
every shipped client uses the same database engine. Start with the setup in
[Testing and installation](testing-and-installation.md) on a clean computer.

## September 2026 SQLite update

The previous `Microsoft.Data.Sqlite` package pulled in
`SQLitePCLRaw.lib.e_sqlite3` 2.1.6, which is affected by
[CVE-2025-6965](https://github.com/advisories/GHSA-2m69-gcr7-jv3q). Its retired
package line has no patched version listed in that advisory. Updating only the
Microsoft package would not replace that native engine.

The shared project now references:

| Component | Package | Version |
| --- | --- | --- |
| Managed database API | Microsoft.Data.Sqlite.Core | 8.0.30 |
| Native provider configuration | SQLitePCLRaw.config.e_sqlite3 | 3.0.5 |
| Bundled SQLite engine | SourceGear.sqlite3 | 3.53.4 |

This follows [Microsoft's custom SQLite guidance](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/custom-versions)
and the [SQLitePCLRaw 3 migration instructions](https://github.com/ericsink/SQLitePCL.raw/blob/main/v3.md).
Microsoft.Data.Sqlite still initializes the provider automatically. The native
engine has an explicit version so Dependabot can propose engine updates
independently. The replacement is the publicly available, unencrypted SQLite
package; it requires no paid build service. Its
[package license](https://www.nuget.org/packages/SourceGear.sqlite3/3.53.4/License)
identifies SQLite as public domain.

Database paths, schema, migrations and connection pooling are unchanged.
Existing classroom databases continue through the existing
migration code. The clients continue to bundle their native database library;
teachers do not need to install SQLite.

## Verification and CI

`Directory.Build.props` enables NuGet auditing for direct and transitive
dependencies, including low-severity advisories. When `CI=true`, known
vulnerabilities (`NU1901` through `NU1904`) fail restore/build. Local builds show
the same advisories as warnings. An unavailable advisory service (`NU1900`) is
not evidence of a clean scan; retry it before accepting a dependency update.

After contributor setup, these two commands exercise storage, migrations,
import/export, desktop behavior and the actual loaded native engine:

```sh
dotnet test receiver/windows/BathroomSync.Tests/BathroomSync.Tests.csproj -c Release
dotnet test receiver/universal.tests/BathroomSync.Universal.Tests.csproj -c Release
```

`NativeSqliteTests` requires the engine actually loaded by the process to be
3.53.4 or newer. It catches missing native assets or a fallback to an older
engine, in addition to the existing database tests. Use the normal release
workflows to publish every supported runtime after a native dependency update.

The September 9, 2026 verification completed:

- All 108 core tests and 86 desktop UI tests passed on Apple Silicon and again
  with the Intel .NET runtime under Rosetta.
- Fresh transitive advisory scans found no vulnerable NuGet packages in the
  core, desktop, test, USB-tool, legacy Windows-client or Mac-helper graphs.
  Windows-targeted desktop restore also passed with CI audit errors enabled.
- Self-contained desktop publishes succeeded for `win-x64`, `osx-arm64` and
  `osx-x64`; USB-tool publishes succeeded for `win-x64` and `osx-arm64`.
  The Windows desktop publish compiled the Windows target and Bluetooth code.
- Complete Mac ARM64 and Intel packaging passed, including strict signatures
  on every native library, no-argument helper startup (Intel under Rosetta),
  and the same checks after extracting each finished ZIP. Inventories include
  the helper's exact macOS runtime-pack version and its license, plus Inter's
  font notice. No obsolete SQLite 2.x native package remains in either inventory.
- Each published native SQLite library matched the package binary for its
  architecture. The Windows DLL also reported the expected version string.
- The simulated USB update test passed, including write boundaries and failure
  handling. A temporary fixture using the old vulnerable transitive package
  failed CI restore as expected, proving the audit gate is active.

## Mac bundle notices and startup

Mac packages keep license documents and their per-runtime inventory in
`Hallzee.app/Contents/Resources`. Placing versioned notice directories under
`Contents/MacOS` caused a reproduced `codesign` failure: dotted directory names
there are interpreted as nested code bundles. This follows
[Apple's bundle placement rules](https://developer.apple.com/documentation/bundleresources/placing-content-in-a-bundle).
Runtime libraries remain in their existing executable locations. Packaging
still signs native libraries individually, verifies the complete app, and
launches its helper without arguments before producing the ZIP. On Apple
Silicon, an Intel helper is also checked when Rosetta is already installed.
The inventory is generated before signing and records resolved package
versions. The published ZIP's checksum covers the final signed binaries;
pre-sign binary hashes would become stale during code signing.

A Mac is sufficient to repeat the Mac tests and Windows cross-publish checks.
A Windows PC is required to verify the real Windows application launch, native
SQLite DLL loading, opening/upgrading an existing classroom database, and
Windows Bluetooth/USB operation. Those physical Windows behaviors have not
been verified by this dependency update; cross-publishing does not execute the
Windows binaries. Intel testing above used Rosetta, not a physical Intel Mac.
