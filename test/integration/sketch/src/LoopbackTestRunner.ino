// LoopbackTestRunner: Arduino sketch for Layer 3b hardware-in-the-loop testing.
// Implements the same command set as test/integration/firmware/src/main.cpp,
// so the same C# scenarios can run against either a native loopback or a real board.
//
// Supported platforms: AVR (Uno/Nano), ESP8266, ESP32/S3, RP2040
//
// Upload with PlatformIO from test/integration/sketch/:
//   pio run -e nano --target upload
//   pio run -e esp32s3 --target upload
//   pio run -e esp8266 --target upload
//   pio run -e rp2040 --target upload

#include <CmdMessenger.h>
#include <EEPROM.h>

CmdMessenger cmdMessenger = CmdMessenger(Serial);

// --- EEPROM Identity Storage ---
// Layout at address 0:
//   [0]       magic byte (0xA5 = provisioned)
//   [1..8]    model  (null-padded, max 8 chars)
//   [9..16]   id     (null-padded, max 8 chars)
// Total: 17 bytes
static const int EEPROM_MAGIC_ADDR = 0;
static const uint8_t EEPROM_MAGIC  = 0xA5;
static const int MODEL_ADDR        = 1;
static const int ID_ADDR           = 9;
static const int FIELD_LEN         = 8;
static const int EEPROM_USED       = 17;

// Platform-specific EEPROM helpers
#if defined(ESP8266) || defined(ESP32)
  #define EEPROM_INIT()  EEPROM.begin(64)
  #define EEPROM_SAVE()  EEPROM.commit()
#elif defined(ARDUINO_ARCH_RP2040)
  #define EEPROM_INIT()  EEPROM.begin(64)
  #define EEPROM_SAVE()  EEPROM.commit()
#else
  // AVR: real EEPROM, no init/commit needed
  #define EEPROM_INIT()  ((void)0)
  #define EEPROM_SAVE()  ((void)0)
#endif

void eepromReadField(int addr, char *buf, int len) {
    for (int i = 0; i < len; i++)
        buf[i] = EEPROM.read(addr + i);
    buf[len] = '\0';
}

void eepromWriteField(int addr, const char *val, int len) {
    for (int i = 0; i < len; i++) {
        char c = (i < (int)strlen(val)) ? val[i] : '\0';
        EEPROM.write(addr + i, c);
    }
}

// Command IDs — MUST match LoopbackIntegrationTests.cs constants
enum {
    kAcknowledge,       // 0  - Boot / generic ack
    kError,             // 1  - Error response
    kEcho,              // 2  - Echo string arg
    kEchoResult,        // 3  - Echo string result
    kAddFloats,         // 4  - Add two floats
    kAddFloatsResult,   // 5  - Sum and difference
    kEchoInt,           // 6  - Echo int32
    kEchoIntResult,     // 7  - Echo int32 result
    kEchoBool,          // 8  - Echo bool
    kEchoBoolResult,    // 9  - Echo bool result
    kMultiArgs,         // 10 - Multiple typed args
    kMultiArgsResult,   // 11 - Multiple typed args result
    kPing,              // 12 - Ping (no args)
    kPong,              // 13 - Pong response
    kEchoInt16,         // 14 - Echo int16
    kEchoInt16Result,   // 15 - Echo int16 result
    kEchoDouble,        // 16 - Echo double
    kEchoDoubleResult,  // 17 - Echo double result
    kWhoAmI,            // 18 - Request device identity
    kWhoAmIResult,      // 19 - Identity response (model, id)
    kSetId,             // 20 - Write identity to EEPROM (model, id)
    kSetIdResult,       // 21 - Confirm identity written
};

void OnUnknownCommand() {
    cmdMessenger.sendCmd(kError, "Unknown command");
}

void OnEcho() {
    char *str = cmdMessenger.readStringArg();
    cmdMessenger.sendCmdStart(kEchoResult);
    cmdMessenger.sendCmdEscArg(str);
    cmdMessenger.sendCmdEnd();
}

void OnAddFloats() {
    float a = cmdMessenger.readFloatArg();
    float b = cmdMessenger.readFloatArg();
    cmdMessenger.sendCmdStart(kAddFloatsResult);
    cmdMessenger.sendCmdArg(a + b);
    cmdMessenger.sendCmdArg(a - b);
    cmdMessenger.sendCmdEnd();
}

void OnEchoInt() {
    long val = cmdMessenger.readInt32Arg();
    cmdMessenger.sendCmd(kEchoIntResult, val);
}

void OnEchoBool() {
    bool val = cmdMessenger.readBoolArg();
    cmdMessenger.sendCmd(kEchoBoolResult, (int)val);
}

void OnMultiArgs() {
    int intVal = cmdMessenger.readInt16Arg();
    float floatVal = cmdMessenger.readFloatArg();
    char *strVal = cmdMessenger.readStringArg();
    bool boolVal = cmdMessenger.readBoolArg();

    cmdMessenger.sendCmdStart(kMultiArgsResult);
    cmdMessenger.sendCmdArg(intVal);
    cmdMessenger.sendCmdArg(floatVal);
    cmdMessenger.sendCmdEscArg(strVal);
    cmdMessenger.sendCmdArg((int)boolVal);
    cmdMessenger.sendCmdEnd();
}

void OnPing() {
    cmdMessenger.sendCmd(kPong, "pong");
}

void OnEchoInt16() {
    int val = cmdMessenger.readInt16Arg();
    cmdMessenger.sendCmd(kEchoInt16Result, val);
}

void OnEchoDouble() {
    double val = cmdMessenger.readDoubleArg();
    cmdMessenger.sendCmdStart(kEchoDoubleResult);
    // Use scientific format: AVR double == float (4 B) and Print::print(double, 8)
    // overflows its internal buffer for large magnitudes (e.g. 1.5e10). Scientific
    // notation keeps the formatted string short while preserving full float precision.
    cmdMessenger.sendCmdSciArg(val, 7);
    cmdMessenger.sendCmdEnd();
}

void OnWhoAmI() {
    char model[FIELD_LEN + 1];
    char id[FIELD_LEN + 1];

    if (EEPROM.read(EEPROM_MAGIC_ADDR) != EEPROM_MAGIC) {
        // Unprovisioned
        cmdMessenger.sendCmdStart(kWhoAmIResult);
        cmdMessenger.sendCmdEscArg("UNPROVISIONED");
        cmdMessenger.sendCmdEscArg("");
        cmdMessenger.sendCmdEnd();
        return;
    }

    eepromReadField(MODEL_ADDR, model, FIELD_LEN);
    eepromReadField(ID_ADDR, id, FIELD_LEN);

    cmdMessenger.sendCmdStart(kWhoAmIResult);
    cmdMessenger.sendCmdEscArg(model);
    cmdMessenger.sendCmdEscArg(id);
    cmdMessenger.sendCmdEnd();
}

void OnSetId() {
    char *model = cmdMessenger.readStringArg();
    char *id    = cmdMessenger.readStringArg();

    EEPROM.write(EEPROM_MAGIC_ADDR, EEPROM_MAGIC);
    eepromWriteField(MODEL_ADDR, model, FIELD_LEN);
    eepromWriteField(ID_ADDR, id, FIELD_LEN);
    EEPROM_SAVE();

    // Echo back what was written for verification
    char readModel[FIELD_LEN + 1];
    char readId[FIELD_LEN + 1];
    eepromReadField(MODEL_ADDR, readModel, FIELD_LEN);
    eepromReadField(ID_ADDR, readId, FIELD_LEN);

    cmdMessenger.sendCmdStart(kSetIdResult);
    cmdMessenger.sendCmdEscArg(readModel);
    cmdMessenger.sendCmdEscArg(readId);
    cmdMessenger.sendCmdEnd();
}

void setup() {
    EEPROM_INIT();
    Serial.begin(115200);
    cmdMessenger.printLfCr();
    cmdMessenger.attach(OnUnknownCommand);
    cmdMessenger.attach(kEcho, OnEcho);
    cmdMessenger.attach(kAddFloats, OnAddFloats);
    cmdMessenger.attach(kEchoInt, OnEchoInt);
    cmdMessenger.attach(kEchoBool, OnEchoBool);
    cmdMessenger.attach(kMultiArgs, OnMultiArgs);
    cmdMessenger.attach(kPing, OnPing);
    cmdMessenger.attach(kEchoInt16, OnEchoInt16);
    cmdMessenger.attach(kEchoDouble, OnEchoDouble);
    cmdMessenger.attach(kWhoAmI, OnWhoAmI);
    cmdMessenger.attach(kSetId, OnSetId);

    cmdMessenger.sendCmd(kAcknowledge, "Arduino ready");
}

void loop() {
    cmdMessenger.feedinSerialData();
}
