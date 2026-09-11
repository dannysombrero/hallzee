export interface DeviceHandle {
  id: string;
  name?: string;
}
export interface BluetoothPort {
  requestDevice(): Promise<DeviceHandle>;
  getRememberedDevices(): Promise<DeviceHandle[]>;
  connect(device: DeviceHandle, signal?: AbortSignal): Promise<void>;
  sendLine(line: string, signal?: AbortSignal): Promise<void>;
  onLine(listener: (line: string) => void): () => void;
  onDisconnect(listener: () => void): () => void;
  disconnect(): void;
}
