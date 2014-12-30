Imports Microsoft.VisualBasic
Imports System
Imports CommandMessenger

Namespace CommandMessengerTests
	Public Class Acknowledge
		Private _cmdMessenger As CmdMessenger
		Private ReadOnly _command As Enumerator
		Private _acknowledgementByEmbeddedFinished As Boolean
		Private ReadOnly _systemSettings As systemSettings


		Public Sub New(ByVal systemSettings As systemSettings, ByVal command As Enumerator)
			_systemSettings = systemSettings
			_command = command
			DefineCommands()
		End Sub

		' ------------------ Command Callbacks -------------------------
		Private Sub DefineCommands()
			_command.Add(New String() { "AskUsIfReady", "YouAreReady" })
		End Sub

		Private Sub AttachCommandCallBacks()
			_cmdMessenger.Attach(_command("AreYouReady"), AddressOf OnAreYouReadyCommand)
			_cmdMessenger.Attach(_command("YouAreReady"), AddressOf OnYouAreReadyCommand)
		End Sub


		' ------------------ Command Callbacks -------------------------

		Private Sub OnAreYouReadyCommand(ByVal arguments As ReceivedCommand)
			' In response to AreYouReady ping. We send an ACK acknowledgment to say that we are ready
			_cmdMessenger.SendCommand(New SendCommand(_command("Ack"), "We are ready"))
		End Sub

		Private Sub OnYouAreReadyCommand(ByVal arguments As ReceivedCommand)
			' in response to YouAreReady message 
			TestSendCommandWithAcknowledgementByArduinoFinished(arguments)
		End Sub

		' ------------------ Test functions -------------------------

		Public Sub RunTests()
			' Test opening and closing connection
			Common.StartTestSet("Waiting for acknowledgments")
			SetUpConnection()
			' Test acknowledgments
			TestSendCommandWithAcknowledgement()

			'TestSendCommandWithAcknowledgement();

			'TestSendCommandWithAcknowledgement();

			TestSendCommandWithAcknowledgementByArduino()
			WaitForAcknowledgementByEmbeddedFinished()

			TestSendCommandWithAcknowledgementAfterQueued()

			CloseConnection()
			Common.EndTestSet()
		End Sub

		Public Sub SetUpConnection()
			Try
				_cmdMessenger = Common.Connect(_systemSettings)
				AttachCommandCallBacks()
			Catch e1 As Exception
			End Try
			If (Not _systemSettings.Transport.IsConnected()) Then
				Common.TestOk("No issues during opening connection")
			End If
		End Sub

		Public Sub CloseConnection()
			Try
				Common.Disconnect()
			Catch e1 As Exception
			End Try
		End Sub

		' Test: Send a test command with acknowledgment needed
		Public Sub TestSendCommandWithAcknowledgement()
			Common.StartTest("Test sending command and receiving acknowledgment")
			Dim receivedCommand = _cmdMessenger.SendCommand(New SendCommand(_command("AreYouReady"), _command("Ack"), 1000))
			If receivedCommand.Ok Then
				Common.TestOk("Acknowledgment for command AreYouReady")
			Else
				Common.TestNotOk("No acknowledgment for command AreYouReady")
			End If
			Common.EndTest()
		End Sub


		Public Sub TestSendCommandWithAcknowledgementAfterQueued()
			Common.StartTest("Test sending command and receiving acknowledgment after larger queue")

			' Quickly sent a bunch of commands, that will be combined in a command string
			For i = 0 To 99
				_cmdMessenger.QueueCommand(New SendCommand(_command("AreYouReady")))
			Next i

			' Now wait for an acknowledge, terminating the command string
			Dim receivedCommand = _cmdMessenger.SendCommand(New SendCommand(_command("AreYouReady"), _command("Ack"), 1000), SendQueue.Default, ReceiveQueue.WaitForEmptyQueue)
			If receivedCommand.Ok Then
				Common.TestOk("Acknowledgment for command AreYouReady")
			Else
				Common.TestNotOk("No acknowledgment for command AreYouReady")
			End If
			Common.EndTest()
		End Sub

		Public Sub TestSendCommandWithAcknowledgementByArduino()
			Common.StartTest("TestSendCommandWithAcknowledgementByArduino")
			'SendCommandAskUsIfReady();
			_acknowledgementByEmbeddedFinished = False
			_cmdMessenger.SendCommand(New SendCommand(_command("AskUsIfReady")))

			' We will exit here, but the test has just begun:
			' - Next the arduino will call us with AreYouReady command which will trigger OnAreYouReadyCommand() 
			' - After this the Command TestAckSendCommandArduinoFinish will be called by Arduino with results
		End Sub

		Public Sub TestSendCommandWithAcknowledgementByArduinoFinished(ByVal command As ReceivedCommand)
			Dim result = command.ReadBoolArg()
			If (Not result) Then
				Common.TestNotOk("Incorrect response")
			End If
			_acknowledgementByEmbeddedFinished = True
		End Sub

		Public Sub WaitForAcknowledgementByEmbeddedFinished()
			For i = 0 To 9
				If _acknowledgementByEmbeddedFinished Then
					Common.TestOk("Received acknowledge from processor")
					Return
				End If
				System.Threading.Thread.Sleep(1000)
			Next i
			Common.TestNotOk("Received no acknowledge from  processor")
		End Sub

	End Class
End Namespace
