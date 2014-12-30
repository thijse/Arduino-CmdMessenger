Imports Microsoft.VisualBasic
Imports System
Imports CommandMessenger


Namespace CommandMessengerTests
	Public Class BinaryTextData
		Private _cmdMessenger As CmdMessenger
		Private ReadOnly _command As Enumerator
		Private ReadOnly _systemSettings As systemSettings

		Public Sub New(ByVal systemSettings As systemSettings, ByVal command As Enumerator)
			_systemSettings = systemSettings
			_command = command
			DefineCommands()
		End Sub


		' ------------------ Command Callbacks -------------------------
		Private Sub DefineCommands()
		End Sub

		Private Sub AttachCommandCallBacks()
		End Sub

		' ------------------ Test functions -------------------------

		Public Sub RunTests()
			Common.StartTestSet("Clear binary data")
			SetUpConnection()
			TestSendBoolData()
			TestSendEscStringData()
			TestSendBinInt16Data()
			TestSendBinInt32Data()
			TestSendBinFloatData()
			TestSendBinDoubleData()
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
			Common.StartTest("Ping-pong of random binary bool values")
			' Try a lot of random numbers
			For i = 0 To 99
				ValuePingPongBinBool(Random.RandomizeBool())
			Next i
			Common.EndTest()
		End Sub

		Public Sub TestSendBinInt16Data()
			Common.StartTest("Ping-pong of random binary Int16 values")
			' Try a lot of random numbers
			For i = 0 To 99
				ValuePingPongBinInt16(Random.RandomizeInt16(Int16.MinValue, Int16.MaxValue), 0)
			Next i
			Common.EndTest()
		End Sub

		Public Sub TestSendBinInt32Data()
			Common.StartTest("Ping-pong of random binary Int32 values")
			' Try a lot of random numbers
			For i = 0 To 99
				ValuePingPongBinInt32(Random.RandomizeInt32(Int32.MinValue, Int32.MaxValue), 0)
			Next i
			Common.EndTest()
		End Sub

		Private Sub TestSendBinFloatData()
			Common.StartTest("Ping-pong of handpicked binary float values")

			' Try some typical numbers
			ValuePingPongBinFloat(0.0F)
			ValuePingPongBinFloat(1.0F)
			ValuePingPongBinFloat(15.0F)
			ValuePingPongBinFloat(65535.0F)

			ValuePingPongBinFloat(0.00390625F)
			ValuePingPongBinFloat(0.00000000023283064365386962890625F)
			Common.EndTest()


			'Craft difficult floating point values, using all special characters.
			'These should all be handled correctly by escaping

			Common.StartTest("Ping-pong of floating point values, using all special characters")
			For a As Integer = 0 To 4
				For b As Integer = 0 To 4
					For c As Integer = 0 To 4
						For d As Integer = 0 To 4
							Dim charA = IntToSpecialChar(a)
							Dim charB = IntToSpecialChar(b)
							Dim charC = IntToSpecialChar(c)
							Dim charD = IntToSpecialChar(d)
							ValuePingPongBinFloat(CreateFloat(New var() { charA, charB, charC, charD }))
						Next d
					Next c
				Next b
			Next a
			Common.EndTest()

			Common.StartTest("Ping-pong of random binary float values")
			' Try a lot of random numbers
			For i As Integer = 0 To 999
				ValuePingPongBinFloat(Random.RandomizeFloat(-Single.MaxValue, Single.MaxValue))
			Next i
			Common.EndTest()
		End Sub

		Public Sub TestSendBinDoubleData()
			Dim range = If((_systemSettings.BoardType Is BoardType.Bit32), Double.MaxValue, Single.MaxValue)
			Dim stepsize = range / 100
			Common.StartTest("Ping-pong of increasing binary double values")
			' Try a lot of random numbers
			For i = 0 To 99
				ValuePingPongBinDouble(i * stepsize)
			Next i
			Common.EndTest()
			Common.StartTest("Ping-pong of random binary double values")
			For i = 0 To 99
				' Bigger values than this go wrong, due to truncation
				ValuePingPongBinDouble(Random.RandomizeDouble(-range, range))
			Next i
			Common.EndTest()
		End Sub

		Private Sub TestSendEscStringData()
			Common.StartTest("Echo strings")
			ValuePingPongEscString("abcdefghijklmnopqrstuvwxyz") ' No special characters, but escaped
			ValuePingPongEscString("abcde,fghijklmnopqrs,tuvwxyz") ' escaped parameter separators
			ValuePingPongEscString("abcde,fghijklmnopqrs,tuvwxyz,") ' escaped parameter separators at end
			ValuePingPongEscString("abc,defghij/klmnop//qr;stuvwxyz/") ' escaped escape char at end
			ValuePingPongEscString("abc,defghij/klmnop//qr;stuvwxyz//") ' double escaped escape char at end
			Common.EndTest()
		End Sub

		Private Sub ValuePingPongBinBool(ByVal value As Boolean)
			Dim pingCommand = New SendCommand(_command("ValuePing"), _command("ValuePong"), 1000)
			pingCommand.AddArgument(CShort(Fix(DataType.BBool)))
			pingCommand.AddBinArgument(value)
			Dim pongCommand = _cmdMessenger.SendCommand(pingCommand)

			If (Not pongCommand.Ok) Then
				Common.TestNotOk("No response on ValuePing command")
				Return
			End If

			Dim result = pongCommand.ReadBinBoolArg()
			If result Is value Then
				Common.TestOk("Value as expected")
			Else
				Common.TestNotOk("unexpected value received: " & result & " instead of " & value)
			End If
		End Sub

		Private Sub ValuePingPongBinInt16(ByVal value As Int16, ByVal accuracy As Int16)
			Dim pingCommand = New SendCommand(_command("ValuePing"), _command("ValuePong"), 1000)
			pingCommand.AddArgument(CShort(Fix(DataType.BInt16)))
			pingCommand.AddBinArgument(value)
			Dim pongCommand = _cmdMessenger.SendCommand(pingCommand)

			If (Not pongCommand.Ok) Then
				Common.TestNotOk("No response on ValuePing command")
			End If

			Dim result = pongCommand.ReadBinInt16Arg()

			Dim difference = Math.Abs(result - value)
			If difference <= accuracy Then
				Common.TestOk("Value as expected")
			Else
				Common.TestNotOk("unexpected value received: " & result & " instead of " & value)
			End If

		End Sub

		Private Sub ValuePingPongBinInt32(ByVal value As Int32, ByVal accuracy As Int32)
			Dim pingCommand = New SendCommand(_command("ValuePing"), _command("ValuePong"), 1000)
			pingCommand.AddArgument(CShort(Fix(DataType.BInt32)))
			pingCommand.AddBinArgument(CInt(Fix(value)))
			Dim pongCommand = _cmdMessenger.SendCommand(pingCommand)

			If (Not pongCommand.Ok) Then
				Common.TestNotOk("No response on ValuePing command")
				Return
			End If

			Dim result = pongCommand.ReadBinInt32Arg()

			Dim difference = Math.Abs(result - value)
			If difference <= accuracy Then
				Common.TestOk("Value as expected")
			Else
				Common.TestNotOk("unexpected value received: " & result & " instead of " & value)
			End If
		End Sub

		Private Sub ValuePingPongBinFloat(ByVal value As Single)
			Const accuracy As Single = Single.Epsilon
			Dim pingCommand = New SendCommand(_command("ValuePing"), _command("ValuePong"), 1000)
			pingCommand.AddArgument(CShort(Fix(DataType.BFloat)))
			pingCommand.AddBinArgument(value)
			Dim pongCommand = _cmdMessenger.SendCommand(pingCommand)

			If (Not pongCommand.Ok) Then
				Common.TestNotOk("No response on ValuePing command")
				Return
			End If

			Dim result = pongCommand.ReadBinFloatArg()

			Dim difference = Math.Abs(result - value)
			If difference <= accuracy Then
				Common.TestOk("Value as expected")
			Else
				Common.TestNotOk("unexpected value received: " & result & " instead of " & value)
			End If
		End Sub

		Private Sub ValuePingPongBinDouble(ByVal value As Double)

			Dim pingCommand = New SendCommand(_command("ValuePing"), _command("ValuePong"), 1000)
			pingCommand.AddArgument(CShort(Fix(DataType.BDouble)))
			pingCommand.AddBinArgument(value)
			Dim pongCommand = _cmdMessenger.SendCommand(pingCommand)

			If (Not pongCommand.Ok) Then
				Common.TestNotOk("No response on ValuePing command")
				Return
			End If

			Dim result = pongCommand.ReadBinDoubleArg()

			Dim difference = Math.Abs(result - value)

			' 
			' For 16bit, because of double-float-float-double casting a small error is introduced
			Dim accuracy = If((_systemSettings.BoardType Is BoardType.Bit32), Double.Epsilon, Math.Abs(value * 1e-6))

			If difference <= accuracy Then
				Common.TestOk("Value as expected")
			Else
				Common.TestNotOk("unexpected value received: " & result & " instead of " & value)
			End If
		End Sub

		Private Sub ValuePingPongEscString(ByVal value As String)
			Dim pingCommand = New SendCommand(_command("ValuePing"), _command("ValuePong"), 1000)
			pingCommand.AddArgument(CInt(Fix(DataType.EscString)))
			pingCommand.AddBinArgument(value) ' Adding a string as binary command will escape it
			Dim pongCommand = _cmdMessenger.SendCommand(pingCommand)

			If (Not pongCommand.Ok) Then
				Common.TestNotOk("No response on ValuePing command")
				Return
			End If

			Dim result = pongCommand.ReadBinStringArg()
			If value Is result Then
				Common.TestOk("Value as expected")
			Else
				Common.TestNotOk("unexpected value received: " & result & " instead of " & value)
			End If
		End Sub

		' Utility functions
		Private Function IntToSpecialChar(ByVal i As Integer) As Char
			Select Case i
				Case 0
					Return ";"c ' End of line
				Case 1
					Return ","c ' End of parameter
				Case 3
					Return "/"c ' Escaping next char
				Case 4
					Return ControlChars.NullChar ' End of byte array
				Case Else
					Return "a"c ' Normal character

			End Select
		End Function

		Private Function CreateFloat(ByVal chars() As Char) As Single
			Dim bytes = BinaryConverter.CharsToBytes(chars)
			Return BitConverter.ToSingle(bytes, 0)
		End Function
	End Class
End Namespace
