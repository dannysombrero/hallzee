import type {
  VirtualTerminalMessage,
  RoomStatePayload,
  PublicActivePass,
  WaitlistItem,
} from "./protocol";

interface ActivePassInternal {
  studentId: string;
  name: string;
  destination: string;
  purpose?: string;
  outEpoch: number;
}

interface WsSession {
  ws: WebSocket;
  role: "host" | "client";
}

export class RoomDurableObject {
  private roomCode: string = "";
  private hostSecret: string | null = null;
  private pinHash: string | null = null;
  private capacity: number = 1;
  private sessions: Set<WsSession> = new Set();
  private activePasses: Map<string, ActivePassInternal> = new Map();
  private waitlist: WaitlistItem[] = [];
  private expiresAtEpoch: number = 0;
  private queuePaused: boolean = false;

  constructor(private state: DurableObjectState, private env: unknown) {}

  async fetch(request: Request): Promise<Response> {
    const url = new URL(request.url);
    if (url.pathname === "/ws") {
      const upgradeHeader = request.headers.get("Upgrade");
      if (!upgradeHeader || upgradeHeader.toLowerCase() !== "websocket") {
        return new Response("Expected Upgrade: websocket", { status: 426 });
      }

      // Pair of WebSockets: one for client, one for server
      const pair = new (globalThis as any).WebSocketPair();
      const clientWs = pair[0];
      const serverWs = pair[1];

      this.handleWebSocket(serverWs);

      return new Response(null, {
        status: 101,
        webSocket: clientWs,
      });
    }

    return new Response("Not found", { status: 404 });
  }

  private handleWebSocket(ws: WebSocket) {
    (ws as any).accept();
    const session: WsSession = { ws, role: "client" };
    this.sessions.add(session);

    ws.addEventListener("message", (event) => {
      try {
        const raw = typeof event.data === "string" ? event.data : new TextDecoder().decode(event.data as ArrayBuffer);
        const msg = JSON.parse(raw) as VirtualTerminalMessage;
        this.processMessage(session, msg);
      } catch (err) {
        this.send(ws, { type: "ERROR", code: "BAD_MESSAGE", message: "Invalid JSON payload" });
      }
    });

    ws.addEventListener("close", () => {
      this.sessions.delete(session);
    });

    ws.addEventListener("error", () => {
      this.sessions.delete(session);
    });
  }

  private processMessage(session: WsSession, msg: VirtualTerminalMessage) {
    const now = Math.floor(Date.now() / 1000);

    // Check expiration (12 hours)
    if (this.expiresAtEpoch > 0 && now > this.expiresAtEpoch) {
      this.send(session.ws, { type: "ERROR", code: "SESSION_EXPIRED", message: "12-hour session expired" });
      this.activePasses.clear();
      this.waitlist = [];
      this.broadcastState();
      return;
    }

    switch (msg.type) {
      case "CLAIM_ROOM": {
        // Teacher is claiming/starting the room
        if (this.hostSecret && this.hostSecret !== msg.hostSecret) {
          // Room claimed by someone else; check PIN if provided
          if (msg.pinHash && this.pinHash && msg.pinHash === this.pinHash) {
            // Correct PIN, allow reclaiming
            this.hostSecret = msg.hostSecret;
          } else {
            this.send(session.ws, {
              type: "ERROR",
              code: "ALREADY_CLAIMED",
              message: "Room is claimed with a different key or PIN",
            });
            return;
          }
        }

        this.roomCode = msg.roomCode.toUpperCase();
        this.hostSecret = msg.hostSecret;
        if (msg.pinHash) this.pinHash = msg.pinHash;
        this.capacity = Math.max(1, msg.capacity || 1);
        this.expiresAtEpoch = now + 12 * 3600; // 12 hours TTL
        session.role = "host";

        this.send(session.ws, {
          type: "CLAIM_OK",
          roomCode: this.roomCode,
          sessionExpiresAtEpoch: this.expiresAtEpoch,
        });

        this.broadcastState();
        break;
      }

      case "JOIN_ROOM": {
        this.roomCode = msg.roomCode.toUpperCase();
        this.sendState(session);
        break;
      }

      case "CHECKOUT_REQUEST": {
        // Student/Station requested a checkout
        if (!this.hostSecret) {
          this.send(session.ws, { type: "ERROR", code: "ROOM_INACTIVE", message: "Room is not active" });
          return;
        }

        // Check if student is already out
        if (this.activePasses.has(msg.payload.studentId)) {
          this.send(session.ws, {
            type: "CHECKOUT_REJECT",
            payload: {
              requestId: msg.payload.requestId,
              reason: "ALREADY_OUT",
              message: "Student is currently checked out",
            },
          });
          return;
        }

        // Check capacity
        if (this.activePasses.size >= this.capacity) {
          this.send(session.ws, {
            type: "CHECKOUT_REJECT",
            payload: {
              requestId: msg.payload.requestId,
              reason: "CAPACITY_REACHED",
              message: "All passes are currently in use",
            },
          });
          return;
        }

        // Forward request to host for confirmation / roster validation
        this.sendToHost(msg);
        break;
      }

      case "CHECKOUT_CONFIRM": {
        // Only host can confirm checkout
        if (session.role !== "host") return;

        this.activePasses.set(msg.payload.studentId, {
          studentId: msg.payload.studentId,
          name: msg.payload.name,
          destination: msg.payload.destination,
          purpose: msg.payload.purpose,
          outEpoch: msg.payload.outEpoch,
        });

        // Remove from waitlist if present
        this.waitlist = this.waitlist.filter((w) => w.studentId !== msg.payload.studentId);
        this.recalculateWaitlistPositions();

        // Broadcast updated state to all clients
        this.broadcastState();
        break;
      }

      case "CHECKOUT_REJECT": {
        // Host rejected the pass
        if (session.role !== "host") return;
        this.broadcast(msg);
        break;
      }

      case "CHECKIN_REQUEST": {
        // Can come from client ("I'm Back") or host
        if (this.activePasses.has(msg.payload.studentId)) {
          this.activePasses.delete(msg.payload.studentId);

          const inEpoch = now;
          this.broadcast({
            type: "CHECKIN_CONFIRM",
            payload: { studentId: msg.payload.studentId, inEpoch },
          });

          this.broadcastState();
        }
        break;
      }

      case "CHECKIN_CONFIRM": {
        // Initiated by teacher/host
        if (session.role !== "host") return;
        if (this.activePasses.has(msg.payload.studentId)) {
          this.activePasses.delete(msg.payload.studentId);
          this.broadcast(msg);
          this.broadcastState();
        }
        break;
      }

      case "WAITLIST_JOIN": {
        if (!this.queuePaused && !this.waitlist.some((w) => w.studentId === msg.payload.studentId)) {
          this.waitlist.push({
            studentId: msg.payload.studentId,
            name: msg.payload.studentId, // Placeholder until host resolves or updates
            position: this.waitlist.length + 1,
            joinedEpoch: now,
          });
          this.broadcastState();
        }
        break;
      }

      case "WAITLIST_ACTION": {
        if (session.role !== "host") return;
        const { studentId, action } = msg.payload;
        if (action === "dismiss") {
          this.waitlist = this.waitlist.filter((w) => w.studentId !== studentId);
        } else if (action === "bump") {
          const idx = this.waitlist.findIndex((w) => w.studentId === studentId);
          if (idx > 0) {
            const [item] = this.waitlist.splice(idx, 1);
            this.waitlist.unshift(item);
          }
        } else if (action === "pause") {
          this.queuePaused = true;
        } else if (action === "resume") {
          this.queuePaused = false;
        }
        this.recalculateWaitlistPositions();
        this.broadcastState();
        break;
      }

      case "HEARTBEAT": {
        this.send(session.ws, { type: "HEARTBEAT", timestamp: now });
        break;
      }
    }
  }

  private recalculateWaitlistPositions() {
    this.waitlist.forEach((item, idx) => {
      item.position = idx + 1;
    });
  }

  private buildRoomState(isHost: boolean): RoomStatePayload {
    const activePasses: PublicActivePass[] = Array.from(this.activePasses.values()).map((p) => {
      if (isHost) {
        return {
          studentId: p.studentId,
          name: p.name,
          outEpoch: p.outEpoch,
        };
      }
      // Public view: anonymized name, NO destination/purpose/flags
      return {
        name: p.name,
        outEpoch: p.outEpoch,
      };
    });

    return {
      roomCode: this.roomCode,
      activeCount: this.activePasses.size,
      capacity: this.capacity,
      isFull: this.activePasses.size >= this.capacity,
      waitlistCount: this.waitlist.length,
      activePasses,
      waitlist: this.waitlist,
      sessionExpiresAtEpoch: this.expiresAtEpoch,
    };
  }

  private sendState(session: WsSession) {
    const isHost = session.role === "host";
    const state = this.buildRoomState(isHost);
    this.send(session.ws, { type: "ROOM_STATE", state });
  }

  private broadcastState() {
    for (const session of this.sessions) {
      this.sendState(session);
    }
  }

  private sendToHost(msg: VirtualTerminalMessage) {
    for (const session of this.sessions) {
      if (session.role === "host") {
        this.send(session.ws, msg);
      }
    }
  }

  private broadcast(msg: VirtualTerminalMessage) {
    for (const session of this.sessions) {
      this.send(session.ws, msg);
    }
  }

  private send(ws: WebSocket, msg: VirtualTerminalMessage) {
    try {
      if ((ws as any).readyState === 1 /* OPEN */) {
        ws.send(JSON.stringify(msg));
      }
    } catch {
      // Ignore broken socket
    }
  }
}
