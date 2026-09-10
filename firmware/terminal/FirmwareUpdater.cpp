#ifdef ARDUINO
#include "FirmwareUpdater.h"
#include "FirmwareRelease.h"
#include <mbedtls/pk.h>
#include <esp_app_desc.h>
#include <esp_system.h>
#include <cstring>
#if !defined(CONFIG_BOOTLOADER_APP_ROLLBACK_ENABLE) || !defined(CONFIG_APP_ROLLBACK_ENABLE)
#error "Hallzee OTA requires the pinned rollback-enabled ESP32 core"
#endif

extern "C" bool verifyRollbackLater() { return true; }

namespace {
constexpr uint32_t SLOT_SIZE = 0x180000;
#define HZ_STRING_INNER(x) #x
#define HZ_STRING(x) HZ_STRING_INNER(x)
#ifdef HALLZEE_ILI9341
#define HZ_VARIANT "esp32-ili9341-r" HZ_STRING(HALLZEE_DISPLAY_ROTATION)
#else
#define HZ_VARIANT "esp32-st7735-r1"
#endif
const char firmwareMarker[] = "HZFW1|" HALLZEE_FW_VERSION "|" HALLZEE_FW_BUILD "|" HZ_VARIANT "|ota-v1";
String variant() {
#ifdef HALLZEE_ILI9341
  return String("esp32-ili9341-r") + String(HALLZEE_DISPLAY_ROTATION);
#else
  return "esp32-st7735-r1";
#endif
}
bool layoutReady() {
  const auto *a = esp_partition_find_first(ESP_PARTITION_TYPE_APP, ESP_PARTITION_SUBTYPE_APP_OTA_0, nullptr);
  const auto *b = esp_partition_find_first(ESP_PARTITION_TYPE_APP, ESP_PARTITION_SUBTYPE_APP_OTA_1, nullptr);
  return a && b && a->address == 0x10000 && b->address == 0x190000 && a->size == SLOT_SIZE && b->size == SLOT_SIZE;
}
String field(const String &s, int index) {
  int start = 0;
  for (int i = 0; i < index; i++) { start = s.indexOf('|', start); if (start < 0) return ""; start++; }
  int end = s.indexOf('|', start);
  return s.substring(start, end < 0 ? s.length() : end);
}
bool unhex(const String &s, uint8_t *out, size_t count) {
  if (s.length() != count * 2) return false;
  for (size_t i = 0; i < count; i++) {
    char pair[] = {s[i * 2], s[i * 2 + 1], 0}; char *end;
    if (!isxdigit(pair[0]) || !isxdigit(pair[1])) return false;
    out[i] = strtoul(pair, &end, 16);
  }
  return true;
}
String hex(const uint8_t *data, size_t size) {
  String result; result.reserve(size * 2);
  for (size_t i = 0; i < size; i++) { char pair[3]; snprintf(pair, sizeof(pair), "%02x", data[i]); result += pair; }
  return result;
}
uint32_t u32(const uint8_t *p) { return uint32_t(p[0]) | uint32_t(p[1]) << 8 | uint32_t(p[2]) << 16 | uint32_t(p[3]) << 24; }
bool version(const String &s, uint32_t &value) {
  unsigned a,b,c; int n = 0;
  if (sscanf(s.c_str(), "%u.%u.%u%n", &a, &b, &c, &n) != 3 || n != int(s.length()) || a > 999 || b > 999 || c > 999) return false;
  value = a * 1000000 + b * 1000 + c; return true;
}
}
FirmwareUpdater::FirmwareUpdater(BluetoothSerialPort &p, Allowed auth, Allowed free) : port(p), authorized(auth), idle(free) {
  mbedtls_sha256_init(&sha);
}
void FirmwareUpdater::confirmBoot(bool healthy) {
  bootChecked = true; bootHealthy = healthy;
  esp_ota_img_states_t state;
  if (esp_ota_get_state_partition(esp_ota_get_running_partition(), &state) == ESP_OK && state == ESP_OTA_IMG_PENDING_VERIFY) {
    if (healthy) bootHealthy = esp_ota_mark_app_valid_cancel_rollback() == ESP_OK;
    if (!bootHealthy) esp_ota_mark_app_invalid_rollback_and_reboot();
  }
}
void FirmwareUpdater::info() {
  Serial.println(firmwareMarker);
  esp_ota_img_states_t state;
  bool pending = esp_ota_get_state_partition(esp_ota_get_running_partition(), &state) == ESP_OK && state == ESP_OTA_IMG_PENDING_VERIFY;
  const char *boot = !bootChecked || pending ? "PENDING" : bootHealthy ? "CONFIRMED" : "FAILED";
  port.println(String("FIRMWARE_INFO,1,") + HALLZEE_FW_VERSION + "," + HALLZEE_FW_BUILD + "," + variant() + "," +
    (layoutReady() ? (bootHealthy && !pending ? "ota-v1,1572864,1," : "ota-v1,1572864,0,") : "legacy,0,0,") + boot + ",1");
}
void FirmwareUpdater::abort() {
  if (handle) esp_ota_abort(handle);
  handle = 0; session = 0; written = 0; imageSize = 0; metadataSize = 0; receivingImage = false;
  metadata = ""; signature = ""; digest = ""; rebootAt = 0;
}
void FirmwareUpdater::error(const char *reason) { port.println(String("FW_ERROR,") + reason); }
bool FirmwareUpdater::command(const String &text) {
  if (!authorized()) return false;
  if (text == "GET_FIRMWARE_INFO") { info(); return true; }
  if (text.startsWith("FW_BEGIN,")) {
    if (busy()) { error("BUSY"); return true; }
    if (!idle()) { error("ACTIVE_PASS"); return true; }
    if (!layoutReady()) { error("USB_SETUP_REQUIRED"); return true; }
    if (!bootChecked || !bootHealthy) { error("NOT_READY"); return true; }
    int a=text.indexOf(',',9), b=text.indexOf(',',a+1);
    String id=text.substring(9,a), size=text.substring(a+1,b), sig=text.substring(b+1);
    uint8_t sid[4];
    if (a<0 || b<0 || !unhex(id,sid,4) || size.length()>4 || size.toInt()<1 || size.toInt()>2048 ||
        sig.length()<128 || sig.length()>144) { error("INVALID_BEGIN"); return true; }
    session=strtoul(id.c_str(),nullptr,16);
    if (!session) { error("INVALID_SESSION"); return true; }
    metadataSize=size.toInt(); signature=sig; metadata.reserve(metadataSize); lastActivity=millis();
    port.println(String("FW_READY,")+id); return true;
  }
  if (text.startsWith("FW_ABORT,") || text.startsWith("FW_COMMIT,")) {
    int comma=text.indexOf(',');
    uint32_t sid=strtoul(text.substring(comma+1).c_str(),nullptr,16);
    if (!session || sid!=session) { error("SESSION"); return true; }
    if (rebootAt) { error("COMMITTED"); return true; }
    if (text.startsWith("FW_ABORT,")) { abort(); port.println("FW_ABORTED"); return true; }
    if (!receivingImage || written!=imageSize) { error("INCOMPLETE"); return true; }
    uint8_t receivedHash[32], expectedHash[32]; mbedtls_sha256_finish(&sha,receivedHash);
    if (!unhex(digest,expectedHash,32) || memcmp(receivedHash,expectedHash,32)) { abort(); error("HASH"); return true; }
    esp_err_t result=esp_ota_end(handle); handle=0;
    if (result!=ESP_OK || esp_ota_set_boot_partition(partition)!=ESP_OK) { abort(); error("IMAGE"); return true; }
    port.println("FW_COMMITTED"); rebootAt=millis(); if (!rebootAt) rebootAt=1;
    return true;
  }
  if (busy()) { error("BUSY"); return true; }
  return false;
}
bool FirmwareUpdater::verifyMetadata() {
  uint8_t hash[32], sig[72];
  if (!unhex(signature,sig,signature.length()/2)) return false;
  mbedtls_sha256(reinterpret_cast<const uint8_t *>(metadata.c_str()),metadata.length(),hash,0);
  mbedtls_pk_context key; mbedtls_pk_init(&key);
  int result=mbedtls_pk_parse_public_key(&key,reinterpret_cast<const uint8_t *>(HALLZEE_FW_PUBLIC_KEY),sizeof(HALLZEE_FW_PUBLIC_KEY));
  if (!result) result=mbedtls_pk_verify(&key,MBEDTLS_MD_SHA256,hash,32,sig,signature.length()/2);
  mbedtls_pk_free(&key); if (result) return false;
  int newline=metadata.indexOf('\n'); if (newline<0) return false;
  String header=metadata.substring(0,newline);
  uint32_t target,current,minClient;
  if (field(header,0)!="HALLZEE-FW" || field(header,1)!="1" || !version(field(header,2),target) ||
      !version(HALLZEE_FW_VERSION,current) || target<=current || !version(field(header,4),minClient) ||
      field(header,5)!="1" || field(header,6)!="1" || field(header,8)!=HALLZEE_FW_KEY_ID) return false;
  int matches=0, start=newline+1;
  while (start<int(metadata.length())) {
    int end=metadata.indexOf('\n',start); if (end<0) return false;
    String line=metadata.substring(start,end); start=end+1;
    if (field(line,0)!=variant()) continue;
    matches++;
    if (field(line,1)!="ota-v1") return false;
    imageSize=field(line,2).toInt(); digest=field(line,3);
  }
  uint8_t expected[32];
  if (matches!=1 || imageSize<256 || imageSize>SLOT_SIZE || !unhex(digest,expected,32)) return false;
  partition=esp_ota_get_next_update_partition(nullptr);
  if (!partition || partition->size<imageSize || esp_ota_begin(partition,imageSize,&handle)!=ESP_OK) return false;
  mbedtls_sha256_starts(&sha,0); receivingImage=true; return true;
}
void FirmwareUpdater::frame(const uint8_t *bytes,size_t size) {
  if (!authorized() || !busy() || rebootAt) { error("SESSION"); return; }
  if (size<15 || bytes[0]!=0 || bytes[1]!='H' || bytes[2]!='Z' || bytes[3]!=1 || u32(bytes+5)!=session) { error("FRAME"); return; }
  uint8_t kind=bytes[4]; uint32_t offset=u32(bytes+9); size_t count=bytes[13] | size_t(bytes[14])<<8;
  if (!count || count>512 || size!=count+15 || (kind!=1 && kind!=2)) { error("FRAME"); return; }
  uint32_t received=kind==1 ? metadata.length() : written;
  // A retransmitted acknowledged prefix is harmless. It is never written twice.
  if (offset<received && offset+count<=received) { }
  else if (offset!=received) { error("OFFSET"); return; }
  else if (kind==1 && !receivingImage && offset+count<=metadataSize) {
    for (size_t i=15;i<size;i++) { if (bytes[i]<10 || bytes[i]>126 || bytes[i]==13) { abort(); error("METADATA"); return; } metadata+=char(bytes[i]); }
    if (metadata.length()==metadataSize && !verifyMetadata()) { abort(); error("SIGNATURE_OR_COMPATIBILITY"); return; }
  } else if (kind==2 && receivingImage && offset+count<=imageSize) {
    if (esp_ota_write(handle,bytes+15,count)!=ESP_OK) { abort(); error("WRITE"); return; }
    mbedtls_sha256_update(&sha,bytes+15,count); written+=count;
  } else { error("STATE"); return; }
  lastActivity=millis(); char id[9]; snprintf(id,sizeof(id),"%08x",session);
  port.println(String("FW_ACK,")+id+","+String(kind)+","+String(kind==1 ? metadata.length() : written));
}
void FirmwareUpdater::poll() {
  if (rebootAt && millis()-rebootAt>=500) { ESP.restart(); return; }
  if (busy() && !rebootAt && (!port.hasClient() || !authorized() || millis()-lastActivity>30000)) abort();
}
#endif
