import { HallzeeError } from "../app/errors";
const encode = (text: string) => new TextEncoder().encode(text);
export function normalized(value: string, pattern: RegExp): string {
  const result = value.trim().toUpperCase();
  if (!pattern.test(result))
    throw new HallzeeError("INVALID_IDENTITY", "The terminal identity or code is invalid.");
  return result;
}
export const terminalId = (value: string) => normalized(value, /^HZ-[0-9A-F]{12}$/);
export const clientId = (value: string) =>
  normalized(value, /^[0-9A-F]{8}-(?:[0-9A-F]{4}-){3}[0-9A-F]{12}$/);
export const nonce = (value: string) => normalized(value, /^[0-9A-F]{32}$/);
export const passkey = (value: string) => normalized(value, /^[0-9]{6}$/);
export const hex = (bytes: ArrayBuffer) =>
  Array.from(new Uint8Array(bytes), (b) => b.toString(16).padStart(2, "0"))
    .join("")
    .toUpperCase();
export function terminalName(value: string) {
  const result = value.trim();
  if (!/^[\x20-\x2B\x2D-\x7E]{1,24}$/.test(result))
    throw new HallzeeError("INVALID_NAME", "Use 1–24 plain ASCII characters without commas.");
  return result;
}
export async function computeClaimProof(
  code: string,
  client: string,
  terminal: string,
  challenge: string,
) {
  const key = await crypto.subtle.importKey(
    "raw",
    encode(passkey(code)),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  return hex(
    await crypto.subtle.sign(
      "HMAC",
      key,
      encode(`CLAIM|2|${terminalId(terminal)}|${clientId(client)}|${nonce(challenge)}`),
    ),
  );
}
export async function deriveOwnerKey(code: string, terminal: string, client: string) {
  const key = await crypto.subtle.importKey("raw", encode(passkey(code)), "HKDF", false, [
    "deriveKey",
  ]);
  return crypto.subtle.deriveKey(
    {
      name: "HKDF",
      hash: "SHA-256",
      salt: encode(terminalId(terminal)),
      info: encode(`Hallzee owner v2|${clientId(client)}`),
    },
    key,
    { name: "HMAC", hash: "SHA-256", length: 256 },
    false,
    ["sign"],
  );
}
export async function computeAuthProof(
  key: CryptoKey,
  terminal: string,
  client: string,
  challenge: string,
) {
  return hex(
    await crypto.subtle.sign(
      "HMAC",
      key,
      encode(`AUTH|2|${terminalId(terminal)}|${clientId(client)}|${nonce(challenge)}`),
    ),
  );
}
