#Region "CmdMessenger - MIT - (c) 2014 Thijs Elenbaas."
'
'  CmdMessenger - library that provides command based messaging
'
'  Permission is hereby granted, free of charge, to any person obtaining
'  a copy of this software and associated documentation files (the
'  "Software"), to deal in the Software without restriction, including
'  without limitation the rights to use, copy, modify, merge, publish,
'  distribute, sublicense, and/or sell copies of the Software, and to
'  permit persons to whom the Software is furnished to do so, subject to
'  the following conditions:
'
'  The above copyright notice and this permission notice shall be
'  included in all copies or substantial portions of the Software.
'
'  Copyright 2014 - Thijs Elenbaas
'
#End Region

Imports Microsoft.VisualBasic
Imports System
Imports CommandMessenger
Imports CommandMessenger.Serialport
Imports CommandMessenger.TransportLayer
Imports System.IO


Namespace CommandMessengerTests
	Public Class Common
		'private const string TestLogFile = "TestLogFile.txt";

		Private Const IdentTp As String = "      " ' indentation test part
		Private Const IdentTt As String = "   " ' indentation test
		Private Const IdentTs As String = " " ' indentation test series
		Private Const IdentSt As String = "      "
		Private Const IdentWn As String = "      "
		Private privateCmdMessenger As CmdMessenger
		Public Shared Property CmdMessenger() As CmdMessenger
			Get
				Return privateCmdMessenger
			End Get
			Set(ByVal value As CmdMessenger)
				privateCmdMessenger = value
			End Set
		End Property
		Private privateSerialTransport As SerialTransport
		Public Shared Property SerialTransport() As SerialTransport
			Get
				Return privateSerialTransport
			End Get
			Set(ByVal value As SerialTransport)
				privateSerialTransport = value
			End Set
		End Property

		Private Shared _loggingCommands As Boolean = False
		Private Shared _testStarted As Boolean = False
		Private Shared _testSetStarted As Boolean = False

		Private Shared _testDescription As String = ""
		Private Shared _testSetDescription As String = ""

		Private Shared _testElementFailCount As Integer = 0
		Private Shared _testElementPassCount As Integer = 0


		Private Shared _testFailCount As Integer = 0
		Private Shared _testPassCount As Integer = 0

		Private Shared _testSetFailCount As Integer = 0
		Private Shared _testSetPassCount As Integer = 0

		Private Shared _streamWriter As StreamWriter


		Public Shared Function Connect(ByVal systemSettings As systemSettings) As CmdMessenger
			CmdMessenger = New CmdMessenger(systemSettings.Transport, systemSettings.sendBufferMaxLength) With {.BoardType = systemSettings.BoardType}
			' Attach to NewLineReceived and NewLineSent for logging purposes
			LogCommands(True)

			CmdMessenger.Connect()
			Return CmdMessenger
		End Function

		Public Shared Sub LogCommands(ByVal logCommands As Boolean)
		  If logCommands AndAlso (Not _loggingCommands) Then
			  AddHandler CmdMessenger.NewLineReceived, AddressOf NewLineReceived
			  AddHandler CmdMessenger.NewLineSent, AddressOf NewLineSent
			  _loggingCommands = True
		  ElseIf (Not logCommands) AndAlso _loggingCommands Then
			  ' ReSharper disable DelegateSubtraction
			  RemoveHandler CmdMessenger.NewLineReceived, AddressOf NewLineReceived
			  RemoveHandler CmdMessenger.NewLineSent, AddressOf NewLineSent
			  _loggingCommands = False
			  ' ReSharper restore DelegateSubtraction
		  End If
		End Sub

		Public Shared Sub OpenTestLogFile(ByVal testLogFile As String)
			_streamWriter = New StreamWriter(testLogFile)
		End Sub


		Public Shared Sub CloseTestLogFile()
			  _streamWriter.Close()
			  _streamWriter = Nothing
		End Sub

		Public Shared Sub Disconnect()
			LogCommands(False)
			CmdMessenger.Disconnect()
			CmdMessenger.Dispose()
		End Sub


		' Remove beeps
		Public Shared Function Silence(ByVal input As String) As String
			Dim output = input.Replace(ChrW(&H0007), " "c)
			Return output
		End Function

		Public Shared Sub StartTestSet(ByVal testSetDescription As String)
			If _testSetStarted Then
				EndTestSet()
			End If
			_testSetDescription = testSetDescription
			WriteLine(IdentTs & "*************************************")
			WriteLine(IdentTs & "*** Start test-set " & _testSetDescription & " ****")
			WriteLine(IdentTs & "*************************************")
			WriteLine()
			_testFailCount = 0
			_testPassCount = 0
			_testSetStarted = True
		End Sub


		Public Shared Sub EndTestSet()
			WriteLine(IdentTs & "*************************************")
			WriteLine(IdentTs & "*** End test-set " & _testSetDescription & " ****")
			WriteLine(IdentTs & "*************************************")
			WriteLine()
			WriteLine(IdentTs & "Tests passed: " & _testPassCount)
			WriteLine(IdentTs & "Tests failed: " & _testFailCount)
			WriteLine()
			If _testFailCount > 0 Then
				_testSetFailCount += 1
			Else
				_testSetPassCount += 1
			End If
			_testSetStarted = False

		End Sub

		Public Shared Sub StartTest(ByVal testDescription As String)
			If _testStarted Then
				EndTest()
			End If
			_testDescription = testDescription
			WriteLine(IdentTt & "*** Start test " & _testDescription & " ****")
			WriteLine()
			_testElementPassCount = 0
			_testElementFailCount = 0
			_testStarted = True
		End Sub

		Public Shared Sub EndTest()
			WriteLine(IdentTt & "*** End test " & _testDescription & " ****")
			WriteLine()
			If _testElementPassCount + _testElementFailCount = 0 Then
				WriteLine(IdentTt & "No tests done")
			Else
				If _testElementFailCount > 0 Then
					_testFailCount += 1
					WriteLine(IdentTt & "Test failed")
				Else
					_testPassCount += 1
					WriteLine(IdentTt & "Test passed")
				End If
				If _testElementPassCount + _testElementFailCount > 1 Then
					WriteLine(IdentTt & "Test parts passed: " & _testElementPassCount)
					WriteLine(IdentTt & "Test parts failed: " & _testElementFailCount)
				End If
				WriteLine()
			End If

			_testStarted = False

			If _streamWriter IsNot Nothing Then
				_streamWriter.Flush()
			End If
		End Sub


		Public Shared Sub TestSummary()
			WriteLine(IdentTs & "*** Test Summary ****")
			WriteLine()

			If _testSetPassCount > 0 AndAlso _testSetFailCount > 0 Then
				WriteLine(IdentTs & "Some test sets failed!! ")
			ElseIf _testSetPassCount > 0 AndAlso _testSetFailCount = 0 Then
				WriteLine(IdentTs & "All test sets passed!! ")
			ElseIf _testSetPassCount = 0 AndAlso _testSetFailCount > 0 Then
				WriteLine(IdentTs & "All test sets failed!! ")
			End If
			If _testSetPassCount = 0 AndAlso _testSetFailCount = 0 Then
				WriteLine(IdentTs & "No tests done!! ")
			End If

			WriteLine(IdentTs & "Test sets passed: " & _testSetPassCount)
			WriteLine(IdentTs & "Test sets failed: " & _testSetFailCount)

			If _streamWriter IsNot Nothing Then
				_streamWriter.Flush()
			End If
		End Sub

		Public Shared Sub TestOk(ByVal resultDescription As String)
			WriteLine(IdentTp & "OK: " & resultDescription)
			_testElementPassCount += 1
		End Sub

		Public Shared Sub TestNotOk(ByVal resultDescription As String)
			WriteLine(IdentTp & "Not OK: " & resultDescription)
			_testElementFailCount += 1
		End Sub

		Public Shared Sub NewLineReceived(ByVal sender As Object, ByVal e As NewLineEvent.NewLineArgs)
			Dim message = e.Command.CommandString()
			'var message = CmdMessenger.CurrentReceivedLine;
			WriteLine(IdentSt & "Received > " & Silence(message))
		End Sub

		Public Shared Sub NewLineSent(ByVal sender As Object, ByVal e As NewLineEvent.NewLineArgs)
			'// Log data to text box
			Dim message = e.Command.CommandString()
			WriteLine(IdentSt & "Sent > " & Silence(message))
		End Sub

		Public Shared Sub OnUnknownCommand(ByVal arguments As ReceivedCommand)
			' In response to unknown commands and corrupt messages
			WriteLine(IdentWn & "Warn > Command without attached callback received")
		End Sub

		Public Shared Sub WriteLine()
			WriteLine("")
		End Sub

		Public Shared Sub WriteLine(ByVal message As String)
			Console.WriteLine(message)

			If _streamWriter IsNot Nothing Then
				_streamWriter.WriteLine(message)
			End If
		End Sub

		Public Shared Sub Write(ByVal message As String)
			Console.Write(message)
			If _streamWriter IsNot Nothing Then
				_streamWriter.Write(message)
			End If
		End Sub
	End Class
End Namespace
