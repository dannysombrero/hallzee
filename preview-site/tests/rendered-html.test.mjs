import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const root = new URL("../", import.meta.url);

async function render(path = "/") {
  const workerUrl = new URL("../dist/server/index.js", import.meta.url);
  workerUrl.searchParams.set("test", `${process.pid}-${Date.now()}`);
  const { default: worker } = await import(workerUrl.href);
  return worker.fetch(
    new Request(`http://localhost${path}`, { headers: { accept: "text/html" } }),
    { ASSETS: { fetch: async () => new Response("Not found", { status: 404 }) } },
    { waitUntil() {}, passThroughOnException() {} },
  );
}

test("server-renders the Hallzee teacher website on root route", async () => {
  const response = await render("/");
  assert.equal(response.status, 200);
  const html = await response.text();
  assert.match(html, /Hallzee/i);
  assert.match(html, /The hall pass system that handles everything for you/i);
  assert.match(html, /Who’s out|Who&#x27;s out|Who's out/);
});

test("server-renders the Hallzee dashboard preview on /preview", async () => {
  const response = await render("/preview");
  assert.equal(response.status, 200);
  const html = await response.text();
  assert.match(html, /Dashboard Overview/);
  assert.match(html, /Recent Hall Pass Activity/);
  assert.match(html, /Sync[\s\S]*?Now/);
  assert.match(html, /Configure Node/);
  assert.match(html, /Export CSV/);
  assert.match(html, /Roster (&amp;|&) Pass Policy/);
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
    "openModal",
    "closeModal",
  ]) {
    assert.match(source, new RegExp(`\\b${behavior}\\b`));
  }
});

test("cancels terminal work safely and keeps the occupied duration live", async () => {
  const source = await readFile(new URL("app/hallzee/HallzeeProvider.tsx", root), "utf8");

  assert.match(source, /connectionTimerRef/);
  assert.match(source, /setTerminalState\(previousState\)/);
  assert.match(source, /setConnectingTerminalId\(null\)/);
  assert.match(source, /trip\.status === "OCCUPIED"[\s\S]*durationSeconds: trip\.durationSeconds \+ 1/);
  assert.match(source, /setActiveModal\(null\);[\s\S]*setTerminalState\("disconnected"\)/);
});

test("keeps the route and application entry points thin", async () => {
  const [page, app] = await Promise.all([
    readFile(new URL("app/preview/page.tsx", root), "utf8"),
    readFile(new URL("app/HallzeeApp.tsx", root), "utf8"),
  ]);
  assert.match(page, /<HallzeeApp \/>/);
  assert.match(app, /<HallzeeProvider>/);
  assert.match(app, /<AppShell \/>/);
});

test("provides dedicated modal dialog components for left nav actions", async () => {
  const [tripsModal, rosterModal, policiesModal, terminalModal, settingsModal] = await Promise.all([
    readFile(new URL("app/hallzee/components/TripsModal.tsx", root), "utf8"),
    readFile(new URL("app/hallzee/components/RosterModal.tsx", root), "utf8"),
    readFile(new URL("app/hallzee/components/PoliciesModal.tsx", root), "utf8"),
    readFile(new URL("app/hallzee/components/TerminalSettingsModal.tsx", root), "utf8"),
    readFile(new URL("app/hallzee/components/SettingsModal.tsx", root), "utf8"),
  ]);
  assert.match(tripsModal, /ModalDialog/);
  assert.match(rosterModal, /ModalDialog/);
  assert.match(policiesModal, /ModalDialog/);
  assert.match(terminalModal, /ModalDialog/);
  assert.match(settingsModal, /ModalDialog/);
});
