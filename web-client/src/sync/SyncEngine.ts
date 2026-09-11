import { WebTerminalSession } from "../protocol/WebTerminalSession";
import { TripRepository, type TripContext } from "../storage/TripRepository";
import { parseTrip, protocolError } from "../protocol/messages";
import { abortCheck, HallzeeError } from "../app/errors";
import { localDate, localTime } from "./TerminalClock";
import type { WireTrip } from "../storage/schema";
export class SyncEngine {
  private closed = false;
  private input: Promise<void> = Promise.resolve();
  private bytes = 0;
  private run?: {
    terminalId: string;
    cursor: number;
    count?: number;
    seen: Set<number>;
    timeAck: boolean;
    recovery: boolean;
    context: (trip: WireTrip) => TripContext;
    resolve: () => void;
    reject: (error: unknown) => void;
    signal?: AbortSignal;
    timer?: ReturnType<typeof setTimeout>;
  };
  constructor(
    private session: WebTerminalSession,
    private trips: TripRepository,
    private liveContext: (trip: WireTrip) => TripContext,
    private changed: () => void,
    private failed: (error: unknown) => void,
  ) {}
  receive(line: string) {
    if (this.closed) return;
    if (!/^(TIME_ACK|SYNC_BEGIN|TRIP|LIVE_TRIP|SYNC_END|ACK_ERROR|ERROR)(,|$)/.test(line)) return;
    this.bytes += new TextEncoder().encode(line).length;
    if (this.bytes > 65536) {
      const error = new HallzeeError("INPUT_OVERFLOW");
      this.closed = true;
      this.cancel(error);
      this.failed(error);
      return;
    }
    const size = new TextEncoder().encode(line).length;
    this.input = this.input
      .then(() => {
        if (!this.closed) return this.process(line);
      })
      .catch((error) => {
        this.cancel(error);
        this.failed(error);
      })
      .finally(() => {
        this.bytes -= size;
      });
  }
  private touch() {
    if (!this.run) return;
    clearTimeout(this.run.timer);
    this.run.timer = setTimeout(
      () =>
        this.cancel(
          new HallzeeError(
            "SYNC_TIMEOUT",
            "Sync stopped before completion. Retry to recover any missing trips.",
            true,
          ),
        ),
      10000,
    );
  }
  async synchronize(signal?: AbortSignal) {
    if (this.run) throw new HallzeeError("OPERATION_BUSY");
    const id = this.session.authenticatedId;
    if (!id) throw new HallzeeError("AUTH_REQUIRED");
    const state = await this.trips.cursor(id);
    abortCheck(signal);
    const result = new Promise<void>((resolve, reject) => {
      this.run = {
        terminalId: id,
        cursor: state.recoveryRequired ? 0 : state.completedCursor,
        seen: new Set(),
        timeAck: false,
        recovery: state.recoveryRequired,
        context: this.liveContext,
        resolve,
        reject,
        signal,
      };
    });
    void result.catch(() => {});
    const abort = () => this.cancel(new HallzeeError("CANCELLED"));
    signal?.addEventListener("abort", abort, { once: true });
    this.touch();
    try {
      await this.session.send(
        `TIME_CURSOR,${localDate()},${localTime()},${this.run!.cursor}`,
        signal,
      );
      await result;
    } catch (error) {
      this.cancel(error);
      throw error;
    } finally {
      signal?.removeEventListener("abort", abort);
    }
  }
  private async process(line: string) {
    const run = this.run;
    if (line.startsWith("LIVE_TRIP,")) {
      const trip = parseTrip(line.slice(10));
      const result = await this.trips.store(trip, this.liveContext(trip));
      if (result === "conflict")
        throw new HallzeeError(
          "TRIP_CONFLICT",
          "This trip ID already has different records. Export and review terminal history before recovery.",
        );
      this.changed();
      return;
    }
    if (!run) return;
    abortCheck(run.signal);
    if (line.startsWith("ERROR,") || line.startsWith("ACK_ERROR,")) throw protocolError(line);
    if (line === "TIME_ACK,OK" && !run.timeAck && run.count === undefined) {
      run.timeAck = true;
      this.touch();
      return;
    }
    if (/^SYNC_BEGIN,\d+$/.test(line) && run.timeAck && run.count === undefined) {
      run.count = Number(line.split(",")[1]);
      this.touch();
      return;
    }
    if (line.startsWith("TRIP,") && run.count !== undefined) {
      const trip = parseTrip(line.slice(5));
      if (trip.tripId < run.cursor || (trip.tripId === run.cursor && !run.seen.has(trip.tripId)))
        throw new HallzeeError("OUT_OF_ORDER_TRIP");
      const result = await this.trips.store(trip, run.context(trip));
      if (result === "conflict")
        throw new HallzeeError(
          "TRIP_CONFLICT",
          "Conflicting terminal history. Export your records before resetting a known factory-reset history.",
        );
      abortCheck(run.signal);
      if (this.run !== run) return;
      await this.session.send(`ACK,${trip.tripId}`, run.signal);
      run.cursor = Math.max(run.cursor, trip.tripId);
      run.seen.add(trip.tripId);
      this.touch();
      this.changed();
      return;
    }
    if (line === "SYNC_END" && run.count !== undefined && run.seen.size >= run.count) {
      await this.trips.completeSync(run.terminalId, run.cursor, run.recovery);
      if (this.run !== run) return;
      clearTimeout(run.timer);
      this.run = undefined;
      run.resolve();
      this.changed();
      return;
    }
    throw new HallzeeError(
      "INCOMPLETE_SYNC",
      "The terminal stream was incomplete. Retry to recover the missing records.",
    );
  }
  cancel(error: unknown = new HallzeeError("CANCELLED")) {
    if (this.run) {
      clearTimeout(this.run.timer);
      this.run.reject(error);
      this.run = undefined;
    }
  }
  async drain() {
    await this.input;
  }
  dispose() {
    this.closed = true;
    this.cancel();
  }
}
