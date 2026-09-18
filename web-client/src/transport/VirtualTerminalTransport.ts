import type {
  VirtualTerminalMessage,
  RoomStatePayload,
  CheckoutRequestPayload,
  CheckoutConfirmPayload,
  CheckoutRejectPayload,
} from "../protocol/VirtualTerminalProtocol";

export type TransportStatus = "disconnected" | "connecting" | "connected" | "error";

export interface VirtualTerminalTransportOptions {
  relayUrl?: string;
  roomCode: string;
  role: "host" | "station";
  hostSecret?: string;
  pinHash?: string;
  capacity?: number;
  onStatusChange?: (status: TransportStatus) => void;
  onRoomState?: (state: RoomStatePayload) => void;
  onCheckoutRequest?: (request: CheckoutRequestPayload) => void;
  onCheckoutReject?: (rejection: CheckoutRejectPayload) => void;
  onCheckinRequest?: (studentId: string) => void;
  onCheckinConfirm?: (studentId: string, inEpoch: number) => void;
  onError?: (code: string, message: string) => void;
}

export function defaultRelayUrl(): string {
  if (typeof window !== "undefined") {
    // In local dev preview or test, use localhost:4192 if on localhost
    if (window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1") {
      return "ws://127.0.0.1:4192/ws";
    }
  }
  return "wss://relay.hallzee.com/ws";
}

export class VirtualTerminalTransport {
  private ws: WebSocket | null = null;
  private status: TransportStatus = "disconnected";
  private shouldReconnect: boolean = true;
  private reconnectAttempts: number = 0;
  private reconnectTimer: any = null;
  private heartbeatTimer: any = null;

  constructor(private options: VirtualTerminalTransportOptions) {}

  connect() {
    this.shouldReconnect = true;
    this.clearTimers();
    this.setStatus("connecting");

    const base = this.options.relayUrl || defaultRelayUrl();
    const url = `${base}?room=${encodeURIComponent(this.options.roomCode.toUpperCase())}${
      this.options.role === "host" ? "&create=1" : ""
    }`;

    try {
      this.ws = new WebSocket(url);
    } catch {
      this.setStatus("error");
      this.scheduleReconnect();
      return;
    }

    this.ws.onopen = () => {
      this.reconnectAttempts = 0;
      this.setStatus("connected");
      this.startHeartbeat();

      if (this.options.role === "host") {
        this.send({
          type: "CLAIM_ROOM",
          roomCode: this.options.roomCode.toUpperCase(),
          hostSecret: this.options.hostSecret || "default-secret",
          pinHash: this.options.pinHash,
          capacity: this.options.capacity || 1,
        });
      } else {
        this.send({
          type: "JOIN_ROOM",
          roomCode: this.options.roomCode.toUpperCase(),
        });
      }
    };

    this.ws.onmessage = (event) => {
      try {
        const msg = JSON.parse(event.data) as VirtualTerminalMessage;
        this.handleMessage(msg);
      } catch {
        // Ignore unparseable frames
      }
    };

    this.ws.onclose = () => {
      this.clearTimers();
      if (this.status !== "disconnected") {
        this.setStatus("disconnected");
      }
      if (this.shouldReconnect) {
        this.scheduleReconnect();
      }
    };

    this.ws.onerror = () => {
      this.setStatus("error");
    };
  }

  disconnect() {
    this.shouldReconnect = false;
    this.clearTimers();
    if (this.ws) {
      try {
        this.ws.close();
      } catch {
        // Ignore
      }
      this.ws = null;
    }
    this.setStatus("disconnected");
  }

  // --- Host Actions ---

  confirmCheckout(payload: CheckoutConfirmPayload) {
    this.send({ type: "CHECKOUT_CONFIRM", payload });
  }

  rejectCheckout(payload: CheckoutRejectPayload) {
    this.send({ type: "CHECKOUT_REJECT", payload });
  }

  confirmCheckin(studentId: string, inEpoch = Math.floor(Date.now() / 1000)) {
    this.send({ type: "CHECKIN_CONFIRM", payload: { studentId, inEpoch } });
  }

  performWaitlistAction(studentId: string, action: "bump" | "dismiss" | "pause" | "resume") {
    this.send({ type: "WAITLIST_ACTION", payload: { studentId, action } });
  }

  // --- Station Actions ---

  requestCheckout(studentId: string, destination: string, purpose?: string) {
    const requestId = `req_${Date.now()}_${Math.random().toString(36).substring(2, 7)}`;
    this.send({
      type: "CHECKOUT_REQUEST",
      payload: { requestId, studentId, destination, purpose },
    });
  }

  requestCheckin(studentId: string) {
    this.send({
      type: "CHECKIN_REQUEST",
      payload: { studentId },
    });
  }

  joinWaitlist(studentId: string) {
    this.send({
      type: "WAITLIST_JOIN",
      payload: { studentId },
    });
  }

  // --- Internals ---

  private handleMessage(msg: VirtualTerminalMessage) {
    switch (msg.type) {
      case "ROOM_STATE":
        this.options.onRoomState?.(msg.state);
        break;
      case "CHECKOUT_REQUEST":
        this.options.onCheckoutRequest?.(msg.payload);
        break;
      case "CHECKOUT_REJECT":
        this.options.onCheckoutReject?.(msg.payload);
        break;
      case "CHECKIN_REQUEST":
        this.options.onCheckinRequest?.(msg.payload.studentId);
        break;
      case "CHECKIN_CONFIRM":
        this.options.onCheckinConfirm?.(msg.payload.studentId, msg.payload.inEpoch);
        this.options.onCheckinRequest?.(msg.payload.studentId);
        break;
      case "ERROR":
        this.options.onError?.(msg.code, msg.message);
        break;
    }
  }

  private send(msg: VirtualTerminalMessage) {
    if (this.ws && this.ws.readyState === WebSocket.OPEN) {
      this.ws.send(JSON.stringify(msg));
    }
  }

  private setStatus(status: TransportStatus) {
    this.status = status;
    this.options.onStatusChange?.(status);
  }

  private scheduleReconnect() {
    this.clearTimers();
    if (!this.shouldReconnect) return;
    const delay = Math.min(10000, 1000 * Math.pow(1.5, this.reconnectAttempts));
    this.reconnectAttempts++;
    this.reconnectTimer = setTimeout(() => {
      if (this.shouldReconnect) {
        this.connect();
      }
    }, delay);
  }

  private startHeartbeat() {
    this.heartbeatTimer = setInterval(() => {
      this.send({ type: "HEARTBEAT", timestamp: Math.floor(Date.now() / 1000) });
    }, 20000);
  }

  private clearTimers() {
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
    if (this.heartbeatTimer) {
      clearInterval(this.heartbeatTimer);
      this.heartbeatTimer = null;
    }
  }
}
