import type { Page } from "@playwright/test";
/** Simulates GATT only. The production transport, session, crypto and storage run unchanged. */
export async function installBluetooth(page: Page) {
  await page.addInitScript(() => {
    const root = window as any;
    root.terminalCommands = [];
    root.fakePasses = [];
    const encoder = new TextEncoder(),
      terminal = "HZ-A1B2C3D4E5F6",
      nonce = "00112233445566778899AABBCCDDEEFF";
    let buffer = "",
      client = "",
      key: CryptoKey;
    class Characteristic extends EventTarget {
      properties = { write: true, read: true };
      async readValue() {
        if (root.simulatedPairingFailure)
          throw new DOMException(
            "synthetic private diagnostic payload",
            root.simulatedPairingFailure === "unsupported" ? "NotSupportedError" : "NetworkError",
          );
        return new DataView(new ArrayBuffer(0));
      }
      value?: DataView;
      async startNotifications() {
        root.notificationsStarted = true;
        return this;
      }
      async writeValueWithResponse(bytes: Uint8Array) {
        if (bytes.byteLength > 20) throw Error("Chunk exceeds 20 bytes");
        buffer += new TextDecoder().decode(bytes);
        if (buffer.includes("\n")) {
          const line = buffer.slice(0, buffer.indexOf("\n"));
          buffer = buffer.slice(buffer.indexOf("\n") + 1);
          root.terminalCommands.push(line);
          if (!root.notificationsStarted) throw Error("HELLO before subscribe");
          await respond(line);
        }
      }
    }
    const tx = new Characteristic(),
      rx = new Characteristic();
    function emit(line: string) {
      const bytes = encoder.encode(line + "\r\n");
      for (let i = 0; i < bytes.length; i += 17) {
        tx.value = new DataView(bytes.slice(i, i + 17).buffer);
        tx.dispatchEvent(new Event("characteristicvaluechanged"));
      }
    }
    async function sign(k: CryptoKey, text: string) {
      return Array.from(
        new Uint8Array(await crypto.subtle.sign("HMAC", k, encoder.encode(text))),
        (n) => n.toString(16).padStart(2, "0"),
      )
        .join("")
        .toUpperCase();
    }
    async function respond(line: string) {
      const p = line.split(",");
      switch (p[0]) {
        case "HELLO":
          client = p[2];
          {
            const material = await crypto.subtle.importKey(
              "raw",
              encoder.encode("807481"),
              "HKDF",
              false,
              ["deriveKey"],
            );
            key = await crypto.subtle.deriveKey(
              {
                name: "HKDF",
                hash: "SHA-256",
                salt: encoder.encode(terminal),
                info: encoder.encode("Hallzee owner v2|" + client),
              },
              material,
              { name: "HMAC", hash: "SHA-256", length: 256 },
              false,
              ["sign"],
            );
          }
          emit(
            `IDENTITY,2,${terminal},E5F6,${sessionStorage.getItem("simClaimed") ? "CLAIMED" : "UNCLAIMED"},AVAILABLE,${nonce}`,
          );
          break;
        case "CLAIM": {
          const pass = await crypto.subtle.importKey(
            "raw",
            encoder.encode("807481"),
            { name: "HMAC", hash: "SHA-256" },
            false,
            ["sign"],
          );
          if (p[3] !== (await sign(pass, `CLAIM|2|${terminal}|${client}|${nonce}`)))
            throw Error("Claim proof mismatch");
          emit(`CLAIM_OK,2,${terminal},${nonce}`);
          break;
        }
        case "CLAIM_COMMIT":
        case "AUTH":
          if (p[3] !== (await sign(key, `AUTH|2|${terminal}|${client}|${nonce}`)))
            throw Error("Auth proof mismatch");
          sessionStorage.setItem("simClaimed", "1");
          emit(`AUTH_OK,2,${terminal},Test terminal`);
          break;
        case "GET_SETTINGS":
          emit("SETTINGS,MAX_ID_LENGTH,10");
          break;
        case "GET_ACTIVE_PASSES":
          emit(
            "ACTIVE_PASSES" +
              root.fakePasses.map((p: any) => "," + p.studentId + "," + p.epoch).join(""),
          );
          break;
        case "MANUAL_CHECKIN":
          root.fakePasses = root.fakePasses.filter((pass: any) => pass.studentId !== p[1]);
          emit(`EVENT,CHECKIN,${p[1]},300`);
          break;
        case "RELEASE_OWNER":
          sessionStorage.removeItem("simClaimed");
          emit("OWNER_RELEASED");
          break;
        case "TIME_CURSOR":
          emit("TIME_ACK,OK");
          if (Number(p[3]) < 1) {
            emit("SYNC_BEGIN,1");
            emit("TRIP,1,00123,2026-09-11,09:00:00,09:05:00,300,COMPLETE,0");
          } else {
            emit("SYNC_BEGIN,0");
            emit("SYNC_END");
          }
          break;
        case "ACK": {
          const db = await new Promise<IDBDatabase>((resolve) => {
            const r = indexedDB.open("hallzee-web");
            r.onsuccess = () => resolve(r.result);
          });
          const row = await new Promise((resolve) => {
            const r = db
              .transaction("trips")
              .objectStore("trips")
              .get([terminal, Number(p[1])]);
            r.onsuccess = () => resolve(r.result);
          });
          db.close();
          if (!row) throw Error("ACK before durable row");
          emit("SYNC_END");
          break;
        }
      }
    }
    class Device extends EventTarget {
      id = "simulated-gatt";
      name = "Hallzee-E5F6";
      gatt = {
        connected: false,
        connect: async () => {
          this.gatt.connected = true;
          return this.gatt;
        },
        disconnect: () => {
          this.gatt.connected = false;
        },
        getPrimaryService: async () => ({
          getCharacteristic: async (uuid: string) => (uuid.startsWith("44a359f3") ? tx : rx),
        }),
      };
    }
    const device = new Device();
    root.fakeGattConnected = () => device.gatt.connected;
    Object.defineProperty(navigator, "bluetooth", {
      configurable: true,
      value: {
        requestDevice: async () => {
          sessionStorage.setItem("simPermission", "1");
          return device;
        },
        getDevices: async () => !root.simNoRememberedDevices && sessionStorage.getItem("simPermission") ? [device] : [],
      },
    });
  });
}
