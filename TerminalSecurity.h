#pragma once

#include <Arduino.h>
#include <Preferences.h>

#include "TerminalIdentity.h"

class TerminalSecurity {
public:
  explicit TerminalSecurity(TerminalIdentity &identity);

  bool begin();
  bool hasOwner() const { return ownerClientId.length() > 0; }
  bool isAuthorized() const { return authorized; }
  const String &authorizedClientId() const { return sessionClientId; }

  bool startClaimMode(unsigned long now);
  void stopClaimMode();
  bool claimModeActive(unsigned long now) const;
  const String &claimKey() const { return pendingClaimKey; }
  uint32_t pairingPasskey() const { return pendingPasskey; }

  bool beginHandshake(String &nonce);
  bool acceptClaim(
    const String &clientId,
    const String &proof,
    const String &claimNonce,
    String &commitNonce
  );
  bool commitClaim(
    const String &clientId,
    const String &proof,
    const String &commitNonce
  );
  bool acceptAuth(
    const String &clientId,
    const String &proof,
    const String &authNonce
  );

  void clearSession();
  bool resetOwner();

private:
  static constexpr unsigned long CLAIM_WINDOW_MS = 120000;
  static constexpr uint8_t MAX_CLAIM_FAILURES = 3;
  static constexpr uint8_t OWNER_KEY_BYTES = 32;
  static constexpr uint8_t NONCE_BYTES = 16;
  static constexpr uint8_t CLAIM_KEY_CHARS = 16;

  TerminalIdentity &identity;
  Preferences preferences;
  String ownerClientId;
  uint8_t ownerKey[OWNER_KEY_BYTES] = {};
  String pendingClaimKey;
  uint32_t pendingPasskey = 0;
  String pendingClientId;
  uint8_t pendingOwnerKey[OWNER_KEY_BYTES] = {};
  String handshakeNonce;
  String commitNonce;
  String sessionClientId;
  unsigned long claimModeStarted = 0;
  uint8_t claimFailures = 0;
  bool claimMode = false;
  bool pendingClaim = false;
  bool authorized = false;

  static String randomHex(uint8_t byteCount);
  static String randomClaimKey();
  static String normalizeClientId(const String &clientId);
  static String normalizeClaimKey(const String &claimKey);
  static bool isValidHex(const String &value, size_t expectedLength);
  static bool constantTimeHexEquals(const String &expected, const String &actual);
  static bool computeHmacHex(
    const uint8_t *key,
    size_t keyLength,
    const String &message,
    String &output
  );
  static bool deriveOwnerKey(
    const String &claimKey,
    const String &terminalId,
    const String &clientId,
    uint8_t *output
  );
  static String claimMessage(
    const String &terminalId,
    const String &clientId,
    const String &nonce
  );
  static String authMessage(
    const String &terminalId,
    const String &clientId,
    const String &nonce
  );
  void clearPendingClaim();
  bool persistOwner(const String &clientId, const uint8_t *key);
};
