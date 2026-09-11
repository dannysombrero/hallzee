import "fake-indexeddb/auto";
import { Signal } from "../../src/app/events";
import { LocalDatabase } from "../../src/storage/LocalDatabase";
import { CredentialRepository } from "../../src/storage/CredentialRepository";
import { WorkspaceRepository } from "../../src/storage/WorkspaceRepository";
import { WebTerminalSession } from "../../src/protocol/WebTerminalSession";
import type { BluetoothPort, DeviceHandle } from "../../src/transport/BluetoothPort";
export class FakePort implements BluetoothPort {
  lines = new Signal<string>();
  drops = new Signal<void>();
  sent: string[] = [];
  connected = false;
  onCommand: (line: string) => void | Promise<void> = () => {};
  requestDevice = async () => ({ id: "test-device" });
  getRememberedDevices = async () => [{ id: "test-device" }];
  onLine = this.lines.subscribe;
  onDisconnect = this.drops.subscribe;
  async connect(_device: DeviceHandle) {
    this.connected = true;
  }
  async sendLine(line: string) {
    this.sent.push(line);
    await this.onCommand(line);
  }
  disconnect() {
    this.connected = false;
  }
}
export async function setup() {
  const db = await LocalDatabase.open("unit-" + crypto.randomUUID());
  const credentials = new CredentialRepository(db);
  const workspace = await new WorkspaceRepository(db).ensure();
  const port = new FakePort();
  const session = new WebTerminalSession(port, credentials);
  return { db, credentials, workspace, port, session };
}
export const tick = () => new Promise<void>((resolve) => setTimeout(resolve, 0));
