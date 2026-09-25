import { RoomDurableObject } from "./RoomDurableObject";

export { RoomDurableObject };

interface Env {
  ROOM_DO: DurableObjectNamespace;
}

// Best-effort abuse budget for distinct room codes, allowing a shared school IP.
// Resume connections do not carry create=1 and repeated attempts for the same code
// do not spend another slot. This in-memory budget is not a durable global quota.
const ipCreations = new Map<string, { codes: Set<string>; resetEpoch: number }>();
function checkRateLimit(ip: string, code: string): boolean {
  const now = Math.floor(Date.now() / 1000);
  for (const [key, entry] of ipCreations) if (now >= entry.resetEpoch) ipCreations.delete(key);
  let entry = ipCreations.get(ip);
  if (!entry) {
    if (ipCreations.size >= 10000) return false;
    entry = { codes: new Set(), resetEpoch: now + 86400 };
    ipCreations.set(ip, entry);
  }
  if (entry.codes.has(code)) return true;
  if (entry.codes.size >= 200) return false;
  entry.codes.add(code);
  return true;
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);

    if (url.pathname === "/health") {
      return new Response(JSON.stringify({ status: "ok", service: "hallzee-relay" }), {
        headers: { "Content-Type": "application/json" },
      });
    }

    if (url.pathname === "/ws") {
      const roomCode = (url.searchParams.get("room") || "").trim().toUpperCase();
      if (!roomCode || !/^[A-Z0-9][A-Z0-9-]{2,15}$/.test(roomCode) || ["JOIN", "PASS", "ASSETS", "ICONS"].includes(roomCode)) {
        return new Response("Invalid room code", { status: 400 });
      }

      // Check rate limit if this is a room creation attempt
      const clientIp = request.headers.get("CF-Connecting-IP") || "127.0.0.1";
      const isCreate = url.searchParams.get("create") === "1";
      if (isCreate && !checkRateLimit(clientIp, roomCode)) {
        return new Response("Too many new terminal codes today. Try again later.", { status: 429 });
      }

      const id = env.ROOM_DO.idFromName(roomCode);
      const stub = env.ROOM_DO.get(id);
      return stub.fetch(request);
    }

    return new Response("Hallzee Virtual Terminal Relay", { status: 200 });
  },
};
