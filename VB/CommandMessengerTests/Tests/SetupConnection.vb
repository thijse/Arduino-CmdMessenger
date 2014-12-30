Imports Microsoft.VisualBasic
Imports System
Imports CommandMessenger
Imports CommandMessenger.TransportLayer

Namespace CommandMessengerTests

	Public Class SetupConnection
		Private ReadOnly _command As Enumerator
		Private ReadOnly _systemSettings As systemSettings
		Private _cmdMessenger As CmdMessenger

		Public Sub New(ByVal systemSettings As systemSettings, ByVal command As Enumerator)
			_systemSettings = systemSettings
			_command = command
			DefineCommands()
		End Sub

		Public Sub DefineCommands()
			_command.Add(New String() { "Ack", "AreYouReady", "Err" })
		End Sub

		Private Sub AttachCommandCallBacks()
			_cmdMessenger.Attach(Common.OnUnknownCommand)
		End Sub

		Public Sub RunTests()
			' Test opening and closing connection
			Common.StartTestSet("Opening connections")
			TestOpenConnection()
			TestSendCommand()
			TestCloseConnection()
			Common.EndTestSet()
		End Sub

		Public Sub TestOpenConnection()
			Common.StartTest("Test opening connection")
			Try
				_cmdMessenger = Common.Connect(_systemSettings)
				AttachCommandCallBacks()
			Catch e1 As Exception
				Common.TestNotOk("Exception during opening connection")
				Common.EndTest()
				Return
			End Try

			If _systemSettings.Transport.IsConnected() Then
				Common.TestOk("No issues during opening connection")
			Else
				Common.TestNotOk("Not open after trying to open connection")
			End If

			Common.EndTest()
		End Sub

		Public Sub TestCloseConnection()
			Common.StartTest("Test closing connection")
			Try
				Common.Disconnect()
			Catch e1 As Exception
				Common.TestNotOk("Exception during opening connection")
				Common.EndTest()
				Return
			End Try
			Console.WriteLine("No issues during closing of connection")

			If _systemSettings.Transport.IsConnected() Then
				Common.TestNotOk("Transport connection still open after disconnection")
			Else
				Common.TestOk("Transport connection not open anymore after disconnection")
			End If

			Common.EndTest()
		End Sub

		' Test: send a command without acknowledgment needed
		Public Sub TestSendCommand()
			Common.StartTest("Test sending command")
			Try
				_cmdMessenger.SendCommand(New SendCommand(_command("AreYouReady")))
			Catch e1 As Exception
				Common.TestNotOk("Exception during sending of command")
				Common.EndTest()
				Return
			End Try
			Common.TestOk("No issues during sending command")
			Common.EndTest()
		End Sub
	End Class
End Namespace
