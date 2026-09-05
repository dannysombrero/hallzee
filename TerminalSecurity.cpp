#include "TerminalSecurity.h"

#include <esp_system.h>
#include <mbedtls/md.h>

namespace {
constexpr char OWNER_NAMESPACE[] = "hallzee_owner";
constexpr char OWNER_CLIENT_KEY[] = "client_id";
constexpr char OWNER_KEY_KEY[] = "owner_key";
constexpr char OWNER_FILE_PATH[] = "/hallzee-owner.bin";
constexpr char OWNER_TEMP_PATH[] = "/hallzee-owner.tmp";
constexpr char OWNER_MAGIC[] = "HZOW";
constexpr uint8_t OWNER_FILE_VERSION = 1;
constexpr size_t OWNER_CLIENT_BYTES = 37;
constexpr size_t OWNER_FILE_BYTES = 4 + 1 + OWNER_CLIENT_BYTES + 32;
}

TerminalSecurity::TerminalSecurity(TerminalIdentity &identity)
  : identity(identity) {}

bool TerminalSecurity::begin() {
  if (!LittleFS.begin(false)) return false;

  // Prefer the new LittleFS record. Existing valid NVS records are migrated
  // once so upgrading does not require a new physical claim.
  if (loadOwnerFromLittleFS()) return true;

  if (preferences.begin(OWNER_NAMESPACE, false)) {
    ownerClientId = preferences.getString(OWNER_CLIENT_KEY, "");
    const size_t storedLength = preferences.getBytesLength(OWNER_KEY_KEY);
    if (ownerClientId.length() > 0 && storedLength == OWNER_KEY_BYTES &&
        preferences.getBytes(OWNER_KEY_KEY, ownerKey, OWNER_KEY_BYTES) == OWNER_KEY_BYTES) {
      if (migrateOwnerFromPreferences()) return true;
    }
  }

  ownerClientId = "";
  memset(ownerKey, 0, sizeof(ownerKey));
  return true;
}

bool TerminalSecurity::startClaimMode(unsigned long now) {
  if (hasOwner()) return false;
  pendingPasskey = 100000 + (esp_random() % 900000);
  claimModeStarted = now;
  claimFailures = 0;
  pendingClaim = false;
  memset(pendingOwnerKey, 0, sizeof(pendingOwnerKey));
  claimMode = true;
  return true;
}

void TerminalSecurity::stopClaimMode() {
  claimMode = false;
  claimModeStarted = 0;
  claimFailures = 0;
  pendingPasskey = 0;
  handshakeNonce = "";
  clearPendingClaim();
}

bool TerminalSecurity::claimModeActive(unsigned long now) const {
  return claimMode && !hasOwner() &&
         static_cast<unsigned long>(now - claimModeStarted) < CLAIM_WINDOW_MS;
}

bool TerminalSecurity::beginHandshake(String &nonce) {
  authorized = false;
  sessionClientId = "";
  clearPendingClaim();
  handshakeNonce = randomHex(NONCE_BYTES);
  nonce = handshakeNonce;
  return nonce.length() == NONCE_BYTES * 2;
}

bool TerminalSecurity::acceptClaim(
  const String &clientId,
  const String &proof,
  const String &claimNonce,
  String &nextCommitNonce
) {
  nextCommitNonce = "";
  if (hasOwner() || !claimModeActive(millis()) || pendingClaim ||
      claimNonce != handshakeNonce) {
    return false;
  }

  const String normalizedClientId = normalizeClientId(clientId);
  if (normalizedClientId.length() == 0 || !isValidHex(proof, 64)) return false;
  const String pairingPasskey = String(pendingPasskey);

  String expected;
  const String message = claimMessage(
    identity.terminalId(), normalizedClientId, claimNonce);
  if (!computeHmacHex(
        reinterpret_cast<const uint8_t *>(pairingPasskey.c_str()),
        pairingPasskey.length(),
        message,
        expected) ||
      !constantTimeHexEquals(expected, proof)) {
    claimFailures++;
    handshakeNonce = "";
    if (claimFailures >= MAX_CLAIM_FAILURES) stopClaimMode();
    return false;
  }

  if (!deriveOwnerKey(
        pairingPasskey, identity.terminalId(), normalizedClientId, pendingOwnerKey)) {
    return false;
  }

  pendingClientId = normalizedClientId;
  commitNonce = randomHex(NONCE_BYTES);
  handshakeNonce = "";
  pendingClaim = true;
  nextCommitNonce = commitNonce;
  return true;
}

bool TerminalSecurity::commitClaim(
  const String &clientId,
  const String &proof,
  const String &requestedCommitNonce
) {
  claimCommitFailure = "NONE";
  if (!pendingClaim) {
    claimCommitFailure = "STATE";
    clearPendingClaim();
    return false;
  }
  if (requestedCommitNonce != commitNonce) {
    claimCommitFailure = "NONCE";
    clearPendingClaim();
    return false;
  }
  if (normalizeClientId(clientId) != pendingClientId) {
    claimCommitFailure = "CLIENT";
    clearPendingClaim();
    return false;
  }
  if (!isValidHex(proof, 64)) {
    claimCommitFailure = "PROOF_FORMAT";
    clearPendingClaim();
    return false;
  }

  String expected;
  const String message = authMessage(
    identity.terminalId(), pendingClientId, requestedCommitNonce);
  if (!computeHmacHex(pendingOwnerKey, OWNER_KEY_BYTES, message, expected) ||
      !constantTimeHexEquals(expected, proof)) {
    claimCommitFailure = "PROOF";
    clearPendingClaim();
    return false;
  }

  if (!persistOwner(pendingClientId, pendingOwnerKey)) {
    clearPendingClaim();
    return false;
  }

  ownerClientId = pendingClientId;
  memcpy(ownerKey, pendingOwnerKey, OWNER_KEY_BYTES);
  sessionClientId = ownerClientId;
  authorized = true;
  stopClaimMode();
  return true;
}

bool TerminalSecurity::acceptAuth(
  const String &clientId,
  const String &proof,
  const String &authNonce
) {
  const String normalizedClientId = normalizeClientId(clientId);
  if (!hasOwner() || normalizedClientId != ownerClientId ||
      authNonce != handshakeNonce || !isValidHex(proof, 64)) {
    return false;
  }

  String expected;
  const String message = authMessage(
    identity.terminalId(), normalizedClientId, authNonce);
  if (!computeHmacHex(ownerKey, OWNER_KEY_BYTES, message, expected) ||
      !constantTimeHexEquals(expected, proof)) {
    handshakeNonce = "";
    return false;
  }

  handshakeNonce = "";
  sessionClientId = normalizedClientId;
  authorized = true;
  return true;
}

void TerminalSecurity::clearSession() {
  authorized = false;
  sessionClientId = "";
  handshakeNonce = "";
  clearPendingClaim();
}

bool TerminalSecurity::resetOwner() {
  bool removed = !LittleFS.exists(OWNER_FILE_PATH) || LittleFS.remove(OWNER_FILE_PATH);
  if (preferences.begin(OWNER_NAMESPACE, false)) {
    if (preferences.isKey(OWNER_CLIENT_KEY)) preferences.remove(OWNER_CLIENT_KEY);
    if (preferences.isKey(OWNER_KEY_KEY)) preferences.remove(OWNER_KEY_KEY);
  }
  if (!removed) return false;
  ownerClientId = "";
  memset(ownerKey, 0, sizeof(ownerKey));
  stopClaimMode();
  clearSession();
  return true;
}

String TerminalSecurity::randomHex(uint8_t byteCount) {
  uint8_t bytes[NONCE_BYTES] = {};
  if (byteCount > sizeof(bytes)) return "";
  esp_fill_random(bytes, byteCount);
  String output;
  output.reserve(byteCount * 2);
  for (uint8_t index = 0; index < byteCount; index++) {
    if (bytes[index] < 16) output += "0";
    output += String(bytes[index], HEX);
  }
  output.toUpperCase();
  return output;
}

String TerminalSecurity::normalizeClientId(const String &clientId) {
  String normalized = clientId;
  normalized.trim();
  normalized.toUpperCase();
  if (normalized.length() != 36) return "";
  for (unsigned int index = 0; index < normalized.length(); index++) {
    const char character = normalized.charAt(index);
    const bool hyphenPosition =
      index == 8 || index == 13 || index == 18 || index == 23;
    if (hyphenPosition) {
      if (character != '-') return "";
    } else if (!((character >= '0' && character <= '9') ||
                 (character >= 'A' && character <= 'F'))) {
      return "";
    }
  }
  return normalized;
}

String TerminalSecurity::normalizePairingPasskey(const String &passkey) {
  String normalized = passkey;
  normalized.trim();
  if (normalized.length() != 6) return "";
  for (unsigned int index = 0; index < normalized.length(); index++) {
    if (normalized.charAt(index) < '0' || normalized.charAt(index) > '9') return "";
  }
  return normalized;
}

bool TerminalSecurity::isValidHex(const String &value, size_t expectedLength) {
  if (value.length() != expectedLength) return false;
  for (unsigned int index = 0; index < value.length(); index++) {
    const char character = value.charAt(index);
    if (!((character >= '0' && character <= '9') ||
          (character >= 'a' && character <= 'f') ||
          (character >= 'A' && character <= 'F'))) {
      return false;
    }
  }
  return true;
}

bool TerminalSecurity::constantTimeHexEquals(
  const String &expected,
  const String &actual
) {
  if (!isValidHex(expected, 64) || !isValidHex(actual, 64)) return false;
  uint8_t difference = 0;
  for (unsigned int index = 0; index < 64; index++) {
    char expectedCharacter = expected.charAt(index);
    char actualCharacter = actual.charAt(index);
    if (expectedCharacter >= 'a' && expectedCharacter <= 'f') {
      expectedCharacter -= 'a' - 'A';
    }
    if (actualCharacter >= 'a' && actualCharacter <= 'f') {
      actualCharacter -= 'a' - 'A';
    }
    difference |= static_cast<uint8_t>(expectedCharacter ^ actualCharacter);
  }
  return difference == 0;
}

bool TerminalSecurity::computeHmacHex(
  const uint8_t *key,
  size_t keyLength,
  const String &message,
  String &output
) {
  const mbedtls_md_info_t *mdInfo = mbedtls_md_info_from_type(MBEDTLS_MD_SHA256);
  if (mdInfo == nullptr) return false;

  uint8_t digest[32] = {};
  if (mbedtls_md_hmac(
        mdInfo,
        key,
        keyLength,
        reinterpret_cast<const uint8_t *>(message.c_str()),
        message.length(),
        digest) != 0) {
    return false;
  }
  output = "";
  output.reserve(64);
  for (uint8_t index = 0; index < sizeof(digest); index++) {
    if (digest[index] < 16) output += "0";
    output += String(digest[index], HEX);
  }
  output.toUpperCase();
  return true;
}

bool TerminalSecurity::deriveOwnerKey(
  const String &pairingPasskey,
  const String &terminalId,
  const String &clientId,
  uint8_t *output
) {
  const String normalizedPasskey = normalizePairingPasskey(pairingPasskey);
  if (normalizedPasskey.length() == 0 || output == nullptr) return false;
  const mbedtls_md_info_t *mdInfo = mbedtls_md_info_from_type(MBEDTLS_MD_SHA256);
  if (mdInfo == nullptr) return false;

  const String info = String("Hallzee owner v2|") + clientId;
  // Keep this implementation structurally identical to the desktop HKDF:
  // RFC 5869-Extract with terminalId as salt, followed by RFC 5869-Expand.
  // Using the primitive HMAC operation here avoids platform-specific wrapper
  // differences between the ESP32 and .NET implementations.
  uint8_t pseudorandomKey[32] = {};
  if (mbedtls_md_hmac(
        mdInfo,
        reinterpret_cast<const uint8_t *>(terminalId.c_str()),
        terminalId.length(),
        reinterpret_cast<const uint8_t *>(normalizedPasskey.c_str()),
        normalizedPasskey.length(),
        pseudorandomKey) != 0) {
    return false;
  }

  uint8_t previous[32] = {};
  size_t previousLength = 0;
  size_t written = 0;
  uint8_t counter = 1;
  while (written < OWNER_KEY_BYTES) {
    uint8_t blockInput[32 + 64 + 1] = {};
    size_t blockInputLength = 0;
    memcpy(blockInput + blockInputLength, previous, previousLength);
    blockInputLength += previousLength;
    memcpy(blockInput + blockInputLength, info.c_str(), info.length());
    blockInputLength += info.length();
    blockInput[blockInputLength++] = counter++;

    uint8_t block[32] = {};
    if (mbedtls_md_hmac(
          mdInfo,
          pseudorandomKey,
          sizeof(pseudorandomKey),
          blockInput,
          blockInputLength,
          block) != 0) {
      return false;
    }
    memcpy(previous, block, sizeof(previous));
    previousLength = sizeof(previous);
    const size_t copyLength = min(
      sizeof(block), static_cast<size_t>(OWNER_KEY_BYTES) - written);
    memcpy(output + written, block, copyLength);
    written += copyLength;
  }
  return true;
}

String TerminalSecurity::claimMessage(
  const String &terminalId,
  const String &clientId,
  const String &nonce
) {
  return String("CLAIM|2|") + terminalId + "|" + clientId + "|" + nonce;
}

String TerminalSecurity::authMessage(
  const String &terminalId,
  const String &clientId,
  const String &nonce
) {
  return String("AUTH|2|") + terminalId + "|" + clientId + "|" + nonce;
}

void TerminalSecurity::clearPendingClaim() {
  pendingClaim = false;
  pendingClientId = "";
  commitNonce = "";
  memset(pendingOwnerKey, 0, sizeof(pendingOwnerKey));
}

bool TerminalSecurity::persistOwner(const String &clientId, const uint8_t *key) {
  if (clientId.length() == 0 || key == nullptr) return false;
  if (!writeOwnerToLittleFS(clientId, key)) {
    claimCommitFailure = "STORAGE_LITTLEFS";
    return false;
  }
  return true;
}

bool TerminalSecurity::loadOwnerFromLittleFS() {
  if (!LittleFS.exists(OWNER_FILE_PATH)) return false;
  File file = LittleFS.open(OWNER_FILE_PATH, FILE_READ);
  if (!file || file.size() != OWNER_FILE_BYTES) return false;

  uint8_t buffer[OWNER_FILE_BYTES] = {};
  if (file.read(buffer, sizeof(buffer)) != sizeof(buffer) ||
      memcmp(buffer, OWNER_MAGIC, 4) != 0 ||
      buffer[4] != OWNER_FILE_VERSION) return false;

  char clientBuffer[OWNER_CLIENT_BYTES] = {};
  memcpy(clientBuffer, buffer + 5, OWNER_CLIENT_BYTES);
  const String candidate(clientBuffer);
  const String normalized = normalizeClientId(candidate);
  if (normalized.length() == 0 || normalized != candidate) return false;

  ownerClientId = normalized;
  memcpy(ownerKey, buffer + 5 + OWNER_CLIENT_BYTES, OWNER_KEY_BYTES);
  return true;
}

bool TerminalSecurity::writeOwnerToLittleFS(const String &clientId, const uint8_t *key) {
  uint8_t buffer[OWNER_FILE_BYTES] = {};
  memcpy(buffer, OWNER_MAGIC, 4);
  buffer[4] = OWNER_FILE_VERSION;
  memcpy(buffer + 5, clientId.c_str(), min(clientId.length(), OWNER_CLIENT_BYTES - 1));
  memcpy(buffer + 5 + OWNER_CLIENT_BYTES, key, OWNER_KEY_BYTES);

  LittleFS.remove(OWNER_TEMP_PATH);
  File temp = LittleFS.open(OWNER_TEMP_PATH, FILE_WRITE);
  if (!temp || temp.write(buffer, sizeof(buffer)) != sizeof(buffer)) return false;
  temp.flush();
  temp.close();

  LittleFS.remove(OWNER_FILE_PATH);
  if (!LittleFS.rename(OWNER_TEMP_PATH, OWNER_FILE_PATH)) {
    LittleFS.remove(OWNER_TEMP_PATH);
    return false;
  }
  return loadOwnerFromLittleFS();
}

bool TerminalSecurity::migrateOwnerFromPreferences() {
  if (!hasOwner()) return false;
  return writeOwnerToLittleFS(ownerClientId, ownerKey);
}
