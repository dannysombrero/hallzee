export class RuntimeLock {
  private release?: () => void;
  private closed = false;
  acquire(): Promise<boolean> {
    return new Promise((resolve, reject) => {
      navigator.locks
        .request("hallzee-web-runtime", { ifAvailable: true }, async (lock) => {
          if (!lock || this.closed) {
            resolve(false);
            return;
          }
          await new Promise<void>((done) => {
            this.release = done;
            resolve(true);
          });
        })
        .catch(reject);
    });
  }
  close() {
    this.closed = true;
    this.release?.();
    this.release = undefined;
  }
}
