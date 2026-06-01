// *** SendWithoutSeparator ***

// This example demonstrates the sendArg() family of functions which send
// arguments WITHOUT the field separator prefix. This is useful when you need
// to construct custom packet formats or concatenate values into a single field.
//
// The sendArg() variants:
//   sendArg(value)        - Send value as string, no separator
//   sendArg(value, n)     - Send value with n decimal places, no separator
//   sendSciArg(value, n)  - Send double in scientific notation, no separator
//   sendBinArg(value)     - Send value in binary format, no separator
//
// Compare with the standard sendCmdArg() which always prepends a field separator.

#include <CmdMessenger.h>  // CmdMessenger

// Attach a new CmdMessenger object to the default Serial port
CmdMessenger cmdMessenger = CmdMessenger(Serial);

// This is the list of recognized commands.
enum
{
  kAcknowledge,       // 0: Command to acknowledge
  kError,             // 1: Command to report errors
  kRequestData,       // 2: PC requests sensor data
  kSensorData,        // 3: Arduino sends sensor data
  kCustomPacket,      // 4: Arduino sends a custom-format packet
};

// Callbacks
void attachCommandCallbacks()
{
  cmdMessenger.attach(OnUnknownCommand);
  cmdMessenger.attach(kRequestData, OnRequestData);
}

void OnUnknownCommand()
{
  cmdMessenger.sendCmd(kError, "Command without attached callback");
}

// Demonstrates standard sendCmdArg vs sendArg
void OnRequestData()
{
  float temperature = 23.456;
  float humidity = 67.89;
  int sensorId = 42;

  // === Example 1: Standard way (each arg gets a separator) ===
  // Result: "3,42,23.46,67.89;"
  cmdMessenger.sendCmdStart(kSensorData);
  cmdMessenger.sendCmdArg(sensorId);       // sends ",42"
  cmdMessenger.sendCmdArg(temperature, 2); // sends ",23.46"
  cmdMessenger.sendCmdArg(humidity, 2);    // sends ",67.89"
  cmdMessenger.sendCmdEnd();

  // === Example 2: Custom format using sendArg (no separator) ===
  // Build a compound field like "S42" as a single argument
  // Result: "4,S42,T23.46,H67.89;"
  cmdMessenger.sendCmdStart(kCustomPacket);

  // First field: "S" prefix + sensor ID concatenated (no separator between them)
  cmdMessenger.sendCmdArg("S");            // sends ",S"  (first arg gets separator)
  cmdMessenger.sendArg(sensorId);          // sends "42"  (no separator - appends to field)

  // Second field: "T" prefix + temperature
  cmdMessenger.sendCmdArg("T");            // sends ",T"
  cmdMessenger.sendArg(temperature, 2);    // sends "23.46"

  // Third field: "H" prefix + humidity
  cmdMessenger.sendCmdArg("H");            // sends ",H"
  cmdMessenger.sendArg(humidity, 2);       // sends "67.89"

  cmdMessenger.sendCmdEnd();
}

// Setup function
void setup()
{
  Serial.begin(115200);
  cmdMessenger.printLfCr();
  attachCommandCallbacks();
  cmdMessenger.sendCmd(kAcknowledge, "Arduino ready - SendWithoutSeparator example");
}

// Loop function
void loop()
{
  cmdMessenger.feedinSerialData();
}
