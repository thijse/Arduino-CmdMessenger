// Loopback test firmware for integration testing.
// Runs as a native process, communicates via stdin/stdout.
// Implements a set of test commands for the host integration test driver.

// StdioStream pulls in <thread>/<atomic>/<mutex> which need std::min/max BEFORE
// Arduino.h defines those as macros.
#include "StdioStream.h"

#include <chrono>
#include "Arduino.h"
#include "CmdMessenger.h"

#ifdef _WIN32
#include <windows.h>
#else
#include <unistd.h>
#endif

StdioStream stdioStream;
CmdMessenger cmdMessenger = CmdMessenger(stdioStream);

// Command IDs — must match the C# test harness
enum {
    kAcknowledge,       // 0 - Boot / generic ack
    kError,             // 1 - Error response
    kEcho,              // 2 - Echo string arg
    kEchoResult,        // 3 - Echo string result
    kAddFloats,         // 4 - Add two floats
    kAddFloatsResult,   // 5 - Sum and difference
    kEchoInt,           // 6 - Echo int32
    kEchoIntResult,     // 7 - Echo int32 result
    kEchoBool,          // 8 - Echo bool
    kEchoBoolResult,    // 9 - Echo bool result
    kMultiArgs,         // 10 - Multiple typed args
    kMultiArgsResult,   // 11 - Multiple typed args result
    kPing,              // 12 - Ping (no args)
    kPong,              // 13 - Pong response
    kEchoInt16,         // 14 - Echo int16
    kEchoInt16Result,   // 15 - Echo int16 result
    kEchoDouble,        // 16 - Echo double
    kEchoDoubleResult,  // 17 - Echo double result
};

// --- Callbacks ---

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
    cmdMessenger.sendCmdArg(val, 8); // 8 decimal places — text protocol default is only 2
    cmdMessenger.sendCmdEnd();
}

// --- Main ---

void setup() {
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

    cmdMessenger.sendCmd(kAcknowledge, "Arduino ready");
}

void loop() {
    cmdMessenger.feedinSerialData();

    // Yield a tiny bit to avoid burning 100% CPU
    #ifdef _WIN32
    Sleep(1);
    #else
    usleep(1000);
    #endif
}

// Entry point for native build
int main() {
    setup();
    // Run until stdin is closed (EOF). The reader thread signals isEof().
    while (!stdioStream.isEof()) {
        loop();
    }
    return 0;
}
