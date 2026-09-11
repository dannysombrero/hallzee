import type { BluetoothPort, DeviceHandle } from "../transport/BluetoothPort";
import { CredentialRepository } from "../storage/CredentialRepository";
import {
  computeAuthProof,
  computeClaimProof,
  deriveOwnerKey,
  nonce,
  terminalId,
  terminalName,
} from "./WebTerminalCrypto";
import { parseIdentity, protocolError } from "./messages";
import { abortCheck, deadline, HallzeeError } from "../app/errors";
import { Signal } from "../app/events";
export type SessionState =
  | "Disconnected"
  | "Connecting"
  | "AwaitingIdentity"
  | "AwaitingClaim"
  | "SavingCredential"
  | "AwaitingClaimCommit"
  | "AwaitingAuthentication"
  | "Authenticated"
  | "Failed";
export class WebTerminalSession {
  state: SessionState = "Disconnected";
  authenticatedId?: string;
  readonly changed = new Signal<SessionState>();
  readonly application = new Signal<string>();
  readonly dropped = new Signal<void>();
  private generation = 0;
  private pending?: {
    match: (line: string) => boolean;
    resolve: (line: string) => void;
    reject: (error: unknown) => void;
  };
  private disposers: (() => void)[];
  constructor(
    readonly port: BluetoothPort,
    private credentials: CredentialRepository,
  ) {
    this.disposers = [
      port.onLine(this.receive),
      port.onDisconnect(() => {
        this.disconnect();
        this.dropped.emit();
      }),
    ];
  }
  private setState(state: SessionState) {
    this.state = state;
    this.changed.emit(state);
  }
  private receive = (line: string) => {
    if (/^(ERROR|SETTINGS_ERROR|POLICY_ERROR|MANUAL_CHECKIN_ERROR|ACK_ERROR),/.test(line)) {
      if (this.pending) this.pending.reject(protocolError(line));
      else if (this.state === "Authenticated") this.application.emit(line);
      return;
    }
    if (this.pending?.match(line)) {
      this.pending.resolve(line);
      return;
    }
    if (this.state === "Authenticated") this.application.emit(line);
    else if (/^(IDENTITY|CLAIM_OK|AUTH_OK),/.test(line))
      this.pending?.reject(new HallzeeError("UNEXPECTED_HANDSHAKE"));
  };
  private async exchange(
    command: string,
    match: (line: string) => boolean,
    signal?: AbortSignal,
    ms = 10000,
  ) {
    if (this.pending) throw new HallzeeError("OPERATION_BUSY");
    let pending!: NonNullable<WebTerminalSession["pending"]>;
    const response = new Promise<string>((resolve, reject) => {
      pending = { match, resolve, reject };
      this.pending = pending;
    });
    void response.catch(() => {});
    try {
      const operation = (async () => {
        await this.port.sendLine(command, signal);
        return response;
      })();
      return await deadline(operation, signal, ms);
    } finally {
      if (this.pending === pending) this.pending = undefined;
    }
  }
  async open(
    device: DeviceHandle,
    workspaceId: string,
    expectedId?: string,
    code?: string,
    signal?: AbortSignal,
  ) {
    this.disconnect();
    const generation = this.generation;
    const local = new AbortController();
    const cancel = () => local.abort();
    signal?.addEventListener("abort", cancel, { once: true });
    let timedOut = false;
    let timer: ReturnType<typeof setTimeout> | undefined;
    const check = () => {
      abortCheck(signal);
      abortCheck(local.signal);
      if (generation !== this.generation) throw new HallzeeError("CANCELLED");
    };
    try {
      this.setState("Connecting");
      const client = await this.credentials.installationId();
      check();
      await this.port.connect(device, local.signal);
      check();
      this.setState("AwaitingIdentity");
      timer = setTimeout(() => {
        timedOut = true;
        cancel();
      }, 8000);
      const identity = parseIdentity(
        await this.exchange(
          `HELLO,2,${client}`,
          (l) => l.startsWith("IDENTITY,"),
          local.signal,
          8000,
        ),
      );
      check();
      const id = identity.terminalId;
      if (expectedId && id !== terminalId(expectedId))
        throw new HallzeeError(
          "IDENTITY_MISMATCH",
          "This is not your assigned terminal. Select the matching terminal ID.",
        );
      let saved = await this.credentials.get(id);
      check();
      if (identity.claimed) {
        if (!saved || saved.clientId !== client)
          throw new HallzeeError(
            "CREDENTIAL_MISSING",
            "This browser does not own this terminal. Release it from the previous client or use physical owner recovery.",
          );
        this.setState("AwaitingAuthentication");
      } else {
        if (identity.inUse)
          throw new HallzeeError("TERMINAL_IN_USE", "An active pass prevents pairing.");
        if (!code)
          throw new HallzeeError(
            "CLAIM_REQUIRED",
            "Open physical pairing mode and enter the code before choosing the terminal.",
          );
        this.setState("AwaitingClaim");
        const proof = await computeClaimProof(code, client, id, identity.nonce);
        check();
        const claim = (
          await this.exchange(
            `CLAIM,2,${client},${proof}`,
            (l) => l.startsWith("CLAIM_OK,"),
            local.signal,
          )
        ).split(",");
        check();
        if (claim.length !== 4 || claim[1] !== "2" || terminalId(claim[2]) !== id)
          throw new HallzeeError("IDENTITY_MISMATCH");
        const commitNonce = nonce(claim[3]);
        this.setState("SavingCredential");
        try {
          const key = await deriveOwnerKey(code, id, client);
          check();
          await this.credentials.savePending(id, client, key);
          check();
          saved = await this.credentials.get(id);
          check();
        } catch (error) {
          try {
            await this.port.sendLine(`CLAIM_ABORT,2,${client}`, local.signal);
          } catch {
            /* retain pending key for recovery */
          }
          throw error;
        }
        if (!saved) throw new HallzeeError("CREDENTIAL_STORAGE_FAILED");
        this.setState("AwaitingClaimCommit");
        const commitProof = await computeAuthProof(saved.key, id, client, commitNonce);
        check();
        const auth = await this.exchange(
          `CLAIM_COMMIT,2,${client},${commitProof}`,
          (l) => l.startsWith("AUTH_OK,"),
          local.signal,
        );
        check();
        return await this.finishAuth(auth, id, client, device, workspaceId, check);
      }
      const proof = await computeAuthProof(saved.key, id, client, identity.nonce);
      check();
      const auth = await this.exchange(
        `AUTH,2,${client},${proof}`,
        (l) => l.startsWith("AUTH_OK,"),
        local.signal,
      );
      check();
      return await this.finishAuth(auth, id, client, device, workspaceId, check);
    } catch (error) {
      if (generation === this.generation) {
        this.disconnect();
        this.setState("Failed");
      }
      if (timedOut)
        throw new HallzeeError(
          "HANDSHAKE_TIMEOUT",
          "The connection timed out. Reconnect to start a fresh authentication.",
          true,
        );
      throw error;
    } finally {
      clearTimeout(timer);
      signal?.removeEventListener("abort", cancel);
      code = undefined;
    }
  }
  private async finishAuth(
    line: string,
    id: string,
    _client: string,
    device: DeviceHandle,
    workspaceId: string,
    check: () => void,
  ) {
    const f = line.split(",");
    if (f.length !== 4 || f[1] !== "2" || terminalId(f[2]) !== id)
      throw new HallzeeError("IDENTITY_MISMATCH");
    const terminal = {
      terminalId: id,
      customName: terminalName(f[3]),
      protocolVersion: 2 as const,
      deviceIdHint: device.id,
      assignedWorkspaceId: workspaceId,
      maxIdLength: 10,
    };
    await this.credentials.confirm(terminal);
    check();
    this.authenticatedId = id;
    this.setState("Authenticated");
    return terminal;
  }
  send(command: string, signal?: AbortSignal) {
    if (this.state !== "Authenticated") return Promise.reject(new HallzeeError("AUTH_REQUIRED"));
    return this.port.sendLine(command, signal);
  }
  request(command: string, match: (line: string) => boolean, signal?: AbortSignal) {
    if (this.state !== "Authenticated") return Promise.reject(new HallzeeError("AUTH_REQUIRED"));
    return this.exchange(command, match, signal);
  }
  disconnect() {
    this.generation++;
    this.pending?.reject(new HallzeeError("DISCONNECTED", "Reconnect to the terminal.", true));
    this.pending = undefined;
    this.authenticatedId = undefined;
    this.port.disconnect();
    this.setState("Disconnected");
  }
  dispose() {
    this.disconnect();
    for (const dispose of this.disposers) dispose();
    this.application.clear();
    this.changed.clear();
    this.dropped.clear();
  }
}
