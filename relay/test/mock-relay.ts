import http from "node:http";
import crypto from "node:crypto";
import type {
  VirtualTerminalMessage,
  RoomStatePayload,
  PublicActivePass,
  WaitlistItem,
} from "../src/protocol";

interface ActivePassInternal {
  studentId: string;
  name: string;
  destination: string;
  purpose?: string;
  outEpoch: number;
}

interface MockSession {
  socket: any;
  role: "host" | "client";
  roomCode?: string;
}

export class MockRelayServer {
  private server: http.Server | null = null;
  private rooms = new Map<
    string,
    {
      hostSecret: string | null;
      pinHash: string | null;
      capacity: number;
      sessions: Set<MockSession>;
      activePasses: Map<string, ActivePassInternal>;
      waitlist: WaitlistItem[];
      expiresAtEpoch: number;
      queuePaused: boolean;
    }
  >();

  start(port = 4192): Promise<number> {
    return new Promise((resolve) => {
      this.server = http.createServer((req, res) => {
        if (req.url === "/health") {
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ status: "ok" }));
          return;
        }
        res.writeHead(404);
        res.end();
      });

      this.server.on("upgrade", (req, socket) => {
        const key = req.headers["sec-websocket-key"];
        if (!key) {
          socket.destroy();
          return;
        }

        // Handshake
        const digest = crypto
          .createHash("sha1")
          .update(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")
          .digest("base64");

        socket.write(
          "HTTP/1.1 101 Switching Protocols\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            `Sec-WebSocket-Accept: ${digest}\r\n\r\n`,
        );

        const session: MockSession = { socket, role: "client" };
        this.handleConnection(session);
      });

      this.server.listen(port, "127.0.0.1", () => {
        const addr = this.server!.address() as { port: number };
        resolve(addr.port);
      });
    });
  }

  stop(): Promise<void> {
    return new Promise((resolve) => {
      if (!this.server) {
        resolve();
        return;
      }
      for (const room of this.rooms.values()) {
        for (const session of room.sessions) {
          try {
            session.socket.destroy();
          } catch {
            // Ignore
          }
        }
        room.sessions.clear();
      }
      (this.server as any).closeAllConnections?.();
      this.server.close(() => resolve());
    });
  }

  private handleConnection(session: MockSession) {
    let buffer = Buffer.alloc(0);

    session.socket.on("data", (chunk: Buffer) => {
      buffer = Buffer.concat([buffer, chunk]);
      while (buffer.length >= 2) {
        const byte1 = buffer[0];
        const byte2 = buffer[1];
        const opcode = byte1 & 0x0f;
        const isMasked = (byte2 & 0x80) !== 0;
        let payloadLen = byte2 & 0x7f;
        let offset = 2;

        if (payloadLen === 126) {
          if (buffer.length < 4) return;
          payloadLen = buffer.readUInt16BE(2);
          offset = 4;
        } else if (payloadLen === 127) {
          if (buffer.length < 10) return;
          payloadLen = Number(buffer.readBigUInt64BE(2));
          offset = 10;
        }

        const maskKeyLen = isMasked ? 4 : 0;
        if (buffer.length < offset + maskKeyLen + payloadLen) return;

        let payload = buffer.subarray(offset + maskKeyLen, offset + maskKeyLen + payloadLen);
        if (isMasked) {
          const mask = buffer.subarray(offset, offset + 4);
          const unmasked = Buffer.alloc(payloadLen);
          for (let i = 0; i < payloadLen; i++) {
            unmasked[i] = payload[i] ^ mask[i % 4];
          }
          payload = unmasked;
        }

        buffer = buffer.subarray(offset + maskKeyLen + payloadLen);

        if (opcode === 0x08) {
          // Close frame
          session.socket.end();
          return;
        } else if (opcode === 0x09) {
          // Ping frame -> send Pong
          this.sendFrame(session.socket, 0x0a, Buffer.alloc(0));
        } else if (opcode === 0x01) {
          // Text frame
          try {
            const str = payload.toString("utf-8");
            const msg = JSON.parse(str) as VirtualTerminalMessage;
            this.handleMessage(session, msg);
          } catch {
            // Ignore parse errors
          }
        }
      }
    });

    session.socket.on("close", () => {
      if (session.roomCode) {
        const room = this.rooms.get(session.roomCode);
        if (room) {
          room.sessions.delete(session);
        }
      }
    });
  }

  private handleMessage(session: MockSession, msg: VirtualTerminalMessage) {
    const now = Math.floor(Date.now() / 1000);

    if (msg.type === "CLAIM_ROOM") {
      const code = msg.roomCode.toUpperCase();
      session.roomCode = code;
      session.role = "host";

      let room = this.rooms.get(code);
      if (!room) {
        room = {
          hostSecret: msg.hostSecret,
          pinHash: msg.pinHash || null,
          capacity: Math.max(1, msg.capacity || 1),
          sessions: new Set(),
          activePasses: new Map(),
          waitlist: [],
          expiresAtEpoch: now + 12 * 3600,
          queuePaused: false,
        };
        this.rooms.set(code, room);
      } else {
        if (room.hostSecret && room.hostSecret !== msg.hostSecret) {
          if (msg.pinHash && room.pinHash && msg.pinHash === room.pinHash) {
            room.hostSecret = msg.hostSecret;
          } else {
            this.send(session, {
              type: "ERROR",
              code: "ALREADY_CLAIMED",
              message: "Room claimed with another secret",
            });
            return;
          }
        }
        room.capacity = Math.max(1, msg.capacity || 1);
      }

      room.sessions.add(session);
      this.send(session, {
        type: "CLAIM_OK",
        roomCode: code,
        sessionExpiresAtEpoch: room.expiresAtEpoch,
      });
      this.broadcastState(code);
      return;
    }

    if (msg.type === "JOIN_ROOM") {
      const code = msg.roomCode.toUpperCase();
      session.roomCode = code;
      const room = this.rooms.get(code);
      if (room) {
        room.sessions.add(session);
        this.send(session, { type: "ROOM_STATE", state: this.buildRoomState(code, false) });
      } else {
        this.send(session, {
          type: "ROOM_STATE",
          state: {
            roomCode: code,
            activeCount: 0,
            capacity: 1,
            isFull: false,
            waitlistCount: 0,
            activePasses: [],
            waitlist: [],
            sessionExpiresAtEpoch: 0,
          },
        });
      }
      return;
    }

    const room = session.roomCode ? this.rooms.get(session.roomCode) : null;
    if (!room) return;

    if (msg.type === "CHECKOUT_REQUEST") {
      if (room.activePasses.has(msg.payload.studentId)) {
        this.send(session, {
          type: "CHECKOUT_REJECT",
          payload: {
            requestId: msg.payload.requestId,
            reason: "ALREADY_OUT",
            message: "Student is currently checked out",
          },
        });
        return;
      }
      if (room.activePasses.size >= room.capacity) {
        this.send(session, {
          type: "CHECKOUT_REJECT",
          payload: {
            requestId: msg.payload.requestId,
            reason: "CAPACITY_REACHED",
            message: "All passes are currently in use",
          },
        });
        return;
      }

      // Forward to host
      for (const s of room.sessions) {
        if (s.role === "host") this.send(s, msg);
      }
    } else if (msg.type === "CHECKOUT_CONFIRM") {
      if (session.role !== "host") return;
      room.activePasses.set(msg.payload.studentId, {
        studentId: msg.payload.studentId,
        name: msg.payload.name,
        destination: msg.payload.destination,
        purpose: msg.payload.purpose,
        outEpoch: msg.payload.outEpoch,
      });
      room.waitlist = room.waitlist.filter((w) => w.studentId !== msg.payload.studentId);
      this.recalculateWaitlist(session.roomCode!);
      this.broadcastState(session.roomCode!);
    } else if (msg.type === "CHECKOUT_REJECT") {
      if (session.role !== "host") return;
      for (const s of room.sessions) this.send(s, msg);
    } else if (msg.type === "CHECKIN_REQUEST") {
      if (room.activePasses.has(msg.payload.studentId)) {
        room.activePasses.delete(msg.payload.studentId);
        for (const s of room.sessions) {
          this.send(s, {
            type: "CHECKIN_CONFIRM",
            payload: { studentId: msg.payload.studentId, inEpoch: now },
          });
        }
        this.broadcastState(session.roomCode!);
      }
    } else if (msg.type === "CHECKIN_CONFIRM") {
      if (session.role !== "host") return;
      if (room.activePasses.has(msg.payload.studentId)) {
        room.activePasses.delete(msg.payload.studentId);
        for (const s of room.sessions) {
          this.send(s, msg);
        }
        this.broadcastState(session.roomCode!);
      }
    } else if (msg.type === "WAITLIST_JOIN") {
      if (!room.queuePaused && !room.waitlist.some((w) => w.studentId === msg.payload.studentId)) {
        room.waitlist.push({
          studentId: msg.payload.studentId,
          name: msg.payload.studentId,
          position: room.waitlist.length + 1,
          joinedEpoch: now,
        });
        this.broadcastState(session.roomCode!);
      }
    } else if (msg.type === "WAITLIST_ACTION") {
      if (session.role !== "host") return;
      const { studentId, action } = msg.payload;
      if (action === "dismiss") {
        room.waitlist = room.waitlist.filter((w) => w.studentId !== studentId);
      } else if (action === "bump") {
        const idx = room.waitlist.findIndex((w) => w.studentId === studentId);
        if (idx > 0) {
          const [item] = room.waitlist.splice(idx, 1);
          room.waitlist.unshift(item);
        }
      } else if (action === "pause") {
        room.queuePaused = true;
      } else if (action === "resume") {
        room.queuePaused = false;
      }
      this.recalculateWaitlist(session.roomCode!);
      this.broadcastState(session.roomCode!);
    } else if (msg.type === "HEARTBEAT") {
      this.send(session, { type: "HEARTBEAT", timestamp: now });
    }
  }

  private recalculateWaitlist(roomCode: string) {
    const room = this.rooms.get(roomCode);
    if (!room) return;
    room.waitlist.forEach((item, idx) => {
      item.position = idx + 1;
    });
  }

  private buildRoomState(roomCode: string, isHost: boolean): RoomStatePayload {
    const room = this.rooms.get(roomCode);
    if (!room) {
      return {
        roomCode,
        activeCount: 0,
        capacity: 1,
        isFull: false,
        waitlistCount: 0,
        activePasses: [],
        waitlist: [],
        sessionExpiresAtEpoch: 0,
      };
    }

    const activePasses: PublicActivePass[] = Array.from(room.activePasses.values()).map((p) => {
      if (isHost) {
        return {
          studentId: p.studentId,
          name: p.name,
          outEpoch: p.outEpoch,
        };
      }
      return {
        name: p.name,
        outEpoch: p.outEpoch,
      };
    });

    return {
      roomCode,
      activeCount: room.activePasses.size,
      capacity: room.capacity,
      isFull: room.activePasses.size >= room.capacity,
      waitlistCount: room.waitlist.length,
      activePasses,
      waitlist: room.waitlist,
      sessionExpiresAtEpoch: room.expiresAtEpoch,
    };
  }

  private broadcastState(roomCode: string) {
    const room = this.rooms.get(roomCode);
    if (!room) return;
    for (const session of room.sessions) {
      const isHost = session.role === "host";
      this.send(session, {
        type: "ROOM_STATE",
        state: this.buildRoomState(roomCode, isHost),
      });
    }
  }

  private send(session: MockSession, msg: VirtualTerminalMessage) {
    const data = Buffer.from(JSON.stringify(msg), "utf-8");
    this.sendFrame(session.socket, 0x01, data);
  }

  private sendFrame(socket: any, opcode: number, payload: Buffer) {
    try {
      const len = payload.length;
      let header: Buffer;
      if (len <= 125) {
        header = Buffer.from([0x80 | opcode, len]);
      } else if (len <= 65535) {
        header = Buffer.alloc(4);
        header[0] = 0x80 | opcode;
        header[1] = 126;
        header.writeUInt16BE(len, 2);
      } else {
        header = Buffer.alloc(10);
        header[0] = 0x80 | opcode;
        header[1] = 127;
        header.writeBigUInt64BE(BigInt(len), 2);
      }
      socket.write(Buffer.concat([header, payload]));
    } catch {
      // Socket closed
    }
  }
}

export async function startMockRelay(port = 4192) {
  const server = new MockRelayServer();
  const actualPort = await server.start(port);
  return {
    port: actualPort,
    close: () => server.stop(),
  };
}
