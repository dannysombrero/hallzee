import { deadline, HallzeeError } from "../app/errors";
export class AutoReconnectCoordinator {
  private cycle?: AbortController;
  constructor(
    private connect: (signal: AbortSignal) => Promise<void>,
    private changed: (until: number | null) => void,
    private failed: (error: unknown) => void,
  ) {}
  start() {
    if (this.cycle) return;
    const control = new AbortController();
    this.cycle = control;
    const end = Date.now() + 45000;
    this.changed(end);
    void (async () => {
      let attempt = 0;
      while (!control.signal.aborted && Date.now() < end) {
        const attemptControl = new AbortController();
        const cancelAttempt = () => attemptControl.abort();
        control.signal.addEventListener("abort", cancelAttempt, { once: true });
        try {
          await deadline(
            this.connect(attemptControl.signal),
            control.signal,
            Math.max(1, end - Date.now()),
          );
          return;
        } catch (error) {
          if (control.signal.aborted) return;
          if (
            (error instanceof DOMException &&
              ["SecurityError", "NotAllowedError", "NotFoundError"].includes(error.name)) ||
            (error instanceof HallzeeError && !error.retryable)
          ) {
            this.failed(error);
            return;
          }
        } finally {
          control.signal.removeEventListener("abort", cancelAttempt);
          attemptControl.abort();
        }
        const wait = Math.min([2000, 5000, 10000, 15000][Math.min(attempt++, 3)], end - Date.now());
        if (wait <= 0) break;
        await new Promise<void>((resolve) => {
          const done = () => {
            clearTimeout(timer);
            control.signal.removeEventListener("abort", done);
            resolve();
          };
          const timer = setTimeout(done, wait);
          control.signal.addEventListener("abort", done, { once: true });
        });
      }
      if (!control.signal.aborted)
        this.failed(
          new HallzeeError(
            "RECONNECT_EXPIRED",
            "Could not reconnect. Retry or choose your saved terminal.",
            true,
          ),
        );
    })().finally(() => {
      if (this.cycle === control) {
        this.cycle = undefined;
        this.changed(null);
      }
    });
  }
  stop() {
    this.cycle?.abort();
    this.cycle = undefined;
    this.changed(null);
  }
}
