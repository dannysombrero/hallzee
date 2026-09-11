import { Signal } from "../app/events";
export interface OfflineStatus {
  ready: boolean;
  update: boolean;
  error: string | null;
}
export class UpdateCoordinator {
  readonly changed = new Signal<OfflineStatus>();
  status: OfflineStatus = { ready: false, update: false, error: null };
  private registration?: ServiceWorkerRegistration;
  private generation = 0;
  private cleanup: (() => void)[] = [];
  private applying = false;
  private publish(patch: Partial<OfflineStatus>) {
    this.status = { ...this.status, ...patch };
    this.changed.emit(this.status);
  }
  async start() {
    if (!("serviceWorker" in navigator) || !import.meta.env.PROD) return;
    const generation = ++this.generation;
    const current = () => generation === this.generation;
    const listen = (target: EventTarget, name: string, handler: EventListener) => {
      target.addEventListener(name, handler);
      this.cleanup.push(() => target.removeEventListener(name, handler));
    };
    try {
      const registration = await navigator.serviceWorker.register("/sw.js", { scope: "/" });
      if (!current()) return;
      this.registration = registration;
      const check = () => {
        if (current()) this.publish({ update: !!registration.waiting });
      };
      listen(registration, "updatefound", () => {
        if (registration.installing) listen(registration.installing, "statechange", check);
      });
      listen(navigator.serviceWorker, "message", (event) => {
        if ((event as MessageEvent).data === "CLOSE_OTHER_WINDOWS") {
          this.applying = false;
          this.publish({ error: "Close other Hallzee windows, then apply the update again." });
        }
      });
      listen(navigator.serviceWorker, "controllerchange", () => {
        if (this.applying) location.reload();
      });
      check();
      await navigator.serviceWorker.ready;
      if (!current()) return;
      const channel = new MessageChannel();
      this.cleanup.push(() => channel.port1.close());
      channel.port1.onmessage = (event) => {
        if (current() && event.data === "READY_OFFLINE") this.publish({ ready: true });
        channel.port1.close();
      };
      registration.active?.postMessage("OFFLINE_STATUS", [channel.port2]);
    } catch {
      if (current())
        this.publish({
          error:
            "Offline setup could not finish. Keep this tab open and retry with internet access.",
        });
    }
  }
  async check() {
    try {
      await this.registration?.update();
      this.publish({ update: !!this.registration?.waiting });
    } catch {
      this.publish({
        error: "Could not check for an update. Local classroom work is still available.",
      });
    }
  }
  apply() {
    if (!this.registration?.waiting) return;
    this.applying = true;
    this.publish({ error: null });
    this.registration.waiting.postMessage("APPLY_UPDATE");
  }
  dispose() {
    this.generation++;
    this.applying = false;
    for (const clean of this.cleanup) clean();
    this.cleanup = [];
    this.changed.clear();
  }
}
