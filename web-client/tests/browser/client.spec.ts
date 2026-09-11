import { test, expect } from "@playwright/test";
import { installBluetooth } from "./fake-bluetooth";
test("offline shell, local roster, backup restore, projection privacy and second-window lock", async ({
  page,
  context,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  const requests: { url: string; method: string; data: string; headers: string }[] = [];
  context.on("request", (r) =>
    requests.push({
      url: r.url(),
      method: r.method(),
      data: r.postData() ?? "",
      headers: JSON.stringify(r.headers()),
    }),
  );
  await page.goto("/");
  await expect(page.getByText("Ready offline", { exact: true })).toBeVisible();
  await page.getByRole("navigation").getByRole("button", { name: "Student roster" }).click();
  await page.getByRole("button", { name: "Add student", exact: true }).click();
  await page.getByLabel("Student ID", { exact: true }).fill("00123");
  await page.getByLabel("First name", { exact: true }).fill("Alex");
  await page.getByLabel("Last name", { exact: true }).fill("Rivera");
  await page.getByRole("button", { name: "Save student", exact: true }).click();
  await expect(page.getByRole("cell", { name: "Alex Rivera", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Edit Alex Rivera", exact: true }).click();
  await page.getByLabel("First name", { exact: true }).fill("Alexa");
  await expect(page.getByLabel("Student ID", { exact: true })).toBeDisabled();
  await page.getByRole("button", { name: "Save student", exact: true }).click();
  await page.keyboard.press("Escape");
  const second = await context.newPage();
  await second.goto("/");
  await expect(
    second.getByText("Hallzee is already open in another window.", { exact: true }),
  ).toBeVisible();
  await second.close();
  await page.getByRole("button", { name: "Classroom & data", exact: true }).click();
  page.on("dialog", (d) => d.accept());
  const pending = page.waitForEvent("download");
  await page.getByRole("button", { name: "Download classroom backup", exact: true }).click();
  const download = await pending;
  const path = await download.path();
  expect(path).toBeTruthy();
  await page.getByLabel("Restore a web-client backup").setInputFiles(path!);
  await expect(page.getByText("Review replacement", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Replace local classroom data", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await context.setOffline(true);
  await page.reload();
  await expect(page.getByText("Ready offline", { exact: true })).toBeVisible();
  await page.getByRole("navigation").getByRole("button", { name: "Student roster" }).click();
  await expect(page.getByRole("cell", { name: "Alexa Rivera", exact: true })).toBeVisible();
  await page.keyboard.press("Escape");
  await page.getByRole("button", { name: "Projection view", exact: true }).click();
  await expect(page.locator("body")).not.toContainText("Alexa");
  await expect(page.locator("body")).not.toContainText("00123");
  expect(errors).toEqual([]);
  for (const request of requests) {
    const url = new URL(request.url);
    expect(url.origin).toBe("http://localhost:4187");
    expect(request.method).toBe("GET");
    expect(url.search).toBe("");
    expect(url.pathname).toMatch(
      /^\/(?:$|index\.html$|sw\.js$|build\.json$|manifest\.webmanifest$|assets\/|icons\/|licenses\/|LICENSE\.txt$|COPYRIGHT\.txt$|THIRD-PARTY-NOTICES\.md$|source\.txt$)/,
    );
    expect(JSON.stringify(request)).not.toMatch(/Alexa|Rivera|00123/);
  }
});
test("real transport claims, stores CryptoKey, ACKs committed trips and authenticates after offline reload", async ({
  page,
  context,
}) => {
  await installBluetooth(page);
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto("/");
  await expect(page.getByText("Ready offline", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Connect terminal", exact: true }).click();
  await page.getByLabel("Physical pairing code").fill("807481");
  await page.getByRole("checkbox").check();
  await page.getByRole("button", { name: "Choose terminal and pair", exact: true }).click();
  await expect(page.getByText("Pass available", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Sync now", exact: true })).toBeEnabled();
  expect(await page.evaluate(() => (window as any).terminalCommands.includes("ACK,1"))).toBe(true);
  const credential = await page.evaluate(async () => {
    const db = await new Promise<IDBDatabase>((resolve) => {
      const r = indexedDB.open("hallzee-web");
      r.onsuccess = () => resolve(r.result);
    });
    const record: any = await new Promise((resolve) => {
      const r = db.transaction("credentials").objectStore("credentials").get("HZ-A1B2C3D4E5F6");
      r.onsuccess = () => resolve(r.result);
    });
    db.close();
    return {
      isKey: record.key instanceof CryptoKey,
      extractable: record.key.extractable,
      state: record.state,
    };
  });
  expect(credential).toEqual({ isKey: true, extractable: false, state: "confirmed" });
  await context.setOffline(true);
  await page.reload();
  await expect(page.getByText("Pass available", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Sync now", exact: true })).toBeEnabled();
  const commands = await page.evaluate(() => (window as any).terminalCommands as string[]);
  expect(commands.some((c) => c.startsWith("AUTH,2,"))).toBe(true);
  expect(commands.some((c) => c.startsWith("CLAIM,"))).toBe(false);
  expect(commands.find((c) => c.startsWith("TIME_CURSOR"))).toMatch(/,1$/);
  expect(errors).toEqual([]);
});
test("security headers and unsupported Bluetooth", async ({ page }) => {
  await page.addInitScript(() =>
    Object.defineProperty(navigator, "bluetooth", { value: undefined, configurable: true }),
  );
  const response = await page.goto("/");
  expect(response?.headers()["content-security-policy"]).toContain("connect-src 'self'");
  await page.getByRole("button", { name: "Connect terminal", exact: true }).click();
  await page.getByLabel("Physical pairing code").fill("807481");
  await page.getByRole("checkbox").check();
  await page.getByRole("button", { name: "Choose terminal and pair", exact: true }).click();
  await expect(page.getByRole("dialog").getByRole("alert")).toContainText(
    "Web Bluetooth is unavailable",
  );
});

test("targeted check-in retains other active passes; confirmed release removes key but retains history", async ({
  page,
}) => {
  await installBluetooth(page);
  await page.goto("/");
  await expect(page.getByText("Ready offline", { exact: true })).toBeVisible();
  await page.evaluate(() => {
    (window as any).fakePasses = [
      { studentId: "00123", epoch: 1789117200 },
      { studentId: "00999", epoch: 1789117260 },
    ];
  });
  await page.getByRole("button", { name: "Connect terminal", exact: true }).click();
  await page.getByLabel("Physical pairing code").fill("807481");
  await page.getByRole("checkbox").check();
  await page.getByRole("button", { name: "Choose terminal and pair", exact: true }).click();
  await expect(page.getByText("2 students out", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Sync now", exact: true })).toBeEnabled();
  page.on("dialog", (d) => d.accept());
  await page
    .locator(".active-student")
    .filter({ hasText: "00123" })
    .getByRole("button", { name: "Check in", exact: true })
    .click();
  await expect(page.getByText("1 student out", { exact: true })).toBeVisible();
  await expect(page.locator(".active-student")).toContainText("00999");
  await page
    .locator(".active-student")
    .getByRole("button", { name: "Check in", exact: true })
    .click();
  await expect(page.getByText("Pass available", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Terminal settings", exact: true }).click();
  await page.getByRole("button", { name: "Disconnect & Unpair", exact: true }).click();
  await expect
    .poll(() =>
      page.evaluate(async () => {
        const db = await new Promise<IDBDatabase>((resolve) => {
          const r = indexedDB.open("hallzee-web");
          r.onsuccess = () => resolve(r.result);
        });
        const counts = await Promise.all(
          ["credentials", "trips"].map(
            (store) =>
              new Promise<number>((resolve) => {
                const r = db.transaction(store).objectStore(store).count();
                r.onsuccess = () => resolve(r.result);
              }),
          ),
        );
        db.close();
        return counts;
      }),
    )
    .toEqual([0, 1]);
});
