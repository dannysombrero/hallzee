import type { DeviceHandle } from "./BluetoothPort";

export type PairingStatus = "Currently Paired" | "Not Paired" | "Paired to other device" | "Status unknown";
export interface DiscoveredTerminal {
  device?: DeviceHandle;
  terminalId?: string;
  name: string;
  pairingStatus: PairingStatus;
  hasSavedCredential?: boolean;
  inUse?: boolean;
}
