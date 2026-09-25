export interface PublicActivePass {
  studentId?: string;
  name: string;
  outEpoch: number;
  destination?: string;
  purpose?: string;
  period?: string;
}

export interface WaitlistItem {
  studentId: string;
  name: string;
  position: number;
  joinedEpoch: number;
}

export interface RoomStatePayload {
  roomCode: string;
  status: "open" | "paused" | "closed" | "expired";
  activeCount: number;
  capacity: number;
  isFull: boolean;
  waitlistCount: number;
  activePasses: PublicActivePass[];
  waitlist: WaitlistItem[];
  sessionExpiresAtEpoch: number;
}

export interface CheckoutRequestPayload {
  requestId: string;
  studentId: string;
  destination: string;
  purpose?: string;
}

export interface CheckoutConfirmPayload {
  requestId: string;
  studentId: string;
  name: string;
  destination: string;
  purpose?: string;
  outEpoch: number;
  period?: string;
}

export interface CheckoutRejectPayload {
  requestId: string;
  reason: "CAPACITY_REACHED" | "FLAGGED_RESTRICTION" | "POLICY_BLOCKED" | "UNKNOWN_STUDENT" | "ALREADY_OUT" | "ROOM_INACTIVE";
  message: string;
}

export interface CheckinRequestPayload {
  studentId: string;
  requestId?: string;
}

export interface CheckinConfirmPayload {
  studentId: string;
  inEpoch: number;
  requestId?: string;
}

// Union of all messages that flow across the WebSocket
export type VirtualTerminalMessage =
  | {
      type: "CLAIM_ROOM";
      roomCode: string;
      hostSecret: string;
      pinHash?: string;
      capacity: number;
      resumeOnly?: boolean;
    }
  | {
      type: "CLAIM_OK";
      roomCode: string;
      sessionExpiresAtEpoch: number;
    }
  | { type: "CLOSE_ROOM" }
  | { type: "ROOM_CLOSED" }
  | { type: "SET_CAPACITY"; capacity: number }
  | {
      type: "JOIN_ROOM";
      roomCode: string;
    }
  | {
      type: "ROOM_STATE";
      state: RoomStatePayload;
    }
  | {
      type: "CHECKOUT_REQUEST";
      payload: CheckoutRequestPayload;
    }
  | {
      type: "CHECKOUT_CONFIRM";
      payload: CheckoutConfirmPayload;
    }
  | {
      type: "CHECKOUT_REJECT";
      payload: CheckoutRejectPayload;
    }
  | {
      type: "CHECKIN_REQUEST";
      payload: CheckinRequestPayload;
    }
  | {
      type: "CHECKIN_CONFIRM";
      payload: CheckinConfirmPayload;
    }
  | {
      type: "WAITLIST_JOIN";
      payload: { studentId: string; requestId?: string };
    }
  | { type: "WAITLIST_CONFIRM"; payload: { requestId: string; position: number } }
  | {
      type: "WAITLIST_ACTION";
      payload: { studentId: string; action: "bump" | "dismiss" | "pause" | "resume" };
    }
  | {
      type: "HEARTBEAT";
      timestamp: number;
    }
  | {
      type: "ERROR";
      code: string;
      message: string;
      requestId?: string;
    };
