# Physical Instagram Follower Counter - Firmware Architecture & Hardware Selection Guide

**Version:** 1.0.0  
**Date:** September 14, 2026  
**Target Platform:** ESP32 Microcontroller Family (Arduino Framework / ESP-IDF)  
**Backend Compatibility:** ASP.NET Core 9 `/device/v1/*` Endpoints  

---

## 1. Executive Summary & Engineering Objectives

The physical Instagram follower counter is a commercial IoT product that displays real-time follower counts from a connected Meta Instagram Creator or Business profile. The physical hardware communicates with the ASP.NET Core backend over secure HTTPS.

### Key Hardware Requirements:
1. **Mechanical Longevity**: Mechanical stepper motors and split-flap gears must **not** turn continuously or when the follower count hasn't changed.
2. **Outage Resilience (`isStale`)**: If Meta rate-limits or temporary Internet connectivity drops occur, the physical display must retain its last known verified count rather than zeroing out or endlessly spinning.
3. **Zero-Touch Consumer WiFi Provisioning**: Non-technical customers must be able to connect the counter to their 2.4GHz home/office Wi-Fi via a smartphone captive portal in under 60 seconds without software installation.
4. **Tamper-Resistant Security**: Factory secrets (`DEVICE_SECRET`) must be isolated in non-volatile flash partitions; device credentials must never expose customer Instagram tokens or Meta credentials.
5. **Night / Sleep Mode**: Optional scheduled quiet hours to silence stepper motor movements in bedrooms or offices.

---

## 2. Microcontroller Hardware Evaluation

| Microcontroller | Core Architecture | Wi-Fi / BT | Flash / SRAM | GPIO Count | Hardware Crypto (TLS) | Estimated Unit Cost | Verdict |
|---|---|---|---|---|---|---|---|
| **ESP32-WROOM-32D / ESP32-S3** | Dual-Core Xtensa 32-bit (240 MHz) | Wi-Fi 802.11 b/g/n + BT 4.2 / 5.0 | 4MB–8MB Flash / 520KB SRAM | 34 / 45 GPIOs | Hardware AES-256, SHA-256, RSA acceleration | \$2.50 – \$3.50 | **RECOMMENDED PRIMARY CHOICE**: Unmatched community support, dedicated hardware cryptographic acceleration for fast TLS 1.3 handshakes, and abundant GPIOs for multi-digit stepper drivers or shift registers. |
| **ESP32-C3 (RISC-V)** | Single-Core RISC-V 32-bit (160 MHz) | Wi-Fi 802.11 b/g/n + BLE 5.0 | 4MB Flash / 400KB SRAM | 15–22 GPIOs | Hardware AES-128/256, SHA-256 | \$1.40 – \$1.80 | **EXCELLENT BUDGET OPTION**: Ideal for cost-optimized high-volume runs or 7-segment/e-Paper variants where fewer motor driver pins are required. |
| **Raspberry Pi Pico W** | Dual-Core ARM Cortex-M0+ (133 MHz) | CYW43439 Wi-Fi (2.4GHz) + BT 5.2 | 2MB Flash / 264KB SRAM | 26 GPIOs | Software crypto (mbedTLS) | \$6.00 | **NOT RECOMMENDED**: Higher unit cost and lack of dedicated hardware cryptographic acceleration leads to high latency and RAM pressure during TLS handshakes. |

### Final Recommendation:
- **ESP32-WROOM-32D** (or **ESP32-S3-WROOM-1**) is selected as the production baseline. Its dual cores allow Core 0 to manage Wi-Fi, TLS handshakes, and JSON parsing while Core 1 executes smooth stepper motor acceleration curves without jitter.

---

## 3. Display & Actuator Mechanisms

Three product tier options are supported by the backend state contract:

```
                               +-----------------------------+
                               |     ESP32 Microcontroller   |
                               +--------------+--------------+
                                              |
                     +------------------------+------------------------+
                     |                                                 |
                     v                                                 v
    +---------------------------------+               +---------------------------------+
    |   Tier 1: Mechanical Split-Flap |               |    Tier 2: Solid-State Display  |
    |   (Classic Flap Drum Aesthetic) |               |     (Budget / Silent Editions)  |
    +----------------+----------------+               +----------------+----------------+
                     |                                                 |
     +---------------+---------------+                 +---------------+---------------+
     |                               |                 |                               |
     v                               v                 v                               v
28BYJ-48 5V Stepper         NEMA 17 Stepper       MAX7219 8-Digit        Waveshare e-Paper
+ ULN2003 Driver            + TMC2209 Silent      7-Segment LED          (Ultra Low Power,
(Affordable / Tactile)      (Ultra Quiet Luxury)  (Instant / Bright)     Direct Sunlight)
```

### Option A: Mechanical Split-Flap Drum (Standard Commercial Tier)
- **Actuators**: 5x to 7x **28BYJ-48 5V Stepper Motors** with **ULN2003 Darlington Driver Arrays** (or A4988 / TMC2209 for silent stepping).
- **Flap Wheels**: 40 flaps per digit drum (`0`–`9` repeated 4 times, or `0`–`9` plus special symbols like `K`, `M`, ` `, `@`).
- **Zero-Position Homing**:
  - Each drum requires a homing index sensor to establish absolute position on power-up.
  - **Sensor Choice**: Slotted optical interrupter (e.g. **ITR9608** / **EE-SX670**) or Miniature Hall-effect sensor (**A3144**) with a tiny neodymium magnet embedded in the reel.
  - **Homing Routine**: On boot, rotate stepper until home sensor triggers -> set current position = `0`.
- **Directional Constraint**: Mechanical flaps can **only rotate forward**; reverse rotation jams or destroys flaps. The firmware step calculation must always step forward modulo the drum capacity.

### Option B: 7-Segment LED Display (Budget Commercial Tier)
- **Display**: 8-digit 0.56" Red / Amber / Green 7-Segment module driven by **MAX7219** via SPI (3 pins: DIN, CS, CLK).
- **Advantages**: Under \$10 BOM cost, zero mechanical wear, instant numerical updates, visible in complete darkness.

### Option C: E-Paper / E-Ink Display (Ultra-Low-Power Tier)
- **Display**: Waveshare 4.2" or 7.5" Black/White/Red E-Paper module via SPI.
- **Advantages**: Zero power consumption between refreshes, exceptional outdoor/sunlight visibility, elegant paper-like typography.

---

## 4. Consumer Wi-Fi Onboarding Flow (Captive Portal)

To provide an Apple/Nest-like consumer unboxing experience, the firmware implements the **WiFiManager** captive portal pattern:

```
[Power On Device]
       |
       v
[Attempt Connect to Saved Wi-Fi (NVS)]
       |
       +---> [Success] ---> Run Normal Polling Loop
       |
       +---> [Failed / No Saved Credentials / Setup Button Held 5s]
                 |
                 v
             [Start Soft-AP: "IG-Counter-Setup-XXXX"]
             [Start Captive DNS Server (192.168.4.1)]
                 |
                 v
             [Customer connects phone to "IG-Counter-Setup-XXXX"]
             [Browser opens automatically to Setup Portal]
                 |
                 v
             [Customer selects Wi-Fi SSID & enters Password]
                 |
                 v
             [Save Wi-Fi to NVS -> Reboot ESP32 -> Connect to Backend]
```

---

## 5. Device Authentication & Endpoint Contracts

### 1. State Polling Contract (`GET /device/v1/state`)
The counter polls this endpoint every 30–60 seconds:

```http
GET /device/v1/state HTTP/1.1
Host: api.counter.com
X-Device-Serial: FC-A82F32
Authorization: Device dev_device_secret_256bit_secure_token_99
Accept: application/json
```

*(Alternatively, query parameters `?serial=FC-A82F32&secret=...` are accepted for low-memory microcontrollers).*

#### Backend Response JSON:
```json
{
  "deviceId": "FC-A82F32",
  "configured": true,
  "instagramConnected": true,
  "username": "arun_naturals_official",
  "followers": 158,
  "sequence": 2,
  "isStale": false,
  "updatedAt": "2026-09-14T12:45:00Z",
  "serverTime": "2026-09-14T12:45:15Z",
  "pollAfterSeconds": 30
}
```

### 2. Telemetry Heartbeat Contract (`POST /device/v1/heartbeat`)
Every 5–15 minutes, the counter sends health diagnostics to the backend:

```http
POST /device/v1/heartbeat HTTP/1.1
Host: api.counter.com
X-Device-Serial: FC-A82F32
Authorization: Device dev_device_secret_256bit_secure_token_99
Content-Type: application/json

{
  "firmwareVersion": "1.0.0",
  "uptimeSeconds": 86420,
  "wifiRssi": -62,
  "freeHeap": 142800,
  "lastSequence": 2,
  "status": "OK"
}
```

---

## 6. The Mechanical Guard State Machine

The firmware state machine ensures maximum mechanical durability and fail-safe handling:

```
                       +---------------------------------------+
                       |             Device Boot               |
                       | - Load credentials & lastSequence NVS |
                       | - Zero-home mechanical reels          |
                       +-------------------+-------------------+
                                           |
                                           v
                       +---------------------------------------+
                       |           Connect to Wi-Fi            |
                       +-------------------+-------------------+
                                           |
                                           v
                       +---------------------------------------+
                       |      Poll GET /device/v1/state        |
                       +-------------------+-------------------+
                                           |
                    +----------------------+----------------------+
                    |                                             |
                    v (configured == false)                       v (configured == true)
    +---------------------------------+           +---------------------------------+
    |     Display Pairing Screen      |           |   instagramConnected == false?  |
    | - Display dashes: "------"      |           +---------------+-----------------+
    | - Display serial on OLED / LCD  |                           |
    +---------------------------------+           +---------------+---------------+
                                                  |                               |
                                                  v (false)                       v (true)
                                  +-------------------------------+   +-------------------------------+
                                  |     Display "CONNECT IG"      |   |   sequence > lastSequence?    |
                                  | - Flaps show "CONN IG"        |   +---------------+---------------+
                                  +-------------------------------+                   |
                                                  +-----------------------------------+-----------------------------------+
                                                  |                                                                       |
                                                  v (YES: Count Changed)                                                  v (NO: Same Count)
                                  +-----------------------------------------------+                       +-------------------------------+
                                  |  1. Calculate forward step delta              |                       |  Skip Stepper Motors!         |
                                  |  2. Execute mechanical rotation to new total  |                       |  (Saves motor life, power,    |
                                  |  3. Persist lastSequence = sequence in NVS    |                       |   and eliminates noise)       |
                                  +-----------------------+-----------------------+                       +---------------+---------------+
                                                          |                                                               |
                                                          +-----------------------+---------------------------------------+
                                                                                  |
                                                                                  v
                                                                  +-------------------------------+
                                                                  |         Check isStale         |
                                                                  |  true  -> Light Amber LED     |
                                                                  |  false -> Light Green / Off   |
                                                                  |  (Never zero out display!)    |
                                                                  +---------------+---------------+
                                                                                  |
                                                                                  v
                                                                  +-------------------------------+
                                                                  |  Sleep pollAfterSeconds (30s) |
                                                                  +-------------------------------+
```

---

## 7. Reference C++ ESP32 Polling Implementation

This production-grade Arduino C++ implementation demonstrates HTTPS state polling, sequence comparison, NVS non-volatile persistence, and fail-safe error handling:

```cpp
#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <HTTPClient.h>
#include <ArduinoJson.h>
#include <Preferences.h>

// ============================================================================
// Factory Device Credentials (Flashed during manufacturing provisioning)
// ============================================================================
const char* DEVICE_SERIAL = "FC-A82F32";
const char* DEVICE_SECRET = "dev_device_secret_256bit_secure_token_99";
const char* BACKEND_BASE_URL = "https://api.counter.com";

// Status LED Pin Definitions
const int PIN_LED_GREEN = 2;  // Online & synchronized
const int PIN_LED_AMBER = 4;  // Upstream stale / warning

// Persistent Storage (ESP32 Non-Volatile Storage)
Preferences preferences;

// Runtime Device State
int lastSeenSequence = 0;
long currentFollowerCount = 0;
unsigned long lastPollTime = 0;
int pollIntervalSeconds = 30;

void setup() {
  Serial.begin(115200);
  pinMode(PIN_LED_GREEN, OUTPUT);
  pinMode(PIN_LED_AMBER, OUTPUT);

  // 1. Initialize NVS and load previous sequence state
  preferences.begin("ig-counter", false);
  lastSeenSequence = preferences.getInt("last_seq", 0);
  currentFollowerCount = preferences.getLong("last_count", 0);
  Serial.printf("[SETUP] Loaded previous sequence: %d, count: %ld\n", 
                lastSeenSequence, currentFollowerCount);

  // 2. Perform zero-homing calibration on mechanical split-flap reels
  homeMechanicalReels();

  // 3. Connect to Wi-Fi (In production, replace with WiFiManager captive portal)
  connectWiFi();
}

void loop() {
  unsigned long now = millis();
  if (now - lastPollTime >= (pollIntervalSeconds * 1000UL) || lastPollTime == 0) {
    lastPollTime = now;
    pollCounterState();
  }
  delay(100);
}

void pollCounterState() {
  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("[WIFI] Disconnected. Attempting reconnect...");
    digitalWrite(PIN_LED_AMBER, HIGH);
    return;
  }

  WiFiClientSecure client;
  client.setInsecure(); // In production, provide root CA cert via client.setCACert(root_ca)
  HTTPClient http;

  String url = String(BACKEND_BASE_URL) + "/device/v1/state";
  http.begin(client, url);

  // Set Production Device Authentication Headers
  http.addHeader("X-Device-Serial", DEVICE_SERIAL);
  http.addHeader("Authorization", String("Device ") + DEVICE_SECRET);
  http.addHeader("Accept", "application/json");

  int httpCode = http.GET();
  if (httpCode == HTTP_CODE_OK) {
    String payload = http.getString();
    
    // Parse JSON Response
    StaticJsonDocument<512> doc;
    DeserializationError error = deserializeJson(doc, payload);

    if (!error) {
      bool configured = doc["configured"] | false;
      bool instagramConnected = doc["instagramConnected"] | false;
      long followers = doc["followers"] | 0;
      int sequence = doc["sequence"] | 0;
      bool isStale = doc["isStale"] | false;
      pollIntervalSeconds = doc["pollAfterSeconds"] | 30;

      Serial.printf("[STATE] Followers: %ld, Sequence: %d, Configured: %d, Connected: %d, Stale: %d\n",
                    followers, sequence, configured, instagramConnected, isStale);

      // Handle Stale Outage Indicator
      digitalWrite(PIN_LED_AMBER, isStale ? HIGH : LOW);
      digitalWrite(PIN_LED_GREEN, isStale ? LOW : HIGH);

      // State Machine Branching
      if (!configured) {
        displayPairingMode();
      } else if (!instagramConnected) {
        displayConnectInstagramMode();
      } else {
        // Guard: Rotate mechanical reels ONLY if sequence has incremented
        if (sequence > lastSeenSequence) {
          Serial.printf("[STEPPER] Count changed! Moving reels from %ld to %ld (Sequence %d -> %d)\n",
                        currentFollowerCount, followers, lastSeenSequence, sequence);

          rotateReelsToCount(followers);

          // Update NVS state
          lastSeenSequence = sequence;
          currentFollowerCount = followers;
          preferences.putInt("last_seq", lastSeenSequence);
          preferences.putLong("last_count", currentFollowerCount);
        } else {
          Serial.println("[STEPPER] Sequence unchanged. Stepper motors idle.");
        }
      }
    } else {
      Serial.printf("[JSON] Deserialization error: %s\n", error.c_str());
    }
  } else {
    Serial.printf("[HTTP] GET /device/v1/state failed with code: %d\n", httpCode);
    digitalWrite(PIN_LED_AMBER, HIGH);
  }

  http.end();
}

void homeMechanicalReels() {
  Serial.println("[HOMING] Rotating reels to zero-sensor trigger...");
  // Stepper motor pulse loop until slotted optical / Hall sensors trigger LOW
  // Sets internal step counter = 0
}

void rotateReelsToCount(long targetFollowers) {
  // Calculates forward step count for each digit drum
  // Always steps forward to prevent mechanical flap jamming
}

void displayPairingMode() {
  Serial.println("[DISPLAY] Device unclaimed: Showing pairing prompt");
}

void displayConnectInstagramMode() {
  Serial.println("[DISPLAY] Instagram not bound: Showing 'CONNECT IG'");
}

void connectWiFi() {
  // Connects to saved Wi-Fi network
}
```

---

## 8. Hardware Bill of Materials (BOM) Estimate

### Standard Mechanical Split-Flap Edition (5 Digits):
| Part Description | Component Ref | Qty | Unit Cost (USD) | Total (USD) |
|---|---|---|---|---|
| Main Microcontroller Module | ESP32-WROOM-32D (4MB Flash) | 1 | \$2.60 | \$2.60 |
| Geared Stepper Motors (5V) | 28BYJ-48 (1:64 reduction) | 5 | \$1.20 | \$6.00 |
| Stepper Driver ICs | ULN2003A (or Darlington Arrays) | 5 | \$0.30 | \$1.50 |
| Shift Register (Pin Extender) | 74HC595 (SPI to 8 outputs) | 3 | \$0.20 | \$0.60 |
| Optical Homing Sensors | ITR9608 Slotted Interrupter | 5 | \$0.25 | \$1.25 |
| Split-Flap Drum Cards | Matte PVC 0.3mm (Digit Imprinted) | 200 | \$0.02 | \$4.00 |
| Injection Molded Chassis / Spindle | ABS Enclosure & Reel Wheels | 1 set | \$5.50 | \$5.50 |
| Power Supply Circuit | 5V 2.5A USB-C DC Adapter + TPS54331 Step-down | 1 | \$2.80 | \$2.80 |
| Status LEDs & Tactile Buttons | Green, Amber LEDs + Reset Button | 1 set | \$0.30 | \$0.30 |
| **Total Estimated BOM (5 Digits)** | | | | **~\$24.55** |

---

## 9. Next Steps for Hardware Development

1. **Hardware Prototype Breadboarding**: Assemble 1 drum unit with 1x 28BYJ-48 stepper and 1x ITR9608 optical homing sensor on an ESP32 dev board.
2. **Flash ESP32 with Provisioned Credentials**: Use `FC-A82F32` and `dev_device_secret_256bit_secure_token_99` to verify end-to-end polling against `https://localhost:7149/device/v1/state`.
3. **Calibrate Flap Step Timing**: Determine ideal microstep delay (e.g. 2ms per step) for smooth, quiet flap drops without skipping steps.
4. **CAD Mechanical Enclosure**: Design 3D-printable or CNC chassis with acrylic front window.
