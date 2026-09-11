import { parsePasses, type ActivePass } from "../protocol/messages";
import { HallzeeError } from "../app/errors";
export class ActivePassStore {
  passes: ActivePass[] = [];
  fresh = false;
  singlePass = false;
  unknown() {
    this.fresh = false;
  }
  snapshot(line: string) {
    this.passes = parsePasses(line);
    this.fresh = true;
  }
  event(line: string) {
    const f = line.split(",");
    if (
      f.length !== 4 ||
      !["CHECKOUT", "CHECKIN", "RESET"].includes(f[1]) ||
      !/^\d{1,16}$/.test(f[2]) ||
      !/^\d+$/.test(f[3]) ||
      !Number.isSafeInteger(Number(f[3]))
    )
      throw new HallzeeError("INVALID_EVENT");
    if (f[1] === "CHECKOUT" && Number(f[3]) === 0) {
      this.unknown();
      return;
    }
    if (!this.fresh) return;
    this.passes = this.passes.filter((p) => p.studentId !== f[2]);
    if (f[1] === "CHECKOUT" && Number(f[3]) > 0)
      this.passes.push({ studentId: f[2], epoch: Number(f[3]) });
    this.passes.sort((a, b) => a.epoch - b.epoch);
    if (this.passes.length > 8) throw new HallzeeError("INVALID_EVENT");
  }
}
