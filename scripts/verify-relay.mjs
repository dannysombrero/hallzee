// Exercise the actual Worker/DO over WebSockets with a temporary synthetic room.
import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";

const origin = new URL(process.env.HALLZEE_RELAY_ORIGIN || "https://relay.hallzee.com");
const roomCode = `VERIFY-${randomUUID().slice(0, 8)}`.toUpperCase();
const hostSecret = randomUUID();
const studentId = "90000001";
const clients = [];

async function connect() {
  const url = new URL("/ws", origin);
  url.protocol = origin.protocol === "https:" ? "wss:" : "ws:";
  url.searchParams.set("room", roomCode);
  const socket = new WebSocket(url);
  const queue = [];
  const waiters = [];
  const client = {
    socket,
    send: message => socket.send(JSON.stringify(message)),
    wait: predicate => new Promise((resolve, reject) => {
      const index = queue.findIndex(predicate);
      if (index >= 0) { resolve(queue.splice(index, 1)[0]); return; }
      const entry = { predicate, resolve: message => { clearTimeout(timer); resolve(message); } };
      const timer = setTimeout(() => {
        waiters.splice(waiters.indexOf(entry), 1);
        reject(new Error("Timed out waiting for the relay protocol."));
      }, 15000);
      waiters.push(entry);
    }),
  };
  socket.addEventListener("message", event => {
    const message = JSON.parse(event.data);
    const index = waiters.findIndex(w => w.predicate(message));
    if (index >= 0) waiters.splice(index, 1)[0].resolve(message);
    else queue.push(message);
  });
  clients.push(client);
  await new Promise((resolve, reject) => {
    const timeout = setTimeout(() => reject(new Error("Relay connection timed out.")), 15000);
    socket.addEventListener("open", () => { clearTimeout(timeout); resolve(); }, { once: true });
    socket.addEventListener("error", () => { clearTimeout(timeout); reject(new Error("Relay connection failed.")); }, { once: true });
  });
  return client;
}
const type = name => message => message.type === name;
const state = (status, count) => message => message.type === "ROOM_STATE"
  && message.state.status === status && message.state.activeCount === count;

let teacher;
try {
  let healthy = false;
  let lastHealthStatus = "not checked";
  const healthDeadline = Date.now() + 300000;
  let nextHealthLog = Date.now() + 15000;
  while (Date.now() < healthDeadline) {
    try {
      const health = await fetch(new URL("/health", origin), { signal: AbortSignal.timeout(5000) });
      lastHealthStatus = `HTTP ${health.status}`;
      const body = await health.json().catch(() => null);
      healthy = health.status === 200 && body?.service === "hallzee-relay";
      if (healthy) break;
      if (health.status === 200) lastHealthStatus = "unexpected health response";
    } catch (error) {
      // Log only known public connection categories, never raw request/error data.
      const code = error.cause?.code || error.name;
      lastHealthStatus = ["ENOTFOUND", "EAI_AGAIN", "ECONNRESET", "ECONNREFUSED",
        "ERR_TLS_CERT_ALTNAME_INVALID", "CERT_HAS_EXPIRED", "UND_ERR_CONNECT_TIMEOUT",
        "TimeoutError"].includes(code) ? code : "connection unavailable";
    }
    if (Date.now() >= nextHealthLog) {
      console.log(`Waiting for relay hostname activation: ${lastHealthStatus}.`);
      nextHealthLog = Date.now() + 15000;
    }
    await new Promise(resolve => setTimeout(resolve, 2000));
  }
  assert.ok(healthy, `Relay health did not become available (${lastHealthStatus}).`);
  teacher = await connect();
  teacher.send({ type: "CLAIM_ROOM", roomCode, hostSecret, capacity: 1 });
  await teacher.wait(type("CLAIM_OK"));
  const station = await connect();
  station.send({ type: "JOIN_ROOM", roomCode });
  await station.wait(state("open", 0));

  station.send({ type: "CHECKOUT_REQUEST", payload: { requestId: "checkout", studentId, destination: "Restroom" } });
  await teacher.wait(type("CHECKOUT_REQUEST"));
  teacher.send({ type: "CHECKOUT_CONFIRM", payload: {
    requestId: "checkout", studentId, name: "Synthetic Student", destination: "Restroom",
    outEpoch: Math.floor(Date.now() / 1000),
  } });
  await station.wait(type("CHECKOUT_CONFIRM"));
  const publicState = await station.wait(state("open", 1));
  assert.equal(publicState.state.activePasses[0].studentId, undefined);
  assert.equal(publicState.state.activePasses[0].name, "Student");

  teacher.socket.close(1000, "Reconnect verification");
  await station.wait(state("paused", 1));
  teacher = await connect();
  teacher.send({ type: "CLAIM_ROOM", roomCode, hostSecret, capacity: 1, resumeOnly: true });
  await teacher.wait(type("CLAIM_OK"));
  await station.wait(state("open", 1));
  const restored = await teacher.wait(state("open", 1));
  assert.equal(restored.state.activePasses[0].studentId, studentId);

  station.send({ type: "CHECKIN_REQUEST", payload: { requestId: "checkin", studentId } });
  await teacher.wait(type("CHECKIN_REQUEST"));
  teacher.send({ type: "CHECKIN_CONFIRM", payload: { requestId: "checkin", studentId, inEpoch: Math.floor(Date.now() / 1000) } });
  await station.wait(type("CHECKIN_CONFIRM"));
  await station.wait(state("open", 0));
  teacher.send({ type: "CLOSE_ROOM" });
  await teacher.wait(type("ROOM_CLOSED"));
  await station.wait(state("closed", 0));
  console.log("Relay verified: claim, join, checkout, anonymous public state, reconnect, check-in and closure.");
} finally {
  if (teacher?.socket.readyState === WebSocket.OPEN) {
    teacher.send({ type: "CHECKIN_CONFIRM", payload: { studentId, inEpoch: Math.floor(Date.now() / 1000) } });
    teacher.send({ type: "CLOSE_ROOM" });
  }
  for (const client of clients) client.socket.close();
}
