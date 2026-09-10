# Website dependency maintenance

Hallzee's website security update on 2026-09-09 reduced `npm audit` from
22 affected packages (13 high, 8 moderate, 1 low) to **zero reported
vulnerabilities**, including a separate audit with development dependencies
omitted. This is a dependency advisory result, not a claim that the application
has no security defects. Existing deployments need a new build and deployment
to receive these changes.

## Updated dependency groups

| Group | Locked versions | Reason |
| --- | --- | --- |
| React, React DOM, React Server Components | 19.2.8 for all three | Uses the patched 19.2 maintenance release and keeps the renderer versions aligned. |
| vinext / RSC plugin / Vite | 1.0.0-beta.9 / 0.5.34 / 8.2.2 | Removes the vulnerable image-size dependency and updates the development/build server. |
| Cloudflare Vite plugin / Wrangler / Workers types | 1.54.6 / 4.130.0 / 5.20260908.1 | Updates the local Workers runtime, HTTP/WebSocket dependencies, and required matching type definitions. |
| Transitive build dependencies | Updated in package-lock.json | Refreshes Babel, browser compatibility data, URI parsing, ZIP handling, YAML, source maps, and other affected transitive packages within their declared ranges. |

React documents the fixed version in its [server-function denial-of-service
advisory](https://github.com/react/react/security/advisories/GHSA-wx67-qw84-cm4g).
Vite's [Windows path-handling advisory](https://github.com/vitejs/vite/security/advisories/GHSA-fx2h-pf6j-xcff)
covers the old development server.

## Temporary, narrowly scoped overrides

`preview-site/package.json` contains two overrides. Keep their scope narrow and
review them whenever upgrading their parent packages:

- `@esbuild-kit/core-utils → esbuild 0.25.12`: Drizzle Kit 0.31.10 still depends
  on an abandoned loader that requests esbuild 0.18.20. The override uses the
  same esbuild version that Drizzle Kit's own supported `^0.25.4` range resolves
  to. It removes the [development-server cross-origin disclosure
  advisory](https://github.com/evanw/esbuild/security/advisories/GHSA-67mh-4wv8-2f99).
  Synchronous/asynchronous TypeScript transforms and the project's migration
  generation were checked. Remove this override when a compatible stable
  Drizzle release removes or updates the obsolete loader.
- `miniflare → sharp 0.35.4`: the selected Cloudflare tooling still pins sharp
  0.35.2. This patch upgrade supplies libheif 1.23.2 and addresses the
  [image-decoder advisory](https://github.com/lovell/sharp/security/advisories/GHSA-rgj7-g3m4-5g8c).
  PNG resizing and AVIF encoding/decoding were checked with the bundled native
  library. Remove this override when the selected Miniflare release requires
  sharp 0.35.4 or a later patched version itself.

Do not use `npm audit fix --force` for these paths. At the time of this change,
its recommendations included downgrading Drizzle Kit to 0.18.1 and Cloudflare
tooling to much older versions. Do not disable audits or hide these dependency
paths to make the report pass.

The `dev`, `build`, and `start` commands use a small Node.js launcher to set
Wrangler's project-local log path on Windows and macOS. It runs the installed
vinext CLI in the same process, preserving arguments, signals, and exit codes;
no shell-specific environment assignment or extra dependency is needed.

## Reproduce the checks

For a clean computer, install Node.js 22 LTS (including npm) from the
[official Node.js download](https://nodejs.org/en/download), then run from the
repository root:

```bash
npm --prefix preview-site ci && npm --prefix preview-site test
npm --prefix preview-site run lint && npm --prefix preview-site audit
```

`npm ci` installs the exact committed lockfile. `npm test` builds the Worker and
runs the six rendered-HTML/prototype checks. For a dependency change involving
Drizzle, also run `npm --prefix preview-site run db:generate`: with the committed
schema it should report no changes and leave migration files untouched. Do not
apply migrations to a hosted database as part of dependency validation.

Validation completed on macOS: a clean install, production build, all six
existing tests, lint, migration generation, and native image-processing smoke
checks passed with Node.js 25.5.0. The production build, six tests, lint, and
migration generation also passed with Node.js 22.23.2 (the CI major version).
The portable launcher passed the Node.js 22 build/tests/lint checks, CLI help,
and invalid-command exit-status check. Full TypeScript checking now passes:
the configuration loads Workers types and the background-preset type includes
the already-defined sidebar style. Full and production-only npm audits returned
zero findings.

GitHub Actions also passed a clean install, zero-finding npm audit, production
build, all six tests, lint, and full TypeScript checking on both Ubuntu and
Windows with Node.js 22 in
[run 34409156145](https://github.com/dannysombrero/hallzee-dev/actions/runs/34409156145).
This verifies the portable launcher's Windows build path.

A Mac is sufficient for these website dependency checks; no physical Windows
PC is required. Interactive Windows dev/start sessions, browser behavior, and
Windows desktop/Bluetooth operation remain unverified by these website checks.
Website page content, images, hosting configuration, and the live deployment
are unchanged.
