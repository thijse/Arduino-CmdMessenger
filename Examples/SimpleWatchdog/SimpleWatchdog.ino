#include <CmdMessenger.h>

CmdMessenger messenger(Serial);

enum
{
	cmdIdentify, // This command will be used both to identify exact device and for watchdog if multiple devices is connected to PC with serial communication
	cmdAck,
	cmdDoSomething
};

void setup()
{
	messenger.attach(onUnknownCommand);
	messenger.attach(cmdIdentify, onIdentifyRequest);
	messenger.attach(cmdDoSomething, onDoSomething);
}

void loop()
{
	messenger.feedinSerialData();
}

void onUnknownCommand()
{

}

void onIdentifyRequest()
{
	// Here we will send back our identifier
	// Use F() macro to store ID in PROGMEM
	messenger.sendCmd(cmdIdentify, F("BFAF4176-766E-436A-ADF2-96133C02B03C"));
}

void onDoSomething()
{
	// do something
}