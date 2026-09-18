import { RoomDurableObject } from "./RoomDurableObject";

export { RoomDurableObject };

interface Env {
  ROOM_DO: DurableObjectNamespace;
}

// In-memory IP rate limiter: max 3 creations per 24h per IP
const ipCreations = new Map<string, { count: number; resetEpoch: number }>();

function checkRateLimit(ip: string): boolean {
  const now = Math.floor(Date.now() / 1000);
  const entry = ipCreations.get(ip);
  if (!entry || now > entry.resetEpoch) {
    ipCreations.set(ip, { count: 1, resetEpoch: now + 86400 });
    return true;
  }
  if (entry.count >= 3) {
    return false;
  }
  entry.count++;
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
      if (!roomCode || !/^[A-Z0-9-]{3,16}$/.test(roomCode)) {
        return new Response("Invalid room code", { status: 400 });
      }

      // Check rate limit if this is a room creation attempt
      const clientIp = request.headers.get("CF-Connecting-IP") || "127.0.0.1";
      const isCreate = url.searchParams.get("create") === "1";
      if (isCreate && !checkRateLimit(clientIp)) {
        return new Response("Rate limit exceeded for room creation (max 3/day)", { status: 429 });
      }

      const id = env.ROOM_DO.idFromName(roomCode);
      const stub = env.ROOM_DO.get(id);
      return stub.fetch(request);
    }

    return new Response("Hallzee Virtual Terminal Relay", { status: 200 });
  },
};
