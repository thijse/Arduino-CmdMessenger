Imports Microsoft.VisualBasic
Imports System
Imports CommandMessenger
Imports CommandMessenger.TransportLayer

Namespace CommandMessengerTests

	Friend Enum DataType As Integer
		Bool
		Int16
		Int32
		Float
		FloatSci
		[Double]
		DoubleSci
		[Char]
		[String]
		BBool
		BInt16
		BInt32
		BFloat
		BDouble
		BChar
		EscString

	End Enum

	Public Class ClearTextData
		Private _cmdMessenger As CmdMessenger
		Private ReadOnly _command As Enumerator
		Private _systemSettings As systemSettings

		Public Sub New(ByVal systemSettings As systemSettings, ByVal command As Enumerator)
			_systemSettings = systemSettings
			_command = command
			DefineCommands()
		End Sub


		' ------------------ Command Callbacks -------------------------
		Private Sub DefineCommands()
			_command.Add(New String() { "ValuePing", "ValuePong" })
		End Sub

		Private Sub AttachCommandCallBacks()
		End Sub

		' ------------------ Test functions -------------------------

		Public Sub RunTests()
			Common.StartTestSet("Clear text data")
			SetUpConnection()
			TestSendBoolData()
			TestSendInt16Data()
			TestSendInt32Data()
			TestSendFloatData()
			TestSendFloatSciData()
			TestSendDoubleSciData()
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

		Public Sub TestSendBoolData()
			Common.StartTest("Ping-pong of random plain-text bool values")
			' Try a lot of random numbers
			For i = 0 To 99
				ValuePingPongBool(Random.RandomizeBool())
			Next i
			Common.EndTest()
		End Sub

		Public Sub TestSendInt16Data()
			Common.StartTest("Ping-pong of random Int16 values")
			' Try a lot of random numbers
			For i = 0 To 99
				ValuePingPongInt16(Random.RandomizeInt16(Int16.MinValue, Int16.MaxValue), 0)
			Next i
			Common.EndTest()
		End Sub

		Public Sub TestSendInt32Data()
			Common.StartTest("Ping-pong of random Int32 values")
			' Try a lot of random numbers
			For i = 0 To 99
				ValuePingPongInt32(Random.RandomizeInt32(Int32.MinValue, Int32.MaxValue), 0)
			Next i
			Common.EndTest()
		End Sub

		Public Sub TestSendFloatData()
			' UInt32.MaxValue is the maximum range of the normal print float implementation
			Const stepsize As Single = CSng(UInt32.MaxValue) / 100
			Common.StartTest("Ping-pong of increasing float values")
			' Try a lot of random numbers
			For i = 0 To 99
				' Bigger values than this go wrong, due to truncation
				ValuePingPongFloat(i * stepsize)
			Next i
			Common.EndTest()
			Common.StartTest("Ping-pong of random float values")
			For i = 0 To 99
				' Bigger values than this go wrong, due to truncation
				ValuePingPongFloat(Random.RandomizeFloat(-UInt32.MaxValue, UInt32.MaxValue))
			Next i
			Common.EndTest()
		End Sub

		Public Sub TestSendFloatSciData()
			Const stepsize As Single = CSng(Single.MaxValue)/100
			Common.StartTest("Ping-pong of increasing float values, returned in scientific format")
			' Try a lot of random numbers
			For i = 0 To 99
				' Bigger values than this go wrong, due to truncation
				ValuePingPongFloatSci(i * stepsize, CSng(0.05))
			Next i
			Common.EndTest()
			Common.StartTest("Ping-pong of random float values, returned in scientific format")
			For i = 0 To 99
				ValuePingPongFloatSci(Random.RandomizeFloat(-Single.MaxValue, Single.MaxValue), 0.05f)
			Next i
			Common.EndTest()
		End Sub

		Public Sub TestSendDoubleSciData()
			Dim range = If((_systemSettings.BoardType Is BoardType.Bit32), Double.MaxValue, Single.MaxValue)
			Dim stepsize = range/100
			Common.StartTest("Ping-pong of increasing double values, returned in scientific format")
			' Try a lot of random numbers
			For i = 0 To 99
				ValuePingPongDoubleSci(i * stepsize, 0.05f)
			Next i
			Common.EndTest()
			Common.StartTest("Ping-pong of random double values, returned in scientific format")
			For i = 0 To 99
				' Bigger values than this go wrong, due to truncation
				ValuePingPongDoubleSci(Random.RandomizeDouble(-range, range), 0.05f)
			Next i
			Common.EndTest()
		End Sub

		Private Sub ValuePingPongInt16(ByVal value As Int16, ByVal accuracy As Int16)
			Dim pingCommand = New SendCommand(_command("ValuePing"), _command("ValuePong"), 1000)
			pingCommand.AddArgument(CShort(Fix(DataType.Int16)))
			pingCommand.AddArgument(value)
			Dim pongCommand = _cmdMessenger.SendCommand(pingCommand)

			If (Not pongCommand.Ok) Then
				Common.TestNotOk("No response on ValuePing command")
			End If

			Dim result = pongCommand.ReadInt16Arg()

			Dim difference = Math.Abs(result - value)

			If difference <= accuracy Then
				Common.TestOk("Value as expected")
			Else
				Common.TestNotOk("unexpected value received: " & result & " instead of " & value)
			End If
		End Sub

		Private Sub ValuePingPongInt32(ByVal value As Int32, ByVal accuracy As Int32)
			Dim pingCommand = New SendCommand(_command("ValuePing"), _command("ValuePong"), 1000)
			pingCommand.AddArgument(CShort(Fix(DataType.Int32)))
			pingCommand.AddArgument(CInt(Fix(value)))
			Dim pongCommand = _cmdMessenger.SendCommand(pingCommand)

			If (Not pongCommand.Ok) Then
				Common.TestNotOk("No response on ValuePing command")
				Return
			End If

			Dim result = pongCommand.ReadInt32Arg()

			Dim difference = Math.Abs(result - value)
			If difference <= accuracy Then
				Common.TestOk("Value as expected")
			Else
				Common.TestNotOk("unexpected value received: " & result & " instead of " & value)
			End If
		End Sub

		Private Sub ValuePingPongBool(ByVal value As Boolean)
			Dim pingCommand = New SendCommand(_command("ValuePing"), _command("ValuePong"), 1000)
			pingCommand.AddArgument(CShort(Fix(DataType.Bool)))
			pingCommand.AddArgument(value)
			Dim pongCommand = _cmdMessenger.SendCommand(pingCommand)

			If (Not pongCommand.Ok) Then
				Common.TestNotOk("No response on ValuePing command")
				Return
			End If

			Dim result = pongCommand.ReadBoolArg()

			If result Is value Then
				Common.TestOk("Value as expected")
			Else
				Common.TestNotOk("unexpected value received: " & result & " instead of " & value)
			End If
		End Sub

		Private Sub ValuePingPongFloat(ByVal value As Single)
			Dim pingCommand = New SendCommand(_command("ValuePing"), _command("ValuePong"), 1000)
			pingCommand.AddArgument(CShort(Fix(DataType.Float)))
			pingCommand.AddArgument(value)
			Dim pongCommand = _cmdMessenger.SendCommand(pingCommand)

			If (Not pongCommand.Ok) Then
				Common.TestNotOk("No response on ValuePing command")
				Return
			End If

			Dim result = pongCommand.ReadFloatArg()
			Dim difference = Math.Abs(result - value)

			Dim accuracy = Math.Abs(value * 2e-7)

			If difference <= accuracy Then
				Common.TestOk("Value as expected")
			Else
				Common.TestNotOk("unexpected value received: " & result & " instead of " & value)
			End If
		End Sub

		Private Sub ValuePingPongFloatSci(ByVal value As Single, ByVal accuracy As Single)
			Dim pingCommand = New SendCommand(_command("ValuePing"), _command("ValuePong"), 1000)
			pingCommand.AddArgument(CShort(Fix(DataType.FloatSci)))
			pingCommand.AddArgument(value)
			Dim pongCommand = _cmdMessenger.SendCommand(pingCommand)

			If (Not pongCommand.Ok) Then
				Common.TestNotOk("No response on ValuePing command")
				Return
			End If

			Dim result = pongCommand.ReadFloatArg()

			Dim difference = RelativeError(value, result)
			If difference <= accuracy Then
				Common.TestOk("Value as expected")
			Else
				Common.TestNotOk("unexpected value received: " & result & " instead of " & value)
			End If
		End Sub

		Private Sub ValuePingPongDoubleSci(ByVal value As Double, ByVal accuracy As Double)
			Dim pingCommand = New SendCommand(_command("ValuePing"), _command("ValuePong"), 1000)
			pingCommand.AddArgument(CShort(Fix(DataType.DoubleSci)))
			pingCommand.AddArgument(value)
			Dim pongCommand = _cmdMessenger.SendCommand(pingCommand)

			If (Not pongCommand.Ok) Then
				Common.TestNotOk("No response on ValuePing command")
				Return
			End If

			Dim result = pongCommand.ReadDoubleArg()

			Dim difference = RelativeError(value, result)

			If difference <= accuracy Then
				Common.TestOk("Value as expected")
			Else
				Common.TestNotOk("unexpected value received: " & result & " instead of " & value)
			End If
		End Sub

		Private Shared Function RelativeError(ByVal value As Double, ByVal result As Double) As Double
			Dim difference = If((Math.Abs(result) > Double.Epsilon), Math.Abs(result - value) / result, Math.Abs(result - value))
			If Double.IsNaN(difference) Then
				difference = 0
			End If
			Return difference
		End Function
	End Class
End Namespace
