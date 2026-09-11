export function capabilities() {
  return {
    secure: window.isSecureContext,
    bluetooth: typeof navigator.bluetooth?.requestDevice === "function",
    rememberedDevices: typeof navigator.bluetooth?.getDevices === "function",
    storage: typeof indexedDB !== "undefined",
    crypto: !!crypto.subtle,
    locks: !!navigator.locks,
    serviceWorker: "serviceWorker" in navigator,
    pip: "documentPictureInPicture" in window,
  };
}
