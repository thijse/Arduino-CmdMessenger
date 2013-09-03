# CmdMessenger v3

A serial messaging library for the Arduino and .NET/Mono platform

## Introduction

CmdMessenger is a messaging library afor the Arduino Platform (and .NET/Mono platform). It has uses the serial port as its transport layer** 

The default message format is:
```
Cmd Id, param 1, [...] , param N;
```
The library can
* both send and receive of commands 
* Both write and read multiple arguments
* Both write and read all primary data types
* Attach callback functions any received command

The library supports any primary data types, and zero to many multiple arguments. Arguments can either be sent in plain text (to be human readable) 
or in binary form (to be efficient). 

With version 3 also comes a full implementation of the toolkit in C#, which runs both in Mono (http://monodevelop.com/Download) and Visual Studio (http://www.microsoft.com/visualstudio/eng#downloads)
This allows for full 2-way communication between the arduino controller and the PC.

\** but it could easily be modified to work over Bluetooth or a web interface.


## Requirements

* [Arduino IDE Version 1.0.5 or later](http://www.arduino.cc/en/Main/Software)* 

\* Earlier versions of the Arduino IDE may work but have not been tested.

## Getting Started

Get to know the library, by trying the examples,from simple to complex:
### Receive 
  The 1st example will make the PC toggle the integrated led on the arduino board. 
  * On the arduino side, it demonstrates how to:
	  - Define commands
	  - Set up a serial connection
	  - Receive a command with a parameter from the PC
  * On the PC side, it demonstrates how to:
	  - Define commands
	  - Set up a serial connection
	  - Send a command with a parameter to the Arduino

### SentandReceive 
  This example expands the previous Receive example. The Arduino will now send back a status. 
  On the arduino side, 
  * it demonstrates how to:
	  - Handle received commands that do not have a function attache
	  - Send a command with a parameter to the PC
  * On the PC side, it demonstrates how to:
	  - Handle received commands that do not have a function attached
	  - Receive a command with a parameter from the Arduino

### SendandReceiveArguments
  This example expands the previous SendandReceive example. The Arduino will now receive multiple 
  and sent multiple float values. 
  * On the arduino side, it demonstrates how to:
	  - Return multiple types status 
	  - Receive multiple parameters,
	  - Send multiple parameters
      - Call a function periodically
  * On the PC side, it demonstrates how to:
	  - Send multiple parameters, and wait for response 
	  - Receive multiple parameters
	  - Add logging events on data that has been sent or received
  
### SendandReceiveBinaryArguments
  This example expands the previous SendandReceiveArguments example. The Arduino will receive and send multiple 
  Binary values, demonstrating that this is more efficient way of communication. 
  * On the arduino side, it demonstrates how to:
	  - Send binary parameters
	  - Receive binary parameters
  * On the PC side, it demonstrates how to:
	  - Receive multiple binary parameters,
      - Send multiple binary parameters
      - How callback events can be handled while the main program waits
	  - How to calculate milliseconds, similar to Arduino function Millis()

All samples are heavily documented and should be self explanatory. 
1. Open the Example sketch in the Arduino IDE and compile and upload it to your board.
2. Open de CmdMessenger.sln solution in Visual Studio or Mono DevelopXamarin Studio
3. Set example project with same name as the Arduino sketch as startup project, and run
4. Enjoy!

## Trouble shooting
* If the PC and arduino are not able to connect, chances are that either the selected port on the PC side is not correct or that the Arduino and PC are not at the same baud rate. Try it out by typing commands into the Arduino Serial Monitor.
* If the port and baud rate are correct but callbacks are not being invoked, try looking at logging of sent and received data. See the SendandReceiveArguments project for an example

## Notes
An example for use with Max5 / MaxMSP was included up until version 2. (it can still be found here https://github.com/dreamcat4/CmdMessenger).
Since we have not been able to check it wil Max/MaxMSP, the example was removed.

## Changelog 

### CmdMessenger v3

* Wait for acknowlegde commands
* Sending of common type arguments (float, int, char)
* Multi-argument commands
* Escaping of special characters in strings
* Sending of binary data of any type (uses escaping, Base-64 anymor not necessary) 
* Bugfixes 
* Added code documentation
* Added multiple samples

### CmdMessenger v2 

* Updated to work with Arduino IDE 022
* Enable / disable newline (print and ignore)
* New generic example (works with all Arduinos)
* More reliable process() loop.
* User can set their own cmd and field seperator
 (defaults to ';' and ',')
* Base-64 encoded data to avoid collisions with ^^
* Works with Arduino Serial Monitor for easy debugging

## Credit

* Initial Messenger Library - Thomas Ouellet Fredericks.
* CmdMessenger Version 1    - Neil Dudman.
* CmdMessenger Version 2    - Dreamcat4.
* CmdMessenger Version 3    - Thijs Elenbaas

## Copyright

CmdMessenger is provided Copyright © 2013 under MIT License.

