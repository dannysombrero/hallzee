import type {
  VirtualTerminalMessage, RoomStatePayload, CheckoutConfirmPayload, WaitlistItem,
} from "./protocol";

export interface RoomPeer {
  role: "host" | "station" | "unjoined";
  send: (message: VirtualTerminalMessage) => void;
}
interface Pending { peer: RoomPeer; studentId: string; kind: "checkout" | "checkin"; at: number }

/** Shared by the Worker and local test relay so tests exercise the actual room lifecycle. */
export class RoomCoordinator {
  private peers = new Set<RoomPeer>();
  private hostSecret: string | null = null;
  private pinHash?: string;
  private capacity = 1;
  private passes = new Map<string, CheckoutConfirmPayload>();
  private waitlist: WaitlistItem[] = [];
  private pending = new Map<string, Pending>();
  private expiresAt = 0;
  private ended: "closed" | "expired" = "closed";
  private queuePaused = false;
  constructor(readonly code: string, private now = () => Math.floor(Date.now() / 1000)) {}

  connect(send: RoomPeer["send"]): RoomPeer {
    const peer: RoomPeer = { role: "unjoined", send };
    this.peers.add(peer);
    return peer;
  }
  disconnect(peer: RoomPeer) {
    this.peers.delete(peer);
    for (const [id, request] of this.pending) if (request.peer === peer) this.pending.delete(id);
    if (peer.role === "host") {
      for (const [id, request] of this.pending) this.error(request.peer, "ROOM_OFFLINE", "Your teacher is reconnecting. Try again shortly.", id);
      this.pending.clear();
      this.broadcast();
    }
  }
  tick() {
    if (this.hostSecret && this.now() >= this.expiresAt) this.end("expired");
    for (const [id, request] of this.pending) if (this.now() - request.at > 20) {
      this.error(request.peer, "REQUEST_TIMEOUT", "The teacher's terminal did not confirm this request. Please try again.", id);
      this.pending.delete(id);
    }
  }
  receive(peer: RoomPeer, raw: unknown) {
    if (!this.peers.has(peer)) return;
    this.tick();
    if (!raw || typeof raw !== "object" || !("type" in raw) || typeof raw.type !== "string") {
      this.error(peer, "BAD_MESSAGE", "Invalid room message."); return;
    }
    const msg = raw as VirtualTerminalMessage;
    if (msg.type === "CLAIM_ROOM" || msg.type === "JOIN_ROOM") {
      if (typeof msg.roomCode !== "string" || msg.roomCode.toUpperCase() !== this.code) {
        this.error(peer, "BAD_ROOM", "The terminal code does not match this connection."); return;
      }
    }
    if (msg.type === "CLAIM_ROOM") {
      if (typeof msg.hostSecret !== "string" || msg.hostSecret.length < 8 || msg.hostSecret.length > 200) {
        this.error(peer, "BAD_MESSAGE", "A valid room owner is required."); return;
      }
      if (msg.resumeOnly && !this.hostSecret) {
        this.error(peer, "SESSION_EXPIRED", "This session has ended. Start a new virtual terminal.");
        peer.send({ type: "ROOM_STATE", state: this.snapshot(false) }); return;
      }
      if (this.hostSecret && msg.hostSecret !== this.hostSecret && (!this.pinHash || msg.pinHash !== this.pinHash)) {
        this.error(peer, "ALREADY_CLAIMED", "That code is already in use. Choose another code or enter its recovery PIN."); return;
      }
      // A reclaimed room has exactly one teacher, including PIN-based recovery.
      for (const old of this.peers) if (old !== peer && old.role === "host") {
        old.role = "unjoined";
        this.error(old, "HOST_REPLACED", "This room was opened on another teacher window.");
      }
      if (!this.hostSecret) this.expiresAt = this.now() + 12 * 3600;
      this.hostSecret = msg.hostSecret;
      if (typeof msg.pinHash === "string" && /^[a-f0-9]{64}$/.test(msg.pinHash)) this.pinHash = msg.pinHash;
      this.capacity = this.validCapacity(msg.capacity);
      peer.role = "host";
      peer.send({ type: "CLAIM_OK", roomCode: this.code, sessionExpiresAtEpoch: this.expiresAt });
      this.broadcast(); return;
    }
    if (msg.type === "JOIN_ROOM") {
      if (peer.role !== "host") peer.role = "station";
      peer.send({ type: "ROOM_STATE", state: this.snapshot(peer.role === "host") }); return;
    }
    if (msg.type === "HEARTBEAT") {
      peer.send({ type: "HEARTBEAT", timestamp: this.now() }); return;
    }
    if (peer.role === "unjoined") { this.error(peer, "NOT_JOINED", "Join the terminal first."); return; }
    if (msg.type === "CLOSE_ROOM") {
      if (!this.requireHost(peer)) return;
      if (this.passes.size) { this.error(peer, "ACTIVE_PASSES", "Check in or void active passes before ending the session."); return; }
      this.end("closed"); peer.send({ type: "ROOM_CLOSED" }); return;
    }
    if (!this.hostSecret) { this.error(peer, "ROOM_INACTIVE", "This terminal is closed. Ask your teacher to open it."); return; }
    if (msg.type === "SET_CAPACITY") {
      if (!this.requireHost(peer)) return;
      this.capacity = this.validCapacity(msg.capacity); this.broadcast(); return;
    }
    if (msg.type === "WAITLIST_ACTION") {
      if (!this.requireHost(peer) || !msg.payload) return;
      const { studentId, action } = msg.payload;
      if (action === "dismiss") this.waitlist = this.waitlist.filter(w => w.studentId !== studentId);
      else if (action === "bump") {
        const index = this.waitlist.findIndex(w => w.studentId === studentId);
        if (index > 0) this.waitlist.unshift(this.waitlist.splice(index, 1)[0]);
      } else if (action === "pause") this.queuePaused = true;
      else if (action === "resume") this.queuePaused = false;
      this.reindex(); this.broadcast(); return;
    }
    if (!("payload" in msg) || !msg.payload || typeof msg.payload !== "object") return;
    if (msg.type === "CHECKOUT_REJECT") {
      if (!this.requireHost(peer)) return;
      const request = this.pending.get(msg.payload.requestId);
      if (request) request.peer.send(msg);
      this.pending.delete(msg.payload.requestId); return;
    }
    if (!("studentId" in msg.payload) || typeof msg.payload.studentId !== "string" || !/^\d{1,16}$/.test(msg.payload.studentId)) {
      this.error(peer, "INVALID_STUDENT", "Enter a numeric student ID (up to 16 digits).", "requestId" in msg.payload ? msg.payload.requestId : undefined); return;
    }
    const studentId = msg.payload.studentId;
    if (msg.type === "CHECKOUT_CONFIRM") {
      if (!this.requireHost(peer)) return;
      if (!Number.isSafeInteger(msg.payload.outEpoch) || this.passes.has(studentId) || this.passes.size >= this.capacity) return;
      this.passes.set(studentId, {
        ...msg.payload, name: "Student", destination: String(msg.payload.destination || "Restroom").slice(0, 80),
        purpose: msg.payload.purpose?.slice(0, 500), period: msg.payload.period?.slice(0, 100),
      });
      const request = this.pending.get(msg.payload.requestId);
      if (request?.kind === "checkout" && request.studentId === studentId) request.peer.send({
        type: "CHECKOUT_CONFIRM", payload: { requestId: msg.payload.requestId, studentId, name: "Student", destination: "", outEpoch: msg.payload.outEpoch },
      });
      this.pending.delete(msg.payload.requestId);
      this.waitlist = this.waitlist.filter(w => w.studentId !== studentId);
      this.reindex(); this.broadcast(); return;
    }
    if (msg.type === "CHECKIN_CONFIRM") {
      if (!this.requireHost(peer)) return;
      this.passes.delete(studentId);
      for (const [id, request] of this.pending) if (request.kind === "checkin" && request.studentId === studentId) {
        request.peer.send({ type: "CHECKIN_CONFIRM", payload: { studentId, inEpoch: msg.payload.inEpoch, requestId: id } });
        this.pending.delete(id);
      }
      this.broadcast(); return;
    }
    if (msg.type !== "CHECKOUT_REQUEST" && msg.type !== "CHECKIN_REQUEST" && msg.type !== "WAITLIST_JOIN") return;
    const requestId = msg.payload.requestId;
    if (!requestId || typeof requestId !== "string" || requestId.length > 100) {
      this.error(peer, "BAD_MESSAGE", "A request identifier is required."); return;
    }
    const host = [...this.peers].find(p => p.role === "host");
    if (!host) { this.error(peer, "ROOM_OFFLINE", "Your teacher is reconnecting. Try again shortly.", requestId); return; }
    if (msg.type === "WAITLIST_JOIN") {
      if (this.queuePaused || this.waitlist.length >= 100 || this.passes.has(studentId)) {
        this.error(peer, "WAITLIST_UNAVAILABLE", "You cannot join the waitlist right now. Ask your teacher.", requestId); return;
      }
      if (!this.waitlist.some(w => w.studentId === studentId)) this.waitlist.push({ studentId, name: "Student", position: this.waitlist.length + 1, joinedEpoch: this.now() });
      peer.send({ type: "WAITLIST_CONFIRM", payload: { requestId, position: this.waitlist.find(w => w.studentId === studentId)!.position } });
      this.broadcast(); return;
    }
    if (msg.type === "CHECKOUT_REQUEST") {
      if (this.passes.has(studentId) || this.passes.size >= this.capacity) {
        peer.send({ type: "CHECKOUT_REJECT", payload: {
          requestId, reason: this.passes.has(studentId) ? "ALREADY_OUT" : "CAPACITY_REACHED",
          message: this.passes.has(studentId) ? "You already have a pass. Choose Check in when you return." : "All passes are in use. Join the waitlist.",
        } }); return;
      }
      if (typeof msg.payload.destination !== "string" || msg.payload.destination.length > 80 ||
        (msg.payload.purpose !== undefined && (typeof msg.payload.purpose !== "string" || msg.payload.purpose.length > 500))) {
        this.error(peer, "BAD_MESSAGE", "Check the destination and purpose.", requestId); return;
      }
    } else if (!this.passes.has(studentId)) {
      this.error(peer, "NOT_OUT", "No active pass was found for that student ID.", requestId); return;
    }
    if (this.pending.size >= 100 || this.pending.has(requestId)) return;
    this.pending.set(requestId, { peer, studentId, kind: msg.type === "CHECKOUT_REQUEST" ? "checkout" : "checkin", at: this.now() });
    host.send(msg);
  }
  private requireHost(peer: RoomPeer) {
    if (peer.role === "host") return true;
    this.error(peer, "NOT_HOST", "Only the teacher can change this room."); return false;
  }
  private validCapacity(value: number) { return Number.isInteger(value) ? Math.max(1, Math.min(8, value)) : 1; }
  private error(peer: RoomPeer, code: string, message: string, requestId?: string) { peer.send({ type: "ERROR", code, message, requestId }); }
  private reindex() { this.waitlist.forEach((w, index) => { w.position = index + 1; }); }
  private end(reason: "closed" | "expired") {
    this.hostSecret = null; this.pinHash = undefined; this.ended = reason;
    this.passes.clear(); this.waitlist = []; this.pending.clear(); this.queuePaused = false;
    this.broadcast();
    for (const peer of this.peers) if (peer.role === "host") peer.role = "unjoined";
  }
  private snapshot(host: boolean): RoomStatePayload {
    return {
      roomCode: this.code,
      status: this.hostSecret ? ([...this.peers].some(p => p.role === "host") ? "open" : "paused") : this.ended,
      activeCount: this.passes.size, capacity: this.capacity, isFull: this.passes.size >= this.capacity,
      waitlistCount: this.waitlist.length, waitlist: host ? this.waitlist.map(w => ({ ...w })) : [],
      activePasses: [...this.passes.values()].map(p => host ? {
        studentId: p.studentId, name: "Student", outEpoch: p.outEpoch,
        destination: p.destination, purpose: p.purpose, period: p.period,
      } : { name: "Student", outEpoch: p.outEpoch }),
      sessionExpiresAtEpoch: this.expiresAt,
    };
  }
  private broadcast() {
    for (const peer of this.peers) if (peer.role !== "unjoined") peer.send({ type: "ROOM_STATE", state: this.snapshot(peer.role === "host") });
  }
}
