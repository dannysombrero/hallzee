import { MockRelayServer } from "../../../relay/test/mock-relay.ts";
const server = new MockRelayServer();
await server.start();
process.on("SIGTERM", () => { void server.stop().then(() => process.exit(0)); });
