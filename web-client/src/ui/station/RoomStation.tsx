import { useState, useEffect, useRef } from "react";
import {
  VirtualTerminalTransport,
  defaultRelayUrl,
  type TransportStatus,
} from "../../transport/VirtualTerminalTransport";
import type { RoomStatePayload, CheckoutRejectPayload } from "../../protocol/VirtualTerminalProtocol";
import { roomLinks } from "../../domain/RoomLinks";
import { durationLabel } from "../../sync/TerminalClock";

export interface RoomStationProps {
  roomCode: string;
  relayUrl?: string;
}

const COMMON_DESTINATIONS = [
  { id: "Restroom", label: "Restroom", icon: "🚻" },
  { id: "Office", label: "Main Office", icon: "📋" },
  { id: "Library", label: "Library", icon: "📚" },
  { id: "Nurse", label: "Nurse", icon: "🩺" },
  { id: "Counselor", label: "Counselor", icon: "🧠" },
  { id: "Other", label: "Other", icon: "✏️" },
];

export function RoomStation({ roomCode, relayUrl }: RoomStationProps) {
  const code = roomCode.toUpperCase().trim();
  const [status, setStatus] = useState<TransportStatus>("connecting");
  const [roomState, setRoomState] = useState<RoomStatePayload | null>(null);
  const [studentId, setStudentId] = useState("");
  const [checkinMode, setCheckinMode] = useState(false);
  const [waiting, setWaiting] = useState(false);
  const pending = useRef<string | null>(null);
  const pendingTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const successTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const [destination, setDestination] = useState("Restroom");
  const [otherDestination, setOtherDestination] = useState("");
  const [purpose, setPurpose] = useState("");
  const [notice, setNotice] = useState<{ text: string; type: "success" | "error" | "info" } | null>(
    null,
  );
  const [successAnimation, setSuccessAnimation] = useState<string | null>(null);
  const [now, setNow] = useState(Date.now());
  const transportRef = useRef<VirtualTerminalTransport | null>(null);

  // Keep live timers ticking
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, []);

  // Request screen wake lock so door tablet stays awake
  useEffect(() => {
    let lock: any = null;
    if ("wakeLock" in navigator) {
      void (navigator as any).wakeLock
        ?.request("screen")
        .then((l: any) => {
          lock = l;
        })
        .catch(() => {});
    }
    return () => {
      void lock?.release?.();
    };
  }, []);

  // Connect station transport
  useEffect(() => {
    const transport = new VirtualTerminalTransport({
      relayUrl: relayUrl || defaultRelayUrl(),
      roomCode: code,
      role: "station",
      onStatusChange: (s) => {
        setStatus(s);
        if (s !== "connected" && pending.current) {
          pending.current = null; setWaiting(false); clearTimeout(pendingTimer.current);
          setNotice({ text: "Connection interrupted. Wait for the terminal to reconnect and try again.", type: "error" });
        }
      },
      onRoomState: (s) => setRoomState(s),
      onCheckoutConfirm: confirmation => {
        if (confirmation.requestId === pending.current) showSuccessFeedback("Have a good trip! Your pass is active.");
      },
      onCheckinConfirm: confirmation => {
        if (confirmation.requestId === pending.current) showSuccessFeedback("Welcome back! You are checked in.");
      },
      onWaitlistConfirm: (requestId, position) => {
        if (requestId === pending.current) showSuccessFeedback(`You are number ${position} on the waitlist.`);
      },
      onCheckoutReject: (rej: CheckoutRejectPayload) => {
        if (rej.requestId !== pending.current) return;
        pending.current = null; setWaiting(false); clearTimeout(pendingTimer.current);
        setNotice({ text: rej.message, type: "error" });
      },
      onError: (_c, msg, requestId) => {
        if (requestId && requestId !== pending.current) return;
        pending.current = null; setWaiting(false); clearTimeout(pendingTimer.current);
        setNotice({ text: msg, type: "error" });
      },
    });

    transportRef.current = transport;
    transport.connect();

    return () => {
      clearTimeout(pendingTimer.current); clearTimeout(successTimer.current);
      transport.disconnect();
      transportRef.current = null;
    };
  }, [code, relayUrl]);

  const activePasses = roomState?.activePasses || [];
  const capacity = roomState?.capacity || 1;
  const isFull = (roomState?.activeCount || 0) >= capacity;
  const isCheckedOut = checkinMode;
  const roomOpen = status === "connected" && roomState?.status === "open" && roomState.sessionExpiresAtEpoch * 1000 > now;
  const roomStatus = roomState?.status === "expired" ? "SESSION ENDED" : roomState?.status === "closed" ? "TERMINAL CLOSED" : roomState?.status === "paused" ? "TEACHER RECONNECTING" : roomOpen ? "LIVE" : "CONNECTING";

  const handleSubmit = (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    const id = studentId.trim();
    if (!/^\d{1,16}$/.test(id)) {
      setNotice({ text: "Enter your numeric student ID (up to 16 digits).", type: "error" }); return;
    }
    if (!roomOpen || waiting || !transportRef.current) return;
    setNotice(null); setWaiting(true);
    if (isCheckedOut) pending.current = transportRef.current.requestCheckin(id);
    else if (isFull) pending.current = transportRef.current.joinWaitlist(id);
    else {
      const dest = destination === "Other" ? otherDestination.trim() || "Other" : destination;
      pending.current = transportRef.current.requestCheckout(id, dest, purpose.trim() || undefined);
    }
    pendingTimer.current = setTimeout(() => {
      pending.current = null; setWaiting(false);
      setNotice({ text: "No confirmation arrived. Check with your teacher before trying again.", type: "error" });
    }, 15000);
  };

  const showSuccessFeedback = (msg: string) => {
    pending.current = null; setWaiting(false); clearTimeout(pendingTimer.current);
    setSuccessAnimation(msg);
    setNotice(null);
    setStudentId("");
    setPurpose("");
    setOtherDestination("");
    setDestination("Restroom");

    successTimer.current = setTimeout(() => {
      setSuccessAnimation(null);
    }, 3500);
  };

  return (
    <div
      style={{
        minHeight: "100vh",
        background: "linear-gradient(135deg, #f0f9ff 0%, #e0f2fe 100%)",
        fontFamily: "system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        padding: "20px",
        boxSizing: "border-box",
      }}
    >
      <div
        style={{
          width: "100%",
          maxWidth: "520px",
          background: "#ffffff",
          borderRadius: "24px",
          boxShadow: "0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 8px 10px -6px rgba(0, 0, 0, 0.1)",
          border: "1px solid #bae6fd",
          overflow: "hidden",
        }}
      >
        {/* HEADER */}
        <div
          style={{
            background: isFull
              ? "linear-gradient(135deg, #f97316 0%, #ea580c 100%)"
              : "linear-gradient(135deg, #0284c7 0%, #0369a1 100%)",
            color: "#ffffff",
            padding: "24px",
            textAlign: "center",
            transition: "background 0.3s ease",
          }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <span
              style={{
                fontSize: "12px",
                fontWeight: 700,
                letterSpacing: "1px",
                textTransform: "uppercase",
                opacity: 0.9,
              }}
            >
              HALLZEE CLASSROOM PASS
            </span>
            <span
              style={{
                fontSize: "11px",
                fontWeight: 700,
                background: "rgba(255, 255, 255, 0.2)",
                padding: "2px 8px",
                borderRadius: "12px",
              }}
            >
              {roomStatus}
            </span>
          </div>

          <h1
            style={{
              margin: "12px 0 4px 0",
              fontSize: "30px",
              fontWeight: 800,
              letterSpacing: "1px",
            }}
          >
            {code}
          </h1>

          <div
            style={{
              display: "inline-flex",
              alignItems: "center",
              gap: "6px",
              background: "rgba(255, 255, 255, 0.15)",
              padding: "6px 14px",
              borderRadius: "20px",
              marginTop: "8px",
              fontSize: "13px",
              fontWeight: 600,
            }}
          >
            {!roomOpen ? <span>{roomStatus}</span> : isFull ? (
              <span>🔴 PASSES FULL ({activePasses.length} / {capacity} in use)</span>
            ) : (
              <span>🟢 PASS AVAILABLE ({activePasses.length} / {capacity} in use)</span>
            )}
          </div>
        </div>

        {/* BODY */}
        <div style={{ padding: "24px" }}>
          {/* SUCCESS OVERLAY */}
          {successAnimation ? (
            <div
              style={{
                padding: "40px 20px",
                textAlign: "center",
                background: "#f0fdf4",
                borderRadius: "16px",
                border: "2px solid #86efac",
              }}
            >
              <div style={{ fontSize: "56px", marginBottom: "12px" }}>✅</div>
              <h2 style={{ fontSize: "22px", fontWeight: 800, color: "#166534", margin: "0 0 8px 0" }}>
                {successAnimation}
              </h2>
              <p style={{ fontSize: "14px", color: "#15803d", margin: 0 }}>
                Screen will reset in a moment...
              </p>
            </div>
          ) : (
            <>
              {/* CURRENTLY OUT TRAY (Privacy safe: anonymous pass timers only) */}
              {activePasses.length > 0 && (
                <div
                  style={{
                    background: "#f8fafc",
                    border: "1px solid #e2e8f0",
                    borderRadius: "14px",
                    padding: "12px 16px",
                    marginBottom: "20px",
                  }}
                >
                  <span
                    style={{
                      fontSize: "11px",
                      fontWeight: 700,
                      color: "#64748b",
                      textTransform: "uppercase",
                      letterSpacing: "0.5px",
                    }}
                  >
                    Currently Out
                  </span>
                  <div style={{ display: "flex", flexWrap: "wrap", gap: "8px", marginTop: "6px" }}>
                    {activePasses.map((p, idx) => {
                      const elapsedSecs = Math.max(0, Math.floor((now / 1000) - p.outEpoch));
                      return (
                        <div
                          key={idx}
                          style={{
                            background: "#ffffff",
                            border: "1px solid #cbd5e1",
                            borderRadius: "8px",
                            padding: "4px 10px",
                            fontSize: "13px",
                            fontWeight: 600,
                            color: "#1e293b",
                          }}
                        >
                          👤 {p.name} · {durationLabel(elapsedSecs)}
                        </div>
                      );
                    })}
                  </div>
                </div>
              )}

              {/* ACTION FORM */}
              <form onSubmit={handleSubmit}>
                <div className="station-mode-tabs" role="group" aria-label="Pass action">
                  <button type="button" className={!checkinMode ? "hallzee-pill-btn" : "hallzee-pill-btn-sky"} aria-pressed={!checkinMode} disabled={waiting} onClick={() => { setCheckinMode(false); setNotice(null); }}>Check out</button>
                  <button type="button" className={checkinMode ? "hallzee-pill-btn" : "hallzee-pill-btn-sky"} aria-pressed={checkinMode} disabled={waiting} onClick={() => { setCheckinMode(true); setNotice(null); }}>Check in</button>
                </div>
                {!roomOpen && <p className="banner" role="status">{roomState?.status === "closed" || roomState?.status === "expired" ? "This terminal is closed. Ask your teacher to open a session." : "Waiting for your teacher's terminal. Check-in and checkout will be available when it reconnects."}</p>}
                <label style={{ display: "block", marginBottom: "16px" }}>
                  <span style={{ fontSize: "14px", fontWeight: 700, color: "#334155" }}>
                    Enter Student ID:
                  </span>
                  <input
                    aria-label="Student ID"
                    type="text"
                    inputMode="numeric"
                    autoFocus
                    maxLength={16}
                    disabled={waiting}
                    value={studentId}
                    onChange={(e) => {
                      setStudentId(e.target.value);
                      setNotice(null);
                    }}
                    placeholder="Type your student ID..."
                    style={{
                      width: "100%",
                      boxSizing: "border-box",
                      padding: "14px 16px",
                      fontSize: "18px",
                      fontWeight: 600,
                      borderRadius: "12px",
                      border: "2px solid #bae6fd",
                      outline: "none",
                      marginTop: "6px",
                      background: "#f8fafc",
                    }}
                  />
                </label>

                {/* DESTINATION SELECTION */}
                {!isCheckedOut && (
                  <div style={{ marginBottom: "16px" }}>
                    <span
                      style={{
                        fontSize: "14px",
                        fontWeight: 700,
                        color: "#334155",
                        display: "block",
                        marginBottom: "8px",
                      }}
                    >
                      Where are you going?
                    </span>
                    <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: "8px" }}>
                      {COMMON_DESTINATIONS.map((d) => {
                        const active = destination === d.id;
                        return (
                          <button
                            key={d.id}
                            type="button"
                            onClick={() => setDestination(d.id)}
                            style={{
                              padding: "10px 6px",
                              borderRadius: "10px",
                              border: active ? "2px solid #0284c7" : "1px solid #e2e8f0",
                              background: active ? "#e0f2fe" : "#ffffff",
                              color: active ? "#0369a1" : "#475569",
                              fontWeight: active ? 700 : 500,
                              fontSize: "13px",
                              cursor: "pointer",
                              display: "flex",
                              flexDirection: "column",
                              alignItems: "center",
                              gap: "4px",
                              transition: "all 0.15s ease",
                            }}
                          >
                            <span style={{ fontSize: "18px" }}>{d.icon}</span>
                            <span>{d.label}</span>
                          </button>
                        );
                      })}
                    </div>

                    {destination === "Other" && (
                      <input
                        aria-label="Other destination"
                        type="text"
                        placeholder="Type destination..."
                        value={otherDestination}
                        onChange={(e) => setOtherDestination(e.target.value)}
                        style={{
                          width: "100%",
                          boxSizing: "border-box",
                          padding: "10px 14px",
                          fontSize: "14px",
                          borderRadius: "10px",
                          border: "1px solid #cbd5e1",
                          marginTop: "8px",
                        }}
                      />
                    )}

                    <input
                      aria-label="Optional note"
                      type="text"
                      placeholder="Note or reason (optional)"
                      value={purpose}
                      onChange={(e) => setPurpose(e.target.value)}
                      style={{
                        width: "100%",
                        boxSizing: "border-box",
                        padding: "10px 14px",
                        fontSize: "13px",
                        borderRadius: "10px",
                        border: "1px solid #e2e8f0",
                        marginTop: "8px",
                        background: "#f8fafc",
                      }}
                    />
                  </div>
                )}

                {/* NOTICES */}
                {notice && (
                  <div
                    style={{
                      padding: "10px 14px",
                      borderRadius: "10px",
                      fontSize: "13px",
                      fontWeight: 600,
                      marginBottom: "16px",
                      background: notice.type === "error" ? "#fee2e2" : "#e0f2fe",
                      color: notice.type === "error" ? "#dc2626" : "#0369a1",
                      border: `1px solid ${notice.type === "error" ? "#fca5a5" : "#bae6fd"}`,
                    }}
                  >
                    {notice.text}
                  </div>
                )}

                {/* SUBMIT BUTTON */}
                <button
                  type="submit"
                  disabled={!roomOpen || !studentId.trim() || waiting}
                  style={{
                    width: "100%",
                    padding: "16px",
                    borderRadius: "14px",
                    fontSize: "18px",
                    fontWeight: 800,
                    border: "none",
                    cursor: "pointer",
                    color: "#ffffff",
                    background: isCheckedOut
                      ? "#10b981"
                      : isFull
                        ? "#f97316"
                        : "#0284c7",
                    boxShadow: "0 4px 6px -1px rgba(0, 0, 0, 0.1)",
                    transition: "all 0.15s ease",
                    opacity: !roomOpen || !studentId.trim() || waiting ? 0.6 : 1,
                  }}
                >
                  {waiting ? "Waiting for confirmation…" : isCheckedOut
                    ? "✓ I'M BACK (CHECK IN)"
                    : isFull
                      ? "⏳ JOIN WAITLIST"
                      : "CHECK OUT →"}
                </button>
              </form>

              {/* WAITLIST FOOTER */}
              {!!roomState?.waitlistCount && (
                <div
                  style={{
                    marginTop: "20px",
                    paddingTop: "14px",
                    borderTop: "1px solid #f1f5f9",
                    fontSize: "12px",
                    color: "#64748b",
                    textAlign: "center",
                  }}
                >
                  <strong>Waiting students: {roomState.waitlistCount}</strong>
                </div>
              )}
            </>
          )}
        </div>
      </div>
      <a className="station-change-room" href={roomLinks(code).join}>Join a different terminal</a>
    </div>
  );
}
