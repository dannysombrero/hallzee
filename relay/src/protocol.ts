export interface PublicActivePass {
  studentId?: string;
  name: string;
  outEpoch: number;
}

export interface WaitlistItem {
  studentId: string;
  name: string;
  position: number;
  joinedEpoch: number;
}

export interface RoomStatePayload {
  roomCode: string;
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
}

export interface CheckoutRejectPayload {
  requestId: string;
  reason: "CAPACITY_REACHED" | "FLAGGED_RESTRICTION" | "POLICY_BLOCKED" | "UNKNOWN_STUDENT" | "ALREADY_OUT";
  message: string;
}

export interface CheckinRequestPayload {
  studentId: string;
}

export interface CheckinConfirmPayload {
  studentId: string;
  inEpoch: number;
}

export type VirtualTerminalMessage =
  | {
      type: "CLAIM_ROOM";
      roomCode: string;
      hostSecret: string;
      pinHash?: string;
      capacity: number;
    }
  | {
      type: "CLAIM_OK";
      roomCode: string;
      sessionExpiresAtEpoch: number;
    }
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
      payload: { studentId: string };
    }
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
    };
