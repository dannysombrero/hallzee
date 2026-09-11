import { readFile, writeFile, readdir, copyFile, mkdir, stat } from "node:fs/promises";
import { createHash } from "node:crypto";
import { execFileSync } from "node:child_process";
import { stripTypeScriptTypes } from "node:module";
const root = new URL("../", import.meta.url),
  dist = new URL("dist/", root);
const pkg = JSON.parse(await readFile(new URL("package.json", root), "utf8"));
let commit = "source-zip",
  dirty = true;
try {
  commit = execFileSync("git", ["rev-parse", "HEAD"], { cwd: root, encoding: "utf8" }).trim();
  dirty = !!execFileSync("git", ["status", "--porcelain"], { cwd: root, encoding: "utf8" }).trim();
} catch {
  /* Source ZIP bootstrap needs no Git. */
}
const repository = process.env.GITHUB_REPOSITORY || "dannysombrero/hallzee";
const sourceUrl = `https://github.com/${repository}/${commit === "source-zip" ? "" : "tree/" + commit}`;
await writeFile(
  new URL("source.txt", dist),
  `Hallzee ${pkg.version}\n${sourceUrl}\nUncommitted changes: ${dirty}\nAGPL-3.0-or-later. Distribute the corresponding source with modified builds.\n`,
);
for (const name of ["LICENSE", "COPYRIGHT", "THIRD-PARTY-NOTICES.md"]) {
  try {
    await copyFile(
      new URL("../" + name, root),
      new URL(name === "THIRD-PARTY-NOTICES.md" ? name : name + ".txt", dist),
    );
  } catch (error) {
    if (name !== "COPYRIGHT") throw error;
  }
}
// Inventory every resolved dependency; retain original installed package notices.
const lock = JSON.parse(await readFile(new URL("package-lock.json", root), "utf8"));
const inventory = [];
await mkdir(new URL("licenses/", dist), { recursive: true });
for (const [location, item] of Object.entries(lock.packages)) {
  if (!location || !item.version) continue;
  const name = item.name || location.split("node_modules/").at(-1);
  const record = {
    name,
    version: item.version,
    license: item.license || "REVIEW REQUIRED",
    development: !!item.dev,
    source: `https://www.npmjs.com/package/${name}/v/${item.version}`,
    notices: [],
  };
  let files = [];
  try {
    files = await readdir(new URL(location + "/", root));
  } catch {
    /* Platform-specific optional dependency is not installed here. */
  }
  for (const file of files.filter((file) => /^(license|copying|copyright|notice)/i.test(file))) {
    const from = new URL(`${location}/${file}`, root);
    if (!(await stat(from)).isFile()) continue;
    const destination = `licenses/${name.replaceAll("/", "_")}@${item.version}-${file}`;
    await copyFile(from, new URL(destination, dist));
    record.notices.push(destination);
  }
  if (!item.dev && files.length && !record.notices.length)
    throw new Error(`Missing runtime notices: ${name}`);
  inventory.push(record);
}
await writeFile(
  new URL("licenses/dependency-inventory.json", dist),
  JSON.stringify(
    {
      scope:
        "Resolved web build dependencies; optional packages may not be installed on this platform.",
      packages: inventory,
    },
    null,
    2,
  ),
);
const workerSource = await readFile(new URL("src/pwa/service-worker.ts", root), "utf8");
const files = await readdir(dist, { recursive: true });
const assets = [];
for (const file of files.sort())
  if (
    file !== "_headers" &&
    file !== "sw.js" &&
    file !== "build.json" &&
    (await stat(new URL(file, dist))).isFile()
  )
    assets.push("/" + file);
const hash = createHash("sha256").update(workerSource);
for (const path of assets) hash.update(path).update(await readFile(new URL("." + path, dist)));
const build = hash.digest("hex").slice(0, 16);
await writeFile(
  new URL("build.json", dist),
  JSON.stringify(
    {
      version: pkg.version,
      build,
      commit,
      dirty,
      sourceUrl,
      productionOrigin: process.env.PRODUCTION_ORIGIN || null,
      hardwareAcceptance: "pending",
    },
    null,
    2,
  ),
);
assets.push("/build.json");
const source = workerSource
  .replace("__HALLZEE_BUILD__", build)
  .replace(/(['"])__HALLZEE_ASSETS__\1/, JSON.stringify(JSON.stringify(assets)));
await writeFile(
  new URL("sw.js", dist),
  stripTypeScriptTypes(source).replace(/export\s*\{\s*\};?/g, ""),
);
console.log(
  `Offline shell ${build}: ${assets.length} static assets including licenses. Hardware acceptance pending; not deployed.`,
);
