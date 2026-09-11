import { HallzeeError } from "../app/errors";
export class LineFramer {
  private bytes: number[] = [];
  push(value: DataView | Uint8Array): string[] {
    const chunk = new Uint8Array(value.buffer, value.byteOffset, value.byteLength);
    const lines: string[] = [];
    for (const byte of chunk) {
      if (byte === 13) continue;
      if (byte === 10) {
        try {
          lines.push(new TextDecoder("utf-8", { fatal: true }).decode(new Uint8Array(this.bytes)));
        } catch {
          this.reset();
          throw new HallzeeError("INVALID_UTF8", "The terminal sent an invalid message.");
        }
        this.bytes = [];
      } else {
        this.bytes.push(byte);
        if (this.bytes.length > 4096) {
          this.reset();
          throw new HallzeeError("INPUT_OVERFLOW", "The terminal message is too long.");
        }
      }
    }
    return lines;
  }
  reset() {
    this.bytes = [];
  }
}
export function commandBytes(line: string) {
  const bytes = new TextEncoder().encode(line);
  if (/[\r\n]/.test(line) || !line || bytes.length > 192) throw new HallzeeError("INVALID_COMMAND");
  return new TextEncoder().encode(line + "\n");
}
