' *** CommandMessengerTest ***

' This project runs unit tests on several parts on the mayor parts of the CmdMessenger library
' Note that the primary function is not to serve as an example, so the code may be less documented 
' and clean than the example projects. 

'Check bluetooth connection
'Merge with other tree


Imports Microsoft.VisualBasic
Imports System
Imports System.IO.Ports
Imports CommandMessenger
Imports CommandMessenger.Serialport

Namespace CommandMessengerTests

	Public Class CommandMessengerTest
		Private _setupConnection As SetupConnection
		Private _acknowledge As Acknowledge
		Private _clearTextData As ClearTextData
		Private _binaryTextData As BinaryTextData
		Private _transferSpeed As TransferSpeed
		Private _multipleArguments As MultipleArguments

		Public Sub New()
			' Set up board & transport mode
			Dim teensy31 = New systemSettings() With {.Description = "Teensy 3.1", .MinReceiveSpeed = 2000000, .MinSendSpeed = 1250000, .MinDirectSendSpeed = 47500, .BoardType = BoardType.Bit32, .sendBufferMaxLength = 512, .Transport = New SerialTransport With {.CurrentSerialSettings = New SerialSettings() With {.PortName = "COM15", .BaudRate = 115200, .DataBits = 8, .Parity = Parity.None, .DtrEnable = False}}}

			Dim arduinoNano = New systemSettings() With {.Description = "Arduino Nano /w AT mega328", .MinReceiveSpeed = 82000, .MinSendSpeed = 90000, .MinDirectSendSpeed = 52000, .BoardType = BoardType.Bit16, .sendBufferMaxLength = 60, .Transport = New SerialTransport With {.CurrentSerialSettings = New SerialSettings() With {.PortName = "COM6", .BaudRate = 115200, .DataBits = 8, .Parity = Parity.None, .DtrEnable = False}}}

			Dim arduinoLeonardoOrProMicro = New systemSettings() With {.Description = "Arduino Leonardo or Sparkfun ProMicro /w AT mega32u4", .MinReceiveSpeed = 82000, .MinSendSpeed = 90000, .MinDirectSendSpeed = 52000, .BoardType = BoardType.Bit16, .sendBufferMaxLength = 60, .Transport = New SerialTransport With {.CurrentSerialSettings = New SerialSettings() With {.PortName = "COM13", .BaudRate = 115200, .DataBits = 8, .Parity = Parity.None, .DtrEnable = True}}}

			' Set up Command enumerators
			Dim command = DefineCommands()

			' Initialize tests, CHANGE "DEVICE" VARIABLE TO YOUR DEVICE!
			Dim device = arduinoNano
			InitializeTests(device, command)

			' Open log file for testing 
			Common.OpenTestLogFile("TestLogFile.txt")

			' Run tests
			RunTests()

			Common.CloseTestLogFile()
		End Sub


		Private Shared Function DefineCommands() As Enumerator
			Dim command = New Enumerator()
			' Set up default commands
			command.Add(New String() { "CommError", "kComment" })
			Return command
		End Function

		Private Sub InitializeTests(ByVal systemSettings As systemSettings, ByVal command As Enumerator)
			_setupConnection = New SetupConnection(systemSettings, command)
			_acknowledge = New Acknowledge(systemSettings, command)
			_clearTextData = New ClearTextData(systemSettings, command)
			_binaryTextData = New BinaryTextData(systemSettings, command)
			_multipleArguments = New MultipleArguments(systemSettings, command)
			_transferSpeed = New TransferSpeed(systemSettings, command)

		End Sub

		Private Sub RunTests()

			'Todo: implement autoconnect tests

			' Test opening and closing connection
			_setupConnection.RunTests()

			' Test acknowledgment both on PC side and embedded side
			_acknowledge.RunTests()

			'// Test all plain text formats
			_clearTextData.RunTests()

			'// Test all binary formats
			_binaryTextData.RunTests()

			'// Test sending multiple arguments
			_multipleArguments.RunTests()

			'// Test large series for completeness (2-way)
			'// todo

			'// Test speed
			_transferSpeed.RunTests()

			'// Test load
			'// todo

			'// Test Strategies
			'// todo

			' Summary of tests
			Common.TestSummary()

			' Exit application
			[Exit]()
		End Sub


		Public Sub [Exit]()
			Console.WriteLine("Press any key to stop...")
			Console.ReadKey()
			Environment.Exit(0)
		End Sub

	End Class
End Namespace
