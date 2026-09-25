import type {
  VirtualTerminalMessage, RoomStatePayload, CheckoutRequestPayload,
  CheckoutConfirmPayload, CheckoutRejectPayload, CheckinRequestPayload,
  CheckinConfirmPayload,
} from "../protocol/VirtualTerminalProtocol";

export type TransportStatus = "disconnected" | "connecting" | "connected" | "error";
export interface VirtualTerminalTransportOptions {
  relayUrl?: string;
  roomCode: string;
  role: "host" | "station";
  hostSecret?: string;
  pinHash?: string;
  capacity?: number;
  resumeOnly?: boolean;
  onStatusChange?: (status: TransportStatus) => void;
  onClaim?: (expiresAtEpoch: number) => void;
  onRoomState?: (state: RoomStatePayload) => void;
  onCheckoutRequest?: (request: CheckoutRequestPayload) => void;
  onCheckoutConfirm?: (confirmation: CheckoutConfirmPayload) => void;
  onCheckoutReject?: (rejection: CheckoutRejectPayload) => void;
  onCheckinRequest?: (request: CheckinRequestPayload) => void;
  onCheckinConfirm?: (confirmation: CheckinConfirmPayload) => void;
  onWaitlistConfirm?: (requestId: string, position: number) => void;
  onError?: (code: string, message: string, requestId?: string) => void;
}
export function defaultRelayUrl(): string {
  if (import.meta.env?.VITE_RELAY_URL) return import.meta.env.VITE_RELAY_URL;
  if (typeof window !== "undefined" && ["localhost", "127.0.0.1"].includes(window.location.hostname))
    return "ws://127.0.0.1:4192/ws";
  return "wss://relay.hallzee.com/ws";
}
export class VirtualTerminalTransport {
  private ws: WebSocket | null = null;
  private shouldReconnect = false;
  private claimed = false;
  private reconnectAttempts = 0;
  private reconnectTimer?: ReturnType<typeof setTimeout>;
  private heartbeatTimer?: ReturnType<typeof setInterval>;
  private lastResponse = 0;
  private closeResolve?: () => void;
  private closeReject?: (error: Error) => void;
  constructor(private options: VirtualTerminalTransportOptions) {}

  connect() {
    this.shouldReconnect = true;
    this.clearTimers();
    this.options.onStatusChange?.("connecting");
    const url = new URL(this.options.relayUrl || defaultRelayUrl());
    url.searchParams.set("room", this.options.roomCode.toUpperCase());
    if (this.options.role === "host" && !this.claimed && !this.options.resumeOnly)
      url.searchParams.set("create", "1");
    try {
      const ws = new WebSocket(url);
      this.ws = ws;
      ws.onopen = () => {
        if (this.ws !== ws) return;
        this.lastResponse = Date.now();
        this.options.onStatusChange?.("connected");
        this.heartbeatTimer = setInterval(() => {
          if (Date.now() - this.lastResponse > 45000) { ws.close(); return; }
          this.send({ type: "HEARTBEAT", timestamp: Math.floor(Date.now() / 1000) });
        }, 15000);
        this.send(this.options.role === "host" ? {
          type: "CLAIM_ROOM", roomCode: this.options.roomCode.toUpperCase(),
          hostSecret: this.options.hostSecret || "", pinHash: this.options.pinHash,
          capacity: this.options.capacity || 1, resumeOnly: this.claimed || this.options.resumeOnly,
        } : { type: "JOIN_ROOM", roomCode: this.options.roomCode.toUpperCase() });
      };
      ws.onmessage = event => {
        if (this.ws !== ws) return;
        this.lastResponse = Date.now();
        let msg: VirtualTerminalMessage;
        try { msg = JSON.parse(event.data); } catch { return; }
        this.handleMessage(msg);
      };
      ws.onclose = () => {
        if (this.ws !== ws) return;
        this.ws = null;
        this.clearTimers();
        this.options.onStatusChange?.("disconnected");
        this.closeReject?.(new Error("The room could not confirm it closed. Reconnect and try again."));
        if (this.shouldReconnect) this.scheduleReconnect();
      };
      ws.onerror = () => { if (this.ws === ws) this.options.onStatusChange?.("error"); };
    } catch {
      this.options.onStatusChange?.("error");
      this.scheduleReconnect();
    }
  }
  disconnect() {
    this.shouldReconnect = false;
    this.clearTimers();
    const ws = this.ws;
    this.ws = null;
    ws?.close();
    this.options.onStatusChange?.("disconnected");
  }
  async closeRoom() {
    if (this.options.role !== "host") return;
    await new Promise<void>((resolve, reject) => {
      const timer = setTimeout(() => finish(new Error("Reconnect before ending this session.")), 5000);
      const finish = (error?: Error) => {
        clearTimeout(timer); this.closeResolve = undefined; this.closeReject = undefined;
        if (error) reject(error); else resolve();
      };
      this.closeResolve = () => finish();
      this.closeReject = finish;
      if (!this.send({ type: "CLOSE_ROOM" })) finish(new Error("Reconnect before ending this session."));
    });
    this.disconnect();
  }
  confirmCheckout(payload: CheckoutConfirmPayload) { return this.send({ type: "CHECKOUT_CONFIRM", payload }); }
  rejectCheckout(payload: CheckoutRejectPayload) { return this.send({ type: "CHECKOUT_REJECT", payload }); }
  confirmCheckin(studentId: string, inEpoch = Math.floor(Date.now() / 1000), requestId?: string) {
    return this.send({ type: "CHECKIN_CONFIRM", payload: { studentId, inEpoch, requestId } });
  }
  setCapacity(capacity: number) { this.options.capacity = capacity; this.send({ type: "SET_CAPACITY", capacity }); }
  performWaitlistAction(studentId: string, action: "bump" | "dismiss" | "pause" | "resume") {
    this.send({ type: "WAITLIST_ACTION", payload: { studentId, action } });
  }
  requestCheckout(studentId: string, destination: string, purpose?: string) {
    const requestId = crypto.randomUUID();
    this.send({ type: "CHECKOUT_REQUEST", payload: { requestId, studentId, destination, purpose } });
    return requestId;
  }
  requestCheckin(studentId: string) {
    const requestId = crypto.randomUUID();
    this.send({ type: "CHECKIN_REQUEST", payload: { requestId, studentId } });
    return requestId;
  }
  joinWaitlist(studentId: string) {
    const requestId = crypto.randomUUID();
    this.send({ type: "WAITLIST_JOIN", payload: { requestId, studentId } });
    return requestId;
  }
  private handleMessage(msg: VirtualTerminalMessage) {
    switch (msg.type) {
      case "CLAIM_OK":
        this.claimed = true; this.reconnectAttempts = 0;
        this.options.onClaim?.(msg.sessionExpiresAtEpoch); break;
      case "ROOM_CLOSED": this.closeResolve?.(); break;
      case "ROOM_STATE": this.reconnectAttempts = 0; this.options.onRoomState?.(msg.state); break;
      case "CHECKOUT_REQUEST": this.options.onCheckoutRequest?.(msg.payload); break;
      case "CHECKOUT_CONFIRM": this.options.onCheckoutConfirm?.(msg.payload); break;
      case "CHECKOUT_REJECT": this.options.onCheckoutReject?.(msg.payload); break;
      case "CHECKIN_REQUEST": this.options.onCheckinRequest?.(msg.payload); break;
      case "CHECKIN_CONFIRM": this.options.onCheckinConfirm?.(msg.payload); break;
      case "WAITLIST_CONFIRM": this.options.onWaitlistConfirm?.(msg.payload.requestId, msg.payload.position); break;
      case "ERROR":
        if (["ALREADY_CLAIMED", "SESSION_EXPIRED", "ROOM_INACTIVE", "HOST_REPLACED"].includes(msg.code) && this.options.role === "host")
          this.shouldReconnect = false;
        this.closeReject?.(new Error(msg.message));
        this.options.onError?.(msg.code, msg.message, msg.requestId); break;
    }
  }
  private send(msg: VirtualTerminalMessage) {
    if (this.ws?.readyState !== WebSocket.OPEN) return false;
    this.ws.send(JSON.stringify(msg)); return true;
  }
  private scheduleReconnect() {
    this.clearTimers();
    if (!this.shouldReconnect) return;
    this.reconnectTimer = setTimeout(() => this.connect(), Math.min(10000, 1000 * 1.5 ** this.reconnectAttempts++));
  }
  private clearTimers() { clearTimeout(this.reconnectTimer); clearInterval(this.heartbeatTimer); }
}
