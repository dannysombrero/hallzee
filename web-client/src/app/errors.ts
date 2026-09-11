export class HallzeeError extends Error {
  constructor(
    public code: string,
    message = code,
    public retryable = false,
  ) {
    super(message);
    this.name = "HallzeeError";
  }
}
export function abortCheck(signal?: AbortSignal) {
  if (signal?.aborted) throw new HallzeeError("CANCELLED");
}
export function deadline<T>(task: Promise<T>, signal?: AbortSignal, ms = 10000): Promise<T> {
  return new Promise((resolve, reject) => {
    const fail = () => finish(() => reject(new HallzeeError("CANCELLED")));
    const timer = setTimeout(
      () =>
        finish(() =>
          reject(
            new HallzeeError(
              "TIMEOUT",
              "The operation timed out. Retry when the terminal is ready.",
              true,
            ),
          ),
        ),
      ms,
    );
    const finish = (action: () => void) => {
      clearTimeout(timer);
      signal?.removeEventListener("abort", fail);
      action();
    };
    signal?.addEventListener("abort", fail, { once: true });
    if (signal?.aborted) fail();
    task.then(
      (value) => finish(() => resolve(value)),
      (error) => finish(() => reject(error)),
    );
  });
}
export function errorText(error: unknown): string {
  if (error instanceof HallzeeError) return error.message;
  if (error instanceof DOMException && error.name === "NotFoundError")
    return "No terminal selected. Choose a terminal when you are ready.";
  if (error instanceof DOMException && ["NotAllowedError", "SecurityError"].includes(error.name))
    return "Bluetooth permission is unavailable. Check browser site permissions or ask school IT.";
  if (error instanceof DOMException && error.name === "QuotaExceededError")
    return "The browser could not save local data because this profile has run out of storage. Free disk space and retry; do not clear Hallzee site data, which contains your records and owner key.";
  if (error instanceof DOMException && error.name === "DataCloneError")
    return "The browser could not save a required value, such as the owner key, in this profile. Use a regular browser profile (Chrome or Edge) and check its storage settings. No unconfirmed trip was acknowledged.";
  const category =
    error instanceof Error &&
    [
      "TypeError",
      "InvalidStateError",
      "InvalidAccessError",
      "OperationError",
      "NetworkError",
      "NotSupportedError",
      "AbortError",
      "UnknownError",
    ].includes(error.name)
      ? error.name
      : "UnknownError";
  return `The operation could not finish (${category}). Your saved records and ownership have been retained.`;
}
