import { HallzeeError } from "../app/errors";
export type BluetoothStage =
  | "chooser"
  | "connect"
  | "service"
  | "characteristics"
  | "pairing"
  | "pairing-write"
  | "security-request"
  | "pairing-resume"
  | "notifications"
  | "write";
const labels: Record<BluetoothStage, string> = {
  chooser: "choosing a terminal",
  connect: "connecting to the terminal",
  service: "finding the Hallzee service",
  characteristics: "finding the Hallzee channels",
  pairing: "establishing encrypted Bluetooth pairing",
  "pairing-write": "establishing encrypted Bluetooth through the write channel",
  "security-request": "asking the terminal to restore Bluetooth encryption",
  "pairing-resume": "waiting for the terminal to restore Bluetooth encryption",
  notifications: "subscribing to terminal updates",
  write: "sending a terminal command",
};
export function bluetoothDisconnected(stage?: BluetoothStage): HallzeeError {
  return new HallzeeError("DISCONNECTED", stage
    ? `The terminal disconnected while ${labels[stage]} (${stage} / Disconnected). The Bluetooth operation did not finish. Retry in Hallzee; if it repeats, report this stage. The browser's Paired badge does not confirm a Hallzee connection.`
    : "The terminal disconnected. Reconnect in Hallzee.", true);
}
// Exact Chromium messages only: never interpolate a native message into UI or logs.
// Source: chromium/src third_party/blink/renderer/modules/bluetooth/bluetooth_error.cc
const nativeReasons = new Map<string, { label: string; help: string; retryable: boolean }>();
function reason(messages: string[], label: string, help: string, retryable = false) {
  for (const message of messages) nativeReasons.set(message, { label, help, retryable });
}
// Chromium maps all five to NotSupportedError. Preserve their distinction
// using fixed labels; the native message itself must never be displayed.
for (const [message, label] of [
  ["GATT Error Unknown.", "GATT_UNKNOWN_ERROR"],
  ["GATT operation failed for unknown reason.", "GATT_UNKNOWN_FAILURE"],
  ["GATT operation not permitted.", "GATT_NOT_PERMITTED"],
  ["GATT Error: Not supported.", "GATT_NOT_SUPPORTED"],
  ["GATT Error: Unknown GattErrorCode.", "GATT_UNTRANSLATED_ERROR"],
]) reason([message], label,
  "The terminal or operating system rejected the Bluetooth operation. Keep Hallzee site data and ownership. If it repeats, report this stage and GATT category with your OS/browser version.");
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
  "The operating system could not complete Bluetooth authentication. For a saved terminal with updated firmware, hold * alone for five seconds while idle to show BT REPAIR. Then select the saved terminal in Hallzee. Updated firmware reconnects without an OS passkey. Keep Hallzee site data and ownership.",
);
const busyMessages = ["Connection already in progress.", "GATT operation already in progress."];
export function isBluetoothBusy(error: unknown): boolean {
  return error instanceof Error && error.name === "NetworkError" && busyMessages.includes(error.message);
}
reason(
  busyMessages,
  "Bluetooth operation already in progress",
  "Chrome or the operating system is still finishing a Bluetooth operation. This can happen with only this Hallzee tab open. Wait briefly, then retry. If it persists, close and reopen Hallzee; keep its site data and saved ownership.",
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
  "Close other apps connected to the terminal. On Chromebook, disconnect or forget in Quick Settings → Bluetooth; on Mac, disconnect in System Settings → Bluetooth. Then retry in Hallzee. If needed, power-cycle the terminal without factory resetting it.",
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
    "Keep the terminal nearby, check Bluetooth is enabled, and close other connected apps. On Chromebook, check Quick Settings → Bluetooth; on Mac, disconnect in System Settings → Bluetooth. Then retry in Hallzee.";
  if (stage === "chooser" && name === "NotFoundError")
    help =
      "No terminal was selected. Open physical pairing mode if needed, then choose the terminal again.";
  else if (["NotAllowedError", "SecurityError"].includes(name))
    help =
      "Check browser site Bluetooth permission and macOS/Windows permission for Chrome or Edge. A district policy may also block access.";
  else if (stage === "connect" && name === "TimeoutError")
    help =
      "The Bluetooth connection attempt did not finish before Hallzee's deadline. This happened before service discovery, encryption or Hallzee authentication. Chrome may still be finishing this same attempt, even with only one tab open. If retry reports a pending operation, close this Hallzee tab/app completely and reopen the same URL in the same browser profile. If it stays stuck, restart the Chromebook/computer and power-cycle the terminal. Keep Hallzee site data and ownership; no pairing code or pairing mode is needed for a saved owner.";
  else if (stage === "pairing")
    help =
      "The encrypted Bluetooth read failed; this does not mean Hallzee ownership was lost. For a saved terminal with updated firmware, hold * alone for five seconds while idle to show BT REPAIR, then choose the saved terminal in Hallzee; updated firmware needs no OS passkey. For an unclaimed terminal, update firmware and enter its physical pairing code in Hallzee after selection. Do not reset ownership or clear Hallzee site data.";
  else if (stage === "pairing-write")
    help = "The encrypted read was rejected, and the encrypted write also failed. A saved owner needs no pairing code or pairing mode, including after a terminal power cycle. Update terminal firmware and Hallzee for encryption recovery, then reconnect in the original browser profile. Keep Hallzee site data and ownership; report this stage and your OS/browser version if it repeats.";
  else if (stage === "security-request" || stage === "pairing-resume")
    help = "Bluetooth encryption could not be restored. A saved owner needs no pairing code or pairing mode. Reconnect in the original Hallzee browser profile and keep its site data and ownership. If it repeats, capture the filtered BLE diagnostics; this error alone does not prove the bond was lost.";
  else if (["service", "characteristics"].includes(stage) && name === "NotFoundError")
    help =
      "The selected device does not expose the expected Hallzee firmware service or channels. Verify the selected terminal and firmware.";
  else if (name === "NotSupportedError")
    help =
      "The browser or Bluetooth adapter could not perform this operation. Try current Chrome or Edge with the Hallzee terminal nearby.";
  const knownReason = error instanceof Error ? nativeReasons.get(error.message) : undefined;
  if (knownReason && !["pairing-write", "security-request", "pairing-resume"].includes(stage)) help = knownReason.help;
  return new HallzeeError(
    `BLUETOOTH_${stage.replaceAll("-", "_").toUpperCase()}_${name.toUpperCase()}`,
    `Bluetooth failed while ${labels[stage]} (${stage} / ${name}). ${knownReason ? `${knownReason.label}. ` : ""}${help}`,
    knownReason?.retryable ?? ["NetworkError", "TimeoutError", "AbortError"].includes(name),
  );
}
