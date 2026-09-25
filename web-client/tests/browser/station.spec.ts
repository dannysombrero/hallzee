import { test, expect, type Page } from "@playwright/test";
import { expectOfflineReady } from "./offline-ready";

async function startRoom(page: Page, code: string) {
  await page.goto("/");
  await page.locator(".top-window-bar").getByRole("button", { name: "Start Virtual Terminal", exact: true }).click();
  await page.getByLabel("Terminal code", { exact: true }).fill(code);
  await page.getByRole("dialog").getByRole("button", { name: "Start Virtual Terminal", exact: true }).click();
  await expect(page.getByRole("dialog").getByText("Virtual terminal open", { exact: true })).toBeVisible();
}

test("header has two clear terminal actions and persistent, shareable join details", async ({ page, context }) => {
  const errors: string[] = [];
  page.on("pageerror", error => errors.push(error.message));
  await page.goto("/");
  const header = page.locator(".top-window-bar");
  await expect(header.getByRole("button")).toHaveCount(2);
  await expect(header).not.toContainText(/Desktop|Teacher workspace|Ready offline|v1\.0\.0/);
  await header.getByRole("button", { name: "Connect Bluetooth Terminal" }).click();
  await expect(page.getByRole("dialog")).not.toContainText("Virtual Web Terminal");
  await page.getByRole("button", { name: "Close dialog" }).click();
  await startRoom(page, "UI-2107");
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByRole("img", { name: "QR code to join this terminal" })).toBeVisible();
  await expect(dialog.locator(".room-direct-link")).toHaveText("http://localhost:4187/ui-2107");
  await context.grantPermissions(["clipboard-read", "clipboard-write"]);
  await dialog.getByRole("button", { name: "Copy Link", exact: true }).click();
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe("http://localhost:4187/ui-2107");
  await dialog.getByRole("button", { name: "Display Join Code" }).click();
  await expect(dialog).toHaveClass(/join-display-dialog/);
  await expect(dialog.locator(".room-code")).toHaveText("UI-2107");
  await page.screenshot({ path: "test-results/virtual-terminal-display.png", fullPage: true });
  await page.getByRole("button", { name: "Close dialog" }).click();
  await header.getByRole("button", { name: /Virtual Terminal Open/ }).click();
  await expect(page.getByRole("dialog").locator(".room-code")).toHaveText("UI-2107");
  await page.screenshot({ path: "test-results/virtual-terminal-share.png", fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  const bounds = await page.getByRole("dialog").boundingBox();
  expect(bounds!.x).toBeGreaterThanOrEqual(0);
  expect(bounds!.x + bounds!.width).toBeLessThanOrEqual(390);
  await page.screenshot({ path: "test-results/virtual-terminal-mobile.png", fullPage: true });
  await page.getByRole("dialog").getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await header.getByRole("button", { name: /Virtual Terminal Open/ }).click();
  page.once("dialog", confirmation => confirmation.accept());
  await page.getByRole("button", { name: "End Session", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  expect(errors).toEqual([]);
});

test("code entry, direct links, refresh, check-in, and terminal switching work together", async ({ page, context }) => {
  const errors: string[] = [];
  page.on("pageerror", error => errors.push(error.message));
  await startRoom(page, "FLOW-2107");
  await page.getByRole("button", { name: "Done", exact: true }).click();
  const station = await context.newPage();
  station.on("pageerror", error => errors.push(error.message));
  await station.goto("/join");
  await station.screenshot({ path: "test-results/join-page.png", fullPage: true });
  await station.getByLabel("Terminal code").fill("flow-2107");
  await station.getByRole("button", { name: "Join Terminal" }).click();
  await expect(station).toHaveURL(/\/flow-2107$/);
  await expect(station.getByRole("heading", { name: "FLOW-2107" })).toBeVisible();
  await station.getByLabel("Student ID", { exact: true }).fill("101");
  await station.getByRole("button", { name: "CHECK OUT →", exact: true }).click();
  await expect(station.getByText("Have a good trip! Your pass is active.")).toBeVisible();
  await expect(page.getByRole("heading", { name: "1 student out" })).toBeVisible();
  await page.reload();
  await expect(page.locator(".top-window-bar").getByRole("button", { name: /Virtual Terminal Open/ })).toBeVisible();
  await expect(page.getByRole("heading", { name: "1 student out" })).toBeVisible();
  await page.locator(".top-window-bar").getByRole("button", { name: "Connect Bluetooth Terminal" }).click();
  await expect(page.getByRole("button", { name: "End Session & Connect Bluetooth" })).toBeDisabled();
  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  await station.getByRole("button", { name: "Check in", exact: true }).click();
  await station.getByLabel("Student ID", { exact: true }).fill("101");
  await station.getByRole("button", { name: "✓ I'M BACK (CHECK IN)", exact: true }).click();
  await expect(station.getByText("Welcome back! You are checked in.")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Pass available", exact: true })).toBeVisible();
  await expect(page.locator(".recent-trip-row")).toHaveCount(1);
  await page.locator(".top-window-bar").getByRole("button", { name: "Connect Bluetooth Terminal" }).click();
  await page.getByRole("button", { name: "End Session & Connect Bluetooth" }).click();
  await expect(page.getByRole("dialog", { name: "Connect Bluetooth Terminal", exact: true })).toBeVisible();
  await expect(station.getByText("TERMINAL CLOSED", { exact: true }).first()).toBeVisible();
  await expect(station.getByRole("button", { name: "✓ I'M BACK (CHECK IN)", exact: true })).toBeDisabled();
  expect(errors).toEqual([]);
});

test("a missing room cannot claim to be available or confirm a pass", async ({ page }) => {
  await page.goto("/pass/MISSING-404");
  await expect(page.getByRole("heading", { name: "MISSING-404" })).toBeVisible();
  await page.getByLabel("Student ID", { exact: true }).fill("101");
  await expect(page.getByRole("button", { name: "CHECK OUT →", exact: true })).toBeDisabled();
  await expect(page.getByText("Have a good trip! Your pass is active.")).toHaveCount(0);
  await expect(page.getByText(/PASS AVAILABLE/)).toHaveCount(0);
});

test("room-code conflicts show an error instead of an open terminal", async ({ page, browser }) => {
  await startRoom(page, "TAKEN-2107");
  const secondContext = await browser.newContext();
  try {
    const other = await secondContext.newPage();
    await other.goto("/");
    await other.locator(".top-window-bar").getByRole("button", { name: "Start Virtual Terminal", exact: true }).click();
    await other.getByLabel("Terminal code", { exact: true }).fill("TAKEN-2107");
    await other.getByRole("dialog").getByRole("button", { name: "Start Virtual Terminal", exact: true }).click();
    await expect(other.getByRole("alert")).toContainText("already in use");
    await expect(other.locator(".top-window-bar")).not.toContainText("Virtual Terminal Open");
  } finally { await secondContext.close(); }
});

test("Exceeded Time controls change dates, search periods, and sort names", async ({ page }) => {
  await page.goto("/");
  await expectOfflineReady(page);
  await page.evaluate(async () => {
    const db = await new Promise<IDBDatabase>(resolve => { const r = indexedDB.open("hallzee-web"); r.onsuccess = () => resolve(r.result); });
    const workspaces = await new Promise<any[]>(resolve => { const r = db.transaction("workspaces").objectStore("workspaces").getAll(); r.onsuccess = () => resolve(r.result); });
    const workspaceId = workspaces[0].workspaceId;
    const date = (days: number) => { const d = new Date(); d.setDate(d.getDate() - days); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`; };
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction(["roster_students", "trips"], "readwrite");
      for (const [studentId, firstName] of [["101", "Zoe"], ["102", "Alex"]])
        tx.objectStore("roster_students").put({ workspaceId, studentId, firstName, lastName: "Example", grade: "7", active: true });
      for (const [tripId, studentId, ago, classSection] of [[1, "101", 0, "Period 2"], [2, "101", 6, "Period 2"], [3, "102", 10, "Period 10"], [4, "102", 20, "Period 10"]] as const)
        tx.objectStore("trips").put({ tripId, studentId, tripDate: date(ago), classSection, durationSeconds: 600,
          status: "COMPLETE", receivedWorkspaceId: workspaceId, terminalId: "LOCAL", timeOut: "09:00:00", timeIn: "09:10:00", receivedAtUtc: new Date().toISOString(), scheduleName: null, contextSource: "resolved-on-receipt" });
      tx.oncomplete = () => resolve(); tx.onabort = () => reject(tx.error);
    }); db.close();
  });
  await page.reload();
  const rows = page.locator(".exceeded-item-row");
  await expect(rows).toHaveCount(2);
  await expect(rows.first()).toContainText("Zoe Example");
  await page.getByRole("button", { name: "Name", exact: true }).click();
  await expect(rows.first()).toContainText("Alex Example");
  await page.getByRole("button", { name: "Count", exact: true }).click();
  await expect(rows.first()).toContainText("Zoe Example");
  await page.getByLabel("Filter exceeded students").fill("Period 10");
  await expect(rows).toHaveCount(1);
  await expect(rows.first()).toContainText("Alex Example");
  await page.getByLabel("Filter exceeded students").fill("");
  await page.getByLabel("Exceeded time timeframe").selectOption("1");
  await expect(rows).toHaveCount(1);
  await expect(rows.first()).toContainText("Zoe Example");
  await page.getByLabel("Exceeded time threshold").selectOption("15");
  await expect(rows).toHaveCount(0);
});
