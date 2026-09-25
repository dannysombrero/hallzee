import { describe, expect, it } from "vitest";
import { RoomCoordinator } from "../../../relay/src/RoomCoordinator";
import type { VirtualTerminalMessage } from "../../src/protocol/VirtualTerminalProtocol";
function setup() {
  let time = 1800000000;
  const room = new RoomCoordinator("ROOM-TEST", () => time);
  const connect = () => {
    const messages: VirtualTerminalMessage[] = [];
    const peer = room.connect(msg => messages.push(msg));
    return { peer, messages, send: (msg: VirtualTerminalMessage) => room.receive(peer, msg), state: () => messages.filter(msg => msg.type === "ROOM_STATE").at(-1)!.state };
  };
  const host = connect();
  const claim = { type: "CLAIM_ROOM", roomCode: "ROOM-TEST", hostSecret: "test-owner-only", capacity: 2 } as const;
  host.send(claim);
  const station = connect(); station.send({ type: "JOIN_ROOM", roomCode: "ROOM-TEST" });
  return { room, host, station, connect, claim, advance: (seconds: number) => { time += seconds; room.tick(); } };
}
describe("production room lifecycle", () => {
  it("does not give a rejected claimant host privileges", () => {
    const { connect, claim, station } = setup();
    const outsider = connect(); outsider.send({ ...claim, hostSecret: "different-test-owner" });
    expect(outsider.messages).toContainEqual(expect.objectContaining({ type: "ERROR", code: "ALREADY_CLAIMED" }));
    outsider.send({ type: "CLOSE_ROOM" });
    expect(station.state().status).toBe("open");
  });
  it("pauses on host loss, restores active pass details on resume, and keeps public data private", () => {
    const { room, host, station, connect, claim } = setup();
    host.send({ type: "CHECKOUT_CONFIRM", payload: { requestId: "manual-demo", studentId: "101", name: "Synthetic Example", destination: "Office", purpose: "Private test note", outEpoch: 1800000000, period: "Period 2" } });
    room.disconnect(host.peer);
    expect(station.state().status).toBe("paused");
    expect(station.state().activePasses).toEqual([{ name: "Student", outEpoch: 1800000000 }]);
    const replacement = connect(); replacement.send({ ...claim, resumeOnly: true });
    expect(replacement.state().activePasses[0]).toMatchObject({ studentId: "101", destination: "Office", purpose: "Private test note", period: "Period 2" });
    expect(station.state().status).toBe("open");
  });
  it("confirms check-ins only after the teacher confirms and does not broadcast the student's ID", () => {
    const { host, station, connect } = setup();
    const other = connect(); other.send({ type: "JOIN_ROOM", roomCode: "ROOM-TEST" });
    host.send({ type: "CHECKOUT_CONFIRM", payload: { requestId: "manual-demo", studentId: "101", name: "Student", destination: "Office", outEpoch: 1800000000 } });
    station.send({ type: "CHECKIN_REQUEST", payload: { studentId: "101", requestId: "checkin-demo" } });
    expect(station.state().activeCount).toBe(1);
    expect(station.messages.some(m => m.type === "CHECKIN_CONFIRM")).toBe(false);
    host.send({ type: "CHECKIN_CONFIRM", payload: { studentId: "101", requestId: "checkin-demo", inEpoch: 1800000300 } });
    expect(station.messages).toContainEqual({ type: "CHECKIN_CONFIRM", payload: { studentId: "101", requestId: "checkin-demo", inEpoch: 1800000300 } });
    expect(other.messages.some(m => m.type === "CHECKIN_CONFIRM")).toBe(false);
    expect(station.state().activeCount).toBe(0);
  });
  it("closes the room for students and allows a new owner to start the same code", () => {
    const { host, station, connect, claim } = setup();
    host.send({ type: "CLOSE_ROOM" });
    expect(station.state().status).toBe("closed");
    expect(host.messages.at(-1)).toEqual({ type: "ROOM_CLOSED" });
    const next = connect(); next.send({ ...claim, hostSecret: "next-demo-owner" });
    expect(station.state().status).toBe("open");
  });
  it("expires independently of client traffic and does not silently renew a resumed session", () => {
    const { advance, station, connect, claim } = setup();
    advance(12 * 3600);
    expect(station.state().status).toBe("expired");
    const next = connect(); next.send({ ...claim, resumeOnly: true });
    expect(next.messages).toContainEqual(expect.objectContaining({ type: "ERROR", code: "SESSION_EXPIRED" }));
    next.send(claim);
    expect(station.state().status).toBe("open");
  });
  it("does not end a room while a student is out", () => {
    const { host, station } = setup();
    host.send({ type: "CHECKOUT_CONFIRM", payload: { requestId: "manual-demo", studentId: "101", name: "Student", destination: "Office", outEpoch: 1800000000 } });
    host.send({ type: "CLOSE_ROOM" });
    expect(host.messages.at(-1)).toMatchObject({ type: "ERROR", code: "ACTIVE_PASSES" });
    expect(station.state().activeCount).toBe(1);
  });
});
