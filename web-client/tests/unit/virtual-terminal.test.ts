import { describe, it, expect, beforeAll, afterAll } from "vitest";
import { MockRelayServer } from "../../../relay/test/mock-relay";
import { VirtualTerminalTransport } from "../../src/transport/VirtualTerminalTransport";
import type { RoomStatePayload, CheckoutRequestPayload } from "../../src/protocol/VirtualTerminalProtocol";

describe("Virtual Terminal Protocol & Relay Coordination", () => {
  let relay: MockRelayServer;
  let relayPort: number;
  let relayUrl: string;

  beforeAll(async () => {
    relay = new MockRelayServer();
    // Port 0 picks an available random port
    relayPort = await relay.start(0);
    relayUrl = `ws://127.0.0.1:${relayPort}/ws`;
  });

  afterAll(async () => {
    await relay.stop();
  });

  it("coordinates host claim and station join with privacy masking", async () => {
    const roomCode = "TEST01";
    let hostState: RoomStatePayload | null = null;
    let stationState: RoomStatePayload | null = null;
    let checkoutReqReceived: CheckoutRequestPayload | null = null;

    // 1. Connect Host
    const host = new VirtualTerminalTransport({
      relayUrl,
      roomCode,
      role: "host",
      hostSecret: "secret-123",
      capacity: 1,
      onRoomState: (s) => {
        hostState = s;
      },
      onCheckoutRequest: (req) => {
        checkoutReqReceived = req;
        // Host confirms checkout
        host.confirmCheckout({
          requestId: req.requestId,
          studentId: req.studentId,
          name: "Ada Lovelace",
          destination: req.destination,
          purpose: req.purpose,
          outEpoch: 1700000000,
        });
      },
    });
    host.connect();

    // Wait for host connection and claim OK
    await new Promise((resolve) => setTimeout(resolve, 150));
    expect(hostState).toBeDefined();
    expect(hostState!.roomCode).toBe("TEST01");
    expect(hostState!.capacity).toBe(1);
    expect(hostState!.activeCount).toBe(0);

    // 2. Connect Station
    const station = new VirtualTerminalTransport({
      relayUrl,
      roomCode,
      role: "station",
      onRoomState: (s) => {
        stationState = s;
      },
    });
    station.connect();

    await new Promise((resolve) => setTimeout(resolve, 150));
    expect(stationState).toBeDefined();
    expect(stationState!.roomCode).toBe("TEST01");

    // 3. Station requests checkout for Nurse with private purpose
    station.requestCheckout("101", "Nurse", "Headache medication");

    // Wait for host to receive and confirm
    await new Promise((resolve) => setTimeout(resolve, 200));

    // Verify host received full private details
    expect(checkoutReqReceived).toBeDefined();
    expect(checkoutReqReceived!.studentId).toBe("101");
    expect(checkoutReqReceived!.destination).toBe("Nurse");
    expect(checkoutReqReceived!.purpose).toBe("Headache medication");

    // Verify station received updated RoomState
    expect(stationState!.activeCount).toBe(1);
    expect(stationState!.isFull).toBe(true);
    expect(stationState!.activePasses.length).toBe(1);

    const publicPass = stationState!.activePasses[0];
    expect(publicPass.name).toBe("Student");
    // CRITICAL PRIVACY MASKING: Public station must NEVER receive studentId, destination, or purpose!
    expect(publicPass.studentId).toBeUndefined();
    expect((publicPass as any).destination).toBeUndefined();
    expect((publicPass as any).purpose).toBeUndefined();

    // Host sees the studentId
    const hostPass = hostState!.activePasses[0];
    expect(hostPass.studentId).toBe("101");

    // 4. Clean up transports
    station.disconnect();
    host.disconnect();
  });

  it("enforces capacity limit and rejects excess checkout", async () => {
    const roomCode = "TEST02";
    let rejectionReceived: any = null;

    const host = new VirtualTerminalTransport({
      relayUrl,
      roomCode,
      role: "host",
      hostSecret: "secret-abc",
      capacity: 1,
    });
    host.connect();
    await new Promise((resolve) => setTimeout(resolve, 150));

    // First checkout
    host.confirmCheckout({
      requestId: "init",
      studentId: "201",
      name: "Grace Hopper",
      destination: "Restroom",
      outEpoch: 1700000000,
    });
    await new Promise((resolve) => setTimeout(resolve, 100));

    const station = new VirtualTerminalTransport({
      relayUrl,
      roomCode,
      role: "station",
      onCheckoutReject: (rej) => {
        rejectionReceived = rej;
      },
    });
    station.connect();
    await new Promise((resolve) => setTimeout(resolve, 150));

    // Second checkout attempt when room is full (capacity=1)
    station.requestCheckout("202", "Restroom");
    await new Promise((resolve) => setTimeout(resolve, 200));

    expect(rejectionReceived).not.toBeNull();
    expect(rejectionReceived.reason).toBe("CAPACITY_REACHED");

    station.disconnect();
    host.disconnect();
  });

  it("handles waitlist queue and actions", async () => {
    const roomCode = "TEST03";
    let latestStationState: RoomStatePayload | null = null;

    const host = new VirtualTerminalTransport({
      relayUrl,
      roomCode,
      role: "host",
      hostSecret: "secret-xyz",
      capacity: 1,
    });
    host.connect();
    await new Promise((resolve) => setTimeout(resolve, 150));

    // Occupy room
    host.confirmCheckout({
      requestId: "r1",
      studentId: "301",
      name: "Alan Turing",
      destination: "Restroom",
      outEpoch: 1700000000,
    });
    await new Promise((resolve) => setTimeout(resolve, 100));

    const station = new VirtualTerminalTransport({
      relayUrl,
      roomCode,
      role: "station",
      onRoomState: (s) => {
        latestStationState = s;
      },
    });
    station.connect();
    await new Promise((resolve) => setTimeout(resolve, 150));

    // Join waitlist
    station.joinWaitlist("302");
    await new Promise((resolve) => setTimeout(resolve, 150));

    expect(latestStationState!.waitlistCount).toBe(1);
    expect(latestStationState!.waitlist).toEqual([]);

    // Host dismisses student from waitlist
    host.performWaitlistAction("302", "dismiss");
    await new Promise((resolve) => setTimeout(resolve, 150));

    expect(latestStationState!.waitlistCount).toBe(0);

    station.disconnect();
    host.disconnect();
  });

  it("handles checkin request and frees pass capacity", async () => {
    const roomCode = "TEST04";
    let checkinStudentReceived: string | null = null;
    let stationState: RoomStatePayload | null = null;

    const host = new VirtualTerminalTransport({
      relayUrl,
      roomCode,
      role: "host",
      hostSecret: "secret-checkin",
      capacity: 1,
      onCheckinRequest: (request) => {
        checkinStudentReceived = request.studentId;
        host.confirmCheckin(request.studentId, undefined, request.requestId);
      },
    });
    host.connect();
    await new Promise((resolve) => setTimeout(resolve, 150));

    // Occupy pass
    host.confirmCheckout({
      requestId: "req-c",
      studentId: "401",
      name: "Katherine Johnson",
      destination: "Restroom",
      outEpoch: 1700000000,
    });
    await new Promise((resolve) => setTimeout(resolve, 100));

    const station = new VirtualTerminalTransport({
      relayUrl,
      roomCode,
      role: "station",
      onRoomState: (s) => {
        stationState = s;
      },
    });
    station.connect();
    await new Promise((resolve) => setTimeout(resolve, 150));

    expect(stationState!.activeCount).toBe(1);

    // Request check-in from station
    station.requestCheckin("401");
    await new Promise((resolve) => setTimeout(resolve, 200));

    expect(checkinStudentReceived).toBe("401");
    expect(stationState!.activeCount).toBe(0);
    expect(stationState!.isFull).toBe(false);

    station.disconnect();
    host.disconnect();
  });
});
