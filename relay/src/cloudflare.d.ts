// Ambient type definitions for Cloudflare Workers & Durable Objects
// when @cloudflare/workers-types is not installed locally.

interface DurableObjectId {
  toString(): string;
  equals(other: DurableObjectId): boolean;
  name?: string;
}

interface DurableObjectStub {
  id: DurableObjectId;
  name?: string;
  fetch(request: Request | string, init?: RequestInit): Promise<Response>;
}

interface DurableObjectNamespace {
  idFromName(name: string): DurableObjectId;
  idFromString(id: string): DurableObjectId;
  newUniqueId(options?: { jurisdiction?: string }): DurableObjectId;
  get(id: DurableObjectId): DurableObjectStub;
}

interface DurableObjectState {
  id: DurableObjectId;
  storage: unknown;
  waitUntil(promise: Promise<unknown>): void;
}

interface ResponseInit {
  webSocket?: WebSocket;
}
