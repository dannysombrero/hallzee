/// <reference lib="webworker" />
export {};
const worker = self as unknown as ServiceWorkerGlobalScope;
const BUILD = "__HALLZEE_BUILD__";
const ASSETS: string[] = JSON.parse("__HALLZEE_ASSETS__");
const CACHE = `hallzee-shell-${BUILD}`;
worker.addEventListener("install", (event) => {
  event.waitUntil(
    (async () => {
      // Fetch every asset before creating the release cache: partial downloads never replace it.
      const responses = await Promise.all(
        ASSETS.map(async (path) => {
          const response = await fetch(path, { cache: "reload", credentials: "omit" });
          if (!response.ok || response.type === "opaque") throw new Error("Incomplete shell");
          return new Response(await response.arrayBuffer(), {
            status: response.status,
            statusText: response.statusText,
            headers: response.headers,
          });
        }),
      );
      const cache = await caches.open(CACHE);
      await Promise.all(ASSETS.map((path, i) => cache.put(path, responses[i])));
    })(),
  );
});
worker.addEventListener("activate", (event) => {
  event.waitUntil(
    (async () => {
      await worker.clients.claim();
      const previous = (await caches.keys()).filter(
        (name) => name.startsWith("hallzee-shell-") && name !== CACHE,
      );
      for (const name of previous.slice(0, -1)) await caches.delete(name);
    })(),
  );
});
worker.addEventListener("message", (event) => {
  if (event.data === "OFFLINE_STATUS")
    event.waitUntil(
      (async () => {
        const cache = await caches.open(CACHE);
        const complete = (await Promise.all(ASSETS.map((path) => cache.match(path)))).every(
          Boolean,
        );
        if (complete) event.ports[0]?.postMessage("READY_OFFLINE");
      })(),
    );
  if (event.data === "APPLY_UPDATE")
    event.waitUntil(
      (async () => {
        const clients = await worker.clients.matchAll({
          type: "window",
          includeUncontrolled: true,
        });
        if (clients.length <= 1) await worker.skipWaiting();
        else event.source?.postMessage("CLOSE_OTHER_WINDOWS");
      })(),
    );
});
worker.addEventListener("fetch", (event) => {
  const url = new URL(event.request.url);
  if (url.origin !== worker.location.origin || event.request.method !== "GET") {
    event.respondWith(Promise.resolve(new Response("Blocked", { status: 403 })));
    return;
  }
  if (event.request.mode === "navigate") {
    event.respondWith(
      caches
        .open(CACHE)
        .then((cache) =>
          cache.match(ASSETS.includes(url.pathname) && !url.search ? url.pathname : "/index.html"),
        )
        .then(
          (response) =>
            response ?? new Response("Reconnect to finish installing Hallzee.", { status: 503 }),
        ),
    );
    return;
  }
  if (ASSETS.includes(url.pathname) && !url.search) {
    event.respondWith(
      caches
        .open(CACHE)
        .then((cache) => cache.match(url.pathname))
        .then((response) => response ?? new Response("Missing application asset", { status: 503 })),
    );
    return;
  }
  if (url.pathname === "/sw.js" && !url.search) return;
  event.respondWith(Promise.resolve(new Response("Not an application asset", { status: 403 })));
});
