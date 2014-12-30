Imports Microsoft.VisualBasic
Imports System
Imports CommandMessenger


Namespace CommandMessengerTests
	Public Class MultipleArguments
		Private _cmdMessenger As CmdMessenger
		Private ReadOnly _command As Enumerator
		Private ReadOnly _systemSettings As systemSettings
		Private ReadOnly _randomNumber As System.Random

		Public Sub New(ByVal systemSettings As systemSettings, ByVal command As Enumerator)
			_systemSettings = systemSettings
			_command = command
			DefineCommands()
			_randomNumber = New System.Random(DateTime.Now.Millisecond)
		End Sub

		' ------------------ Command Callbacks -------------------------
		Private Sub DefineCommands()
			_command.Add(New String() { "MultiValuePing", "MultiValuePong" })
		End Sub

		Private Sub AttachCommandCallBacks()
		End Sub

		' ------------------ Test functions -------------------------

		Public Sub RunTests()

			Common.StartTestSet("Clear binary data")
			SetUpConnection()
			TestSendMultipleValues()
			CloseConnection()
			Common.EndTestSet()
		End Sub

		Public Sub SetUpConnection()
			_cmdMessenger = Common.Connect(_systemSettings)
			AttachCommandCallBacks()
		End Sub

		Public Sub CloseConnection()
			Common.Disconnect()
		End Sub

		Public Sub TestSendMultipleValues()
			Common.StartTest("Ping-pong of a command with handpicked binary int16, int32, and double parameters")
			'_cmdMessenger.LogSendCommandsEnabled = true;
			ValuePingPongBinInt16Int32Double(-11776, -1279916419, -2.7844819605867E+38)
			'_cmdMessenger.LogSendCommandsEnabled = false;
			Common.EndTest()

			Common.StartTest("Ping-pong of a command with random binary int16, int32, and double parameters")
			For i = 0 To 999
				' Bigger values than this go wrong, due to truncation
				ValuePingPongBinInt16Int32Double(Random.RandomizeInt16(Int16.MinValue, Int16.MaxValue), Random.RandomizeInt32(Int32.MinValue, Int32.MaxValue), Random.RandomizeDouble((If(_systemSettings.BoardType Is BoardType.Bit32, Double.MinValue, Single.MinValue)), (If(_systemSettings.BoardType Is BoardType.Bit32, Double.MaxValue, Single.MaxValue))))
			Next i
			Common.EndTest()
		End Sub

		Private Sub ValuePingPongBinInt16Int32Double(ByVal int16Value As Int16, ByVal int32Value As Int32, ByVal doubleValue As Double)
			Dim pingCommand = New SendCommand(_command("MultiValuePing"), _command("MultiValuePong"), 1000)
			pingCommand.AddBinArgument(int16Value)
			pingCommand.AddBinArgument(int32Value)
			pingCommand.AddBinArgument(doubleValue)
			Dim pongCommand = _cmdMessenger.SendCommand(pingCommand)

			If (Not pongCommand.Ok) Then
				Common.TestNotOk("No response on ValuePing command")
				Return
			End If
			Dim int16Result = pongCommand.ReadBinInt16Arg()
			Dim int32Result = pongCommand.ReadBinInt32Arg()
			Dim doubleResult = pongCommand.ReadBinDoubleArg()

			If int16Result Is int16Value Then
				Common.TestOk("1st parameter value, Int16, as expected: " & int16Result)
			Else
				Common.TestNotOk("unexpected 1st parameter value received: " & int16Result & " instead of " & int16Value)
			End If

			If int32Result Is int32Value Then
				Common.TestOk("2nd parameter value, Int32, as expected: " & int32Result)
			Else
				Common.TestNotOk("unexpected 2nd parameter value, Int32, received: " & int32Result & " instead of " & int32Value)
			End If

			' For 16bit, because of double-float-float-double casting a small error is introduced
			Dim accuracy = If((_systemSettings.BoardType Is BoardType.Bit32), Double.Epsilon, Math.Abs(doubleValue * 1e-6))
			Dim difference = Math.Abs(doubleResult - doubleValue)
			If difference <= accuracy Then
				Common.TestOk("3rd parameter value, Double, as expected: " & doubleResult)
			Else
				Common.TestNotOk("unexpected 3rd parameter value, Double, received: " & doubleResult & " instead of " & doubleValue)
			End If
		End Sub
	End Class
End Namespace
