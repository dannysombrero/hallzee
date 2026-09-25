import { expect, type Page } from "@playwright/test";

/** Check the worker itself; offline readiness is no longer header decoration. */
export async function expectOfflineReady(page: Page) {
  const status = await page.evaluate(async () => {
    const registration = await navigator.serviceWorker.ready;
    return new Promise<string>((resolve, reject) => {
      const channel = new MessageChannel();
      const timer = setTimeout(() => { channel.port1.close(); reject(new Error("Offline shell did not become ready")); }, 10000);
      channel.port1.onmessage = event => { clearTimeout(timer); channel.port1.close(); resolve(event.data); };
      registration.active!.postMessage("OFFLINE_STATUS", [channel.port2]);
    });
  });
  expect(status).toBe("READY_OFFLINE");
}
