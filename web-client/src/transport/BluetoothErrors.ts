import { HallzeeError } from "../app/errors";
export type BluetoothStage =
  | "chooser"
  | "connect"
  | "service"
  | "characteristics"
  | "pairing"
  | "notifications"
  | "write";
const labels: Record<BluetoothStage, string> = {
  chooser: "choosing a terminal",
  connect: "connecting to the terminal",
  service: "finding the Hallzee service",
  characteristics: "finding the Hallzee channels",
  pairing: "establishing encrypted Bluetooth pairing",
  notifications: "subscribing to terminal updates",
  write: "sending a terminal command",
};
// Exact Chromium messages only: never interpolate a native message into UI or logs.
// Source: chromium/src third_party/blink/renderer/modules/bluetooth/bluetooth_error.cc
const nativeReasons = new Map<string, { label: string; help: string; retryable: boolean }>();
function reason(messages: string[], label: string, help: string, retryable = false) {
  for (const message of messages) nativeReasons.set(message, { label, help, retryable });
}
reason(
  [
    "Bluetooth permission has been blocked.",
    "User denied the browser permission to scan for Bluetooth devices.",
  ],
  "Bluetooth permission blocked",
  "Allow your browser (Chrome or Edge) in macOS System Settings → Privacy & Security → Bluetooth, then quit and reopen it. Also check the site's Bluetooth permission. On Windows, check system Bluetooth and browser permissions.",
);
reason(
  [
    "User or their enterprise policy has disabled Web Bluetooth.",
    "Web Bluetooth API globally disabled.",
  ],
  "Bluetooth disabled by browser or policy",
  "Check your browser's Bluetooth settings (Chrome or Edge). On a managed device, ask the administrator whether Web Bluetooth is allowed.",
);
reason(
  [
    "Authentication canceled.",
    "Authentication failed.",
    "Authentication rejected.",
    "Authentication timeout.",
    "GATT Error: Not paired.",
  ],
  "Bluetooth authentication incomplete",
  "The operating system could not complete Bluetooth authentication. For a saved terminal with updated firmware, hold * alone for five seconds while idle to show BT REPAIR. Choose the saved terminal and enter that code only in the OS prompt. Keep Hallzee site data and ownership.",
);
reason(
  ["Connection already in progress.", "GATT operation already in progress."],
  "Bluetooth operation already in progress",
  "Close other apps or tabs connecting to the terminal, wait for their connection attempt to finish, then retry.",
  true,
);
reason(
  ["Bluetooth Device is no longer in range."],
  "Terminal out of range",
  "Move the powered terminal closer and retry.",
  true,
);
reason(
  ["Bluetooth adapter not available.", "Bluetooth Low Energy not available."],
  "Bluetooth adapter unavailable",
  "Enable Bluetooth and check that this computer has a working Bluetooth Low Energy adapter.",
);
reason(
  [
    "Connection Error: Connection attempt failed.",
    "Unknown error when connecting to the device.",
    "Connection failed for unknown reason.",
  ],
  "Bluetooth connection failed",
  "Close other apps connected to the terminal. On Mac, disconnect the terminal in System Settings → Bluetooth, then retry in Hallzee. If needed, power-cycle the terminal without factory resetting it.",
  true,
);
/** Keep browser/device payloads out of diagnostics; only allowlisted error categories leave here. */
export function bluetoothError(error: unknown, stage: BluetoothStage): HallzeeError {
  if (error instanceof HallzeeError && error.code !== "TIMEOUT") return error;
  const names = [
    "NetworkError",
    "NotFoundError",
    "NotAllowedError",
    "SecurityError",
    "NotSupportedError",
    "InvalidStateError",
    "AbortError",
    "TimeoutError",
    "TypeError",
  ];
  const rawName = error instanceof Error ? error.name : "";
  const name =
    error instanceof HallzeeError
      ? "TimeoutError"
      : names.includes(rawName)
        ? rawName
        : "UnknownError";
  let help =
    "Keep the terminal nearby, check Bluetooth is enabled, and close other connected apps. On Mac, disconnect the terminal in System Settings → Bluetooth, then retry in Hallzee.";
  if (stage === "chooser" && name === "NotFoundError")
    help =
      "No terminal was selected. Open physical pairing mode if needed, then choose the terminal again.";
  else if (["NotAllowedError", "SecurityError"].includes(name))
    help =
      "Check browser site Bluetooth permission and macOS/Windows permission for Chrome or Edge. A district policy may also block access.";
  else if (stage === "pairing")
    help =
      "The encrypted Bluetooth read failed; this does not mean Hallzee ownership was lost. For a saved terminal with updated firmware, hold * alone for five seconds while idle to show BT REPAIR, choose the saved terminal, and enter that code in the OS prompt. For an unclaimed terminal, use its physical pairing screen. Do not reset ownership or clear Hallzee site data.";
  else if (["service", "characteristics"].includes(stage) && name === "NotFoundError")
    help =
      "The selected device does not expose the expected Hallzee firmware service or channels. Verify the selected terminal and firmware.";
  else if (name === "NotSupportedError")
    help =
      "The browser or Bluetooth adapter could not perform this operation. Try current Chrome or Edge with the Hallzee terminal nearby.";
  const knownReason = error instanceof Error ? nativeReasons.get(error.message) : undefined;
  if (knownReason) help = knownReason.help;
  return new HallzeeError(
    `BLUETOOTH_${stage.toUpperCase()}_${name.toUpperCase()}`,
    `Bluetooth failed while ${labels[stage]} (${stage} / ${name}). ${knownReason ? `${knownReason.label}. ` : ""}${help}`,
    knownReason?.retryable ?? ["NetworkError", "TimeoutError", "AbortError"].includes(name),
  );
}
