import http from "node:http";
import { WebSocketServer } from "ws";
import { RoomCoordinator } from "../../../relay/src/RoomCoordinator.ts";

/** Local socket adapter for the production room coordinator. No parallel protocol implementation. */
export class MockRelayServer {
  private server?: http.Server;
  private sockets?: WebSocketServer;
  private timer?: ReturnType<typeof setInterval>;
  private rooms = new Map<string, RoomCoordinator>();
  async start(port = 4192): Promise<number> {
    this.server = http.createServer((req, res) => {
      res.writeHead(req.url === "/health" ? 200 : 404);
      res.end(req.url === "/health" ? '{"status":"ok"}' : "Not found");
    });
    this.sockets = new WebSocketServer({ server: this.server, maxPayload: 8192 });
    this.sockets.on("connection", (socket, request) => {
      const code = new URL(request.url!, "http://localhost").searchParams.get("room")?.toUpperCase() || "";
      if (!/^[A-Z0-9][A-Z0-9-]{2,15}$/.test(code)) { socket.close(); return; }
      let room = this.rooms.get(code);
      if (!room) { room = new RoomCoordinator(code); this.rooms.set(code, room); }
      const current = room;
      const peer = current.connect(message => { if (socket.readyState === 1) socket.send(JSON.stringify(message)); });
      socket.on("message", data => { try { current.receive(peer, JSON.parse(data.toString())); } catch { socket.close(); } });
      socket.on("close", () => current.disconnect(peer));
    });
    this.timer = setInterval(() => { for (const room of this.rooms.values()) room.tick(); }, 1000);
    await new Promise<void>(resolve => this.server!.listen(port, "127.0.0.1", resolve));
    return (this.server.address() as { port: number }).port;
  }
  async stop() {
    clearInterval(this.timer);
    for (const socket of this.sockets?.clients || []) socket.terminate();
    await new Promise<void>(resolve => this.sockets ? this.sockets.close(() => resolve()) : resolve());
    await new Promise<void>(resolve => this.server ? this.server.close(() => resolve()) : resolve());
  }
}
export async function startMockRelay(port = 4192) {
  const server = new MockRelayServer();
  return { port: await server.start(port), close: () => server.stop() };
}
