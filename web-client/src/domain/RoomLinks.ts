export const ROOM_CODE_PATTERN = /^[A-Z0-9][A-Z0-9-]{2,15}$/;
const reservedCodes = new Set(["JOIN", "PASS", "ASSETS", "ICONS"]);

export function normalizeRoomCode(value: string): string {
  return value.trim().toUpperCase();
}

export function validRoomCode(value: string): boolean {
  const code = normalizeRoomCode(value);
  return ROOM_CODE_PATTERN.test(code) && !reservedCodes.has(code);
}

export type AppRoute =
  | { type: "dashboard" }
  | { type: "join"; invalidCode?: boolean }
  | { type: "station"; roomCode: string };

export function appRoute(url: URL): AppRoute {
  const path = url.pathname.replace(/\/+$/, "") || "/";
  const code = url.searchParams.get("room") || url.searchParams.get("pass");
  if (code) return validRoomCode(code)
    ? { type: "station", roomCode: normalizeRoomCode(code) }
    : { type: "join", invalidCode: true };
  if (path === "/join" || path === "/pass") return { type: "join" };
  if (path === "/") return url.hostname === "pass.hallzee.com"
    ? { type: "join" } : { type: "dashboard" };
  let candidate: string;
  try { candidate = decodeURIComponent(path.replace(/^\/pass\//, "/").slice(1)); }
  catch { return { type: "join", invalidCode: true }; }
  return validRoomCode(candidate)
    ? { type: "station", roomCode: normalizeRoomCode(candidate) }
    : { type: "join", invalidCode: true };
}

export function roomLinks(code: string, origin = window.location.origin) {
  const current = new URL(origin);
  const configured = import.meta.env.VITE_JOIN_ORIGIN as string | undefined;
  const publicOrigin = configured || (current.hostname === "web.hallzee.com"
    ? "https://pass.hallzee.com" : current.origin);
  const base = new URL(publicOrigin).origin;
  const join = new URL(base).hostname === "pass.hallzee.com" ? `${base}/` : `${base}/join`;
  return { join, terminal: `${base}/${encodeURIComponent(normalizeRoomCode(code).toLowerCase())}` };
}
