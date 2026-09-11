import { test, expect } from "@playwright/test";
import { createServer, type Server } from "node:http";
import { readFile } from "node:fs/promises";
let server: Server;
let version = "A";
let interrupted = false;
const origin = "http://localhost:4188";
test.beforeAll(async () => {
  const root = new URL("../../dist/", import.meta.url);
  const source = await readFile(new URL("sw.js", root), "utf8");
  server = createServer(async (req, res) => {
    try {
      const path = new URL(req.url!, origin).pathname;
      res.setHeader("Cache-Control", "no-store");
      if (path === "/update-marker.js") {
        res.statusCode = interrupted ? 503 : 200;
        res.setHeader("Content-Type", "text/javascript");
        res.end("// Complete update marker");
        return;
      }
      if (path === "/sw.js") {
        res.setHeader("Content-Type", "text/javascript");
        res.end(
          source
            .replace(/const BUILD\s*=\s*["'][^"']+["']/, `const BUILD='test-${version}'`)
            .replace("const ASSETS", "const ASSETS")
            .replace(
              /JSON.parse\(("(?:\\.|[^"\\])*")\)/,
              (_, json) =>
                `JSON.parse('${JSON.stringify(version === "A" ? JSON.parse(JSON.parse(json)) : [...JSON.parse(JSON.parse(json)), "/update-marker.js"])}')`,
            ),
        );
        return;
      }
      const name = path === "/" ? "index.html" : path.slice(1);
      if (name.includes("..")) {
        res.writeHead(403).end();
        return;
      }
      let data = await readFile(new URL(name, root));
      if (name === "index.html")
        data = Buffer.from(
          data
            .toString()
            .replace("</head>", `<meta name="test-build" content="${version}"></head>`),
        );
      const ext = name.split(".").at(-1)!;
      res.setHeader(
        "Content-Type",
        (
          {
            js: "text/javascript",
            css: "text/css",
            html: "text/html",
            json: "application/json",
            webmanifest: "application/manifest+json",
            svg: "image/svg+xml",
            png: "image/png",
          } as Record<string, string>
        )[ext] ?? "text/plain",
      );
      res.end(data);
    } catch {
      res.writeHead(404).end();
    }
  });
  await new Promise<void>((resolve) => server.listen(4188, "localhost", resolve));
});
test.afterAll(async () => {
  await new Promise<void>((resolve, reject) =>
    server.close((error) => (error ? reject(error) : resolve())),
  );
});
test("partial update keeps A; explicit B activation preserves IndexedDB and nonextractable owner key", async ({
  page,
  context,
}) => {
  await page.goto(origin);
  await expect(page.getByText("Ready offline", { exact: true })).toBeVisible();
  await page.evaluate(async () => {
    const db = await new Promise<IDBDatabase>((resolve) => {
      const r = indexedDB.open("hallzee-web");
      r.onsuccess = () => resolve(r.result);
    });
    const key = await crypto.subtle.generateKey({ name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction(["app_meta", "credentials"], "readwrite", { durability: "strict" });
      tx.objectStore("app_meta").put({ key: "update-canary", value: "local-only" });
      tx.objectStore("credentials").put({
        terminalId: "HZ-A1B2C3D4E5F6",
        clientId: "12345678-1234-1234-1234-1234567890AB",
        key,
        state: "confirmed",
        createdAtUtc: new Date().toISOString(),
      });
      tx.oncomplete = () => resolve();
      tx.onabort = () => reject(tx.error);
    });
    db.close();
  });
  version = "B";
  interrupted = true;
  await page.evaluate(async () => {
    const r = await navigator.serviceWorker.getRegistration();
    await r!.update();
    await new Promise<void>((resolve) => {
      const worker = r!.installing;
      if (!worker) {
        resolve();
        return;
      }
      worker.addEventListener("statechange", () => {
        if (worker.state === "redundant") resolve();
      });
    });
  });
  await context.setOffline(true);
  await page.reload();
  await expect(page.locator('meta[name="test-build"]')).toHaveAttribute("content", "A");
  await expect(page.getByText("Ready offline", { exact: true })).toBeVisible();
  await context.setOffline(false);
  interrupted = false;
  await page.getByRole("button", { name: "Check updates", exact: true }).click();
  await expect(page.getByText("Update ready.", { exact: true })).toBeVisible();
  await expect(page.locator('meta[name="test-build"]')).toHaveAttribute("content", "A");
  const second = await context.newPage();
  await second.goto(origin);
  page.on("dialog", (d) => d.accept());
  await page.getByRole("button", { name: "Apply update", exact: true }).click();
  await expect(
    page.getByText("Close other Hallzee windows, then apply the update again.", { exact: true }),
  ).toBeVisible();
  await expect(page.locator('meta[name="test-build"]')).toHaveAttribute("content", "A");
  await second.close();
  await page.getByRole("button", { name: "Apply update", exact: true }).click();
  await expect(page.locator('meta[name="test-build"]')).toHaveAttribute("content", "B");
  await expect(page.getByText("Ready offline", { exact: true })).toBeVisible();
  const saved = await page.evaluate(async () => {
    const db = await new Promise<IDBDatabase>((resolve) => {
      const r = indexedDB.open("hallzee-web");
      r.onsuccess = () => resolve(r.result);
    });
    const get = (store: string, id: string) =>
      new Promise<any>((resolve) => {
        const r = db.transaction(store).objectStore(store).get(id);
        r.onsuccess = () => resolve(r.result);
      });
    const meta = await get("app_meta", "update-canary"),
      credential = await get("credentials", "HZ-A1B2C3D4E5F6");
    db.close();
    return {
      value: meta.value,
      key: credential.key instanceof CryptoKey,
      extractable: credential.key.extractable,
    };
  });
  expect(saved).toEqual({ value: "local-only", key: true, extractable: false });
  await context.setOffline(true);
  await page.reload();
  await expect(page.locator('meta[name="test-build"]')).toHaveAttribute("content", "B");
});
