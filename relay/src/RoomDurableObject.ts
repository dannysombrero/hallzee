import { RoomCoordinator } from "./RoomCoordinator";

export class RoomDurableObject {
  private room?: RoomCoordinator;
  private timer?: ReturnType<typeof setInterval>;
  private sockets = new Set<WebSocket>();
  constructor(_state: DurableObjectState, _env: unknown) {}
  async fetch(request: Request): Promise<Response> {
    const url = new URL(request.url);
    if (url.pathname !== "/ws") return new Response("Not found", { status: 404 });
    if (request.headers.get("Upgrade")?.toLowerCase() !== "websocket")
      return new Response("Expected Upgrade: websocket", { status: 426 });
    const code = url.searchParams.get("room")!.toUpperCase();
    this.room ??= new RoomCoordinator(code);
    const room = this.room;
    if (room.code !== code) return new Response("Room mismatch", { status: 400 });
    const pair = new (globalThis as any).WebSocketPair();
    const ws = pair[1] as WebSocket;
    (ws as any).accept();
    this.sockets.add(ws);
    const peer = room.connect(message => { if (ws.readyState === 1) ws.send(JSON.stringify(message)); });
    this.timer ??= setInterval(() => room.tick(), 1000);
    ws.addEventListener("message", event => {
      try {
        if (typeof event.data !== "string" || event.data.length > 8192) { ws.close(1009, "Message too large"); return; }
        room.receive(peer, JSON.parse(event.data));
      } catch { ws.close(1003, "Invalid room message"); }
    });
    const remove = () => {
      if (!this.sockets.delete(ws)) return;
      room.disconnect(peer);
      if (!this.sockets.size) { clearInterval(this.timer); this.timer = undefined; }
    };
    ws.addEventListener("close", remove);
    ws.addEventListener("error", remove);
    return new Response(null, { status: 101, webSocket: pair[0] });
  }
}
