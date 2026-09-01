import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const root = new URL("../", import.meta.url);

async function render() {
  const workerUrl = new URL("../dist/server/index.js", import.meta.url);
  workerUrl.searchParams.set("test", `${process.pid}-${Date.now()}`);
  const { default: worker } = await import(workerUrl.href);
  return worker.fetch(
    new Request("http://localhost/", { headers: { accept: "text/html" } }),
    { ASSETS: { fetch: async () => new Response("Not found", { status: 404 }) } },
    { waitUntil() {}, passThroughOnException() {} },
  );
}

test("server-renders the Hallzee dashboard baseline", async () => {
  const response = await render();
  assert.equal(response.status, 200);
  const html = await response.text();
  assert.match(html, /Hallzee Client Preview/i);
  assert.match(html, /Dashboard Overview/);
  assert.match(html, /Recent Hall Pass Activity/);
  assert.match(html, /Sync Now/);
  assert.match(html, /Room 204/);
  assert.match(html, /Sandbox:/);
});

test("preserves the current demo behaviors behind the application provider", async () => {
  const source = await readFile(new URL("app/hallzee/HallzeeProvider.tsx", root), "utf8");
  for (const behavior of [
    "findTerminals",
    "connectTerminal",
    "syncNow",
    "toggleOccupancy",
    "exportCsv",
    "importRoster",
    "applyTerminalSettings",
  ]) assert.match(source, new RegExp(`\\b${behavior}\\b`));
});

test("keeps the route and application entry points thin", async () => {
  const [page, app] = await Promise.all([
    readFile(new URL("app/page.tsx", root), "utf8"),
    readFile(new URL("app/HallzeeApp.tsx", root), "utf8"),
  ]);
  assert.match(page, /<HallzeeApp \/>/);
  assert.match(app, /<HallzeeProvider>/);
  assert.match(app, /<AppShell \/>/);
});
