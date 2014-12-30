Imports Microsoft.VisualBasic
Imports System
Imports System.Threading
Imports CommandMessenger

Namespace CommandMessengerTests
	Public Class TransferSpeed
		Private _cmdMessenger As CmdMessenger
		Private ReadOnly _command As Enumerator
		Private privateRunLoop As Boolean
		Public Property RunLoop() As Boolean
			Get
				Return privateRunLoop
			End Get
			Set(ByVal value As Boolean)
				privateRunLoop = value
			End Set
		End Property
		Private _receivedItemsCount As Integer ' Counter of number of command items received
		Private _receivedBytesCount As Integer ' Counter of number of command bytes received
		Private _beginTime As Long ' Start time, 1st item of sequence received
		Private _endTime As Long ' End time, last item of sequence received
'TODO: INSTANT VB TODO TASK: There is no VB.NET equivalent to 'volatile':
'ORIGINAL LINE: private volatile bool _receiveSeriesFinished;
		Private _receiveSeriesFinished As Boolean ' Indicates if plain text float series has been fully received
'TODO: INSTANT VB TODO TASK: There is no VB.NET equivalent to 'volatile':
'ORIGINAL LINE: private volatile bool _sendSeriesFinished;
		Private _sendSeriesFinished As Boolean
		Private ReadOnly _systemSettings As systemSettings
		Private Const SeriesLength As Integer = 10000 ' Number of items we like to receive from the Arduino
		Private Const SeriesBase As Single = 1.00001F ' Base of values to return: SeriesBase * (0..SeriesLength-1)
		Private _minimalBps As Single
		Public Sub New(ByVal systemSettings As systemSettings, ByVal command As Enumerator)
			_systemSettings = systemSettings
			_command = command
			DefineCommands()
		End Sub

		' ------------------ Command Callbacks -------------------------
		Private Sub DefineCommands()
			_command.Add(New String() { "RequestReset", "RequestResetAcknowledge", "RequestSeries", "ReceiveSeries", "DoneReceiveSeries", "PrepareSendSeries", "SendSeries", "AckSendSeries" })
		End Sub

		' ------------------ Command Callbacks -------------------------
		Private Sub AttachCommandCallBacks()
			_cmdMessenger.Attach(_command("RequestResetAcknowledge"), AddressOf OnRequestResetAcknowledge)
			_cmdMessenger.Attach(_command("ReceiveSeries"), AddressOf OnReceiveSeries)
			_cmdMessenger.Attach(_command("DoneReceiveSeries"), AddressOf OnDoneReceiveSeries)
			_cmdMessenger.Attach(_command("AckSendSeries"), AddressOf OnAckSendSeries)
		End Sub


		' ------------------ Test functions -------------------------

		Public Sub RunTests()
			' Open Connection
			Common.StartTestSet("Benchmarking transfer speeds")
			SetUpConnection()

			' Stop logging commands as this may degrade performance
			Common.LogCommands(False)

			' Test acknowledgments
			'*** Benchmark 1: receive series of float data
			SetupReceiveSeries()

			'*** Benchmark 2: queued send series of float data
			SetupQueuedSendSeries()

			'*** Benchmark 3: direct send series of float data
			DirectSendSeries()

			' Start logging commands again
			Common.LogCommands(True)

			' Close connection
			CloseConnection()
			Common.EndTestSet()
		End Sub

		Public Sub SetUpConnection()
			Try
				_cmdMessenger = Common.Connect(_systemSettings)
				AttachCommandCallBacks()
			Catch e1 As Exception
			End Try
		End Sub

		Public Sub CloseConnection()
			Try
				Common.Disconnect()
			Catch e1 As Exception
			End Try
		End Sub

		Private Sub WaitAndClear()
			Dim requestResetCommand = New SendCommand(_command("RequestReset"), _command("RequestResetAcknowledge"), 1000)
			Dim requestResetAcknowledge = _cmdMessenger.SendCommand(requestResetCommand, SendQueue.ClearQueue,ReceiveQueue.ClearQueue)

			Common.WriteLine(If((Not requestResetAcknowledge.Ok), "No Wait OK received", "Wait received"))
			' Wait another second to see if
			Thread.Sleep(1000)
			' Clear queues again to be very sure
			_cmdMessenger.ClearReceiveQueue()
			_cmdMessenger.ClearSendQueue()
		End Sub


		' Called when a RequestResetAcknowledge comes in   
		Private Sub OnRequestResetAcknowledge(ByVal receivedcommand As ReceivedCommand)
			' This function should not be called because OnRequestResetAcknowledge should
			' be handled by the requestResetAcknowledge synchronous command in WaitAndClear()
			' if it happens, we will do another WaitAndClear() and hope that works 
			WaitAndClear()
		End Sub

		' *** Benchmark 1 ***
		Private Sub SetupReceiveSeries()
			Common.StartTest("Calculating speed in receiving series of float data")

			WaitAndClear()

			_receiveSeriesFinished = False
			_receivedItemsCount = 0
			_receivedBytesCount = 0
			_minimalBps = _systemSettings.MinReceiveSpeed

			' Send command requesting a series of 100 float values send in plain text form           
			Dim commandPlainText = New SendCommand(_command("RequestSeries"))
			commandPlainText.AddArgument(SeriesLength)
			commandPlainText.AddArgument(SeriesBase)

			' Send command 
			_cmdMessenger.SendCommand(commandPlainText)

			' Now wait until all values have arrived
			Do While Not _receiveSeriesFinished
				Thread.Sleep(10)
			Loop
		End Sub

		' Callback function To receive the plain text float series from the Arduino
		Private Sub OnReceiveSeries(ByVal arguments As ReceivedCommand)

			If _receivedItemsCount Mod (SeriesLength \ 10) = 0 Then
				Common.WriteLine(_receivedItemsCount & " Received value: " & arguments.ReadFloatArg())
			End If
			If _receivedItemsCount = 0 Then
				' Received first value, start stopwatch
				_beginTime = Millis
			End If

			_receivedItemsCount += 1
			_receivedBytesCount += CountBytesInCommand(arguments, False)

		End Sub

		Private Sub OnDoneReceiveSeries(ByVal receivedcommand As ReceivedCommand)
			Dim bps As Single = CalcTransferSpeed()

			If bps > _minimalBps Then
				Common.TestOk("Embedded system is receiving data as fast as expected. Measured: " & bps & " bps, expected " & _minimalBps)
			Else
				Common.TestNotOk("Embedded system is receiving data not as fast as expected. Measured: " & bps & " bps, expected " & _minimalBps)
			End If


			_receiveSeriesFinished = True

			Common.EndTest()
		End Sub

		Private Function CalcTransferSpeed() As Single
			Common.WriteLine("Benchmark results")
			' Received all values, stop stopwatch
			_endTime = Millis
			Dim deltaTime = (_endTime - _beginTime)
			Common.WriteLine(deltaTime & " milliseconds per " & _receivedItemsCount & " items = is " & CSng(deltaTime) / CSng(_receivedItemsCount) & " ms/item, " & CSng(1000) * _receivedItemsCount / CSng(deltaTime) & " Hz")

			Dim bps As Single = CSng(8) * 1000 * _receivedBytesCount / CSng(deltaTime)
			Common.WriteLine(deltaTime & " milliseconds per " & _receivedItemsCount & " bytes = is " & CSng(deltaTime) / CSng(_receivedBytesCount) & " ms/byte, " & CSng(1000) * _receivedBytesCount / CSng(deltaTime) & " bytes/sec, " & bps & " bps")
			Return bps
		End Function


		' *** Benchmark 2 ***
		' Setup queued send series
		Private Sub SetupQueuedSendSeries()
			Common.StartTest("Calculating speed in sending queued series of float data")
			WaitAndClear()

			_minimalBps = _systemSettings.MinSendSpeed
			_sendSeriesFinished = False
			Dim prepareSendSeries = New SendCommand(_command("PrepareSendSeries"))
			prepareSendSeries.AddArgument(SeriesLength)
			_cmdMessenger.SendCommand(prepareSendSeries, SendQueue.WaitForEmptyQueue, ReceiveQueue.WaitForEmptyQueue)

			' Prepare
			_receivedBytesCount = 0
			_cmdMessenger.PrintLfCr = True
			_beginTime = Millis

			' Now queue all commands
			For sendItemsCount = 0 To SeriesLength - 1
				Dim sendSeries = New SendCommand(_command("SendSeries"))
				sendSeries.AddArgument(sendItemsCount * SeriesBase)

				_receivedBytesCount += CountBytesInCommand(sendSeries, _cmdMessenger.PrintLfCr)

				_cmdMessenger.QueueCommand(sendSeries)
				If sendItemsCount Mod (SeriesLength \ 10) = 0 Then
					Common.WriteLine("Send value: " & sendItemsCount * SeriesBase)
				End If
			Next sendItemsCount

			' Now wait until receiving party acknowledges that values have arrived
			Do While Not _sendSeriesFinished
				Thread.Sleep(10)
			Loop
		End Sub

		Private Sub OnAckSendSeries(ByVal receivedcommand As ReceivedCommand)
			Dim bps As Single = CalcTransferSpeed()

			If bps > _minimalBps Then
				Common.TestOk("Embedded system is receiving data as fast as expected. Measured: " & bps & " bps, expected " & _minimalBps)
			Else
				Common.TestNotOk("Embedded system is receiving data not as fast as expected. Measured: " & bps & " bps, expected " & _minimalBps)
			End If


			Common.EndTest()
			_sendSeriesFinished = True
		End Sub

		' *** Benchmark 3 ***
		Private Sub DirectSendSeries()
			Common.StartTest("Calculating speed in individually sending a series of float data")
			WaitAndClear()

			_minimalBps = _systemSettings.MinDirectSendSpeed
			_sendSeriesFinished = False

			Dim prepareSendSeries = New SendCommand(_command("PrepareSendSeries"))
			prepareSendSeries.AddArgument(SeriesLength)
			' We need to to send the prepareSendSeries by bypassing the queue or it might be sent after the directly send commands later on
			_cmdMessenger.SendCommand(prepareSendSeries, SendQueue.WaitForEmptyQueue, ReceiveQueue.WaitForEmptyQueue,UseQueue.BypassQueue)

			' Prepare
			_receivedBytesCount = 0
			_cmdMessenger.PrintLfCr = True
			_beginTime = Millis

			' Now send all commands individually and bypass the queue
			 For sendItemsCount = 0 To SeriesLength - 1
				Dim sendSeries = New SendCommand(_command("SendSeries"))
				sendSeries.AddArgument(sendItemsCount * SeriesBase)

				_receivedBytesCount += CountBytesInCommand(sendSeries, _cmdMessenger.PrintLfCr)

				_cmdMessenger.SendCommand(sendSeries, SendQueue.Default, ReceiveQueue.Default, UseQueue.BypassQueue)

				If sendItemsCount Mod (SeriesLength\10) = 0 Then
					Common.WriteLine("Send value: " & sendItemsCount*SeriesBase)
				End If
			 Next sendItemsCount
			_endTime = Millis
			' Now wait until receiving party acknowledges that values have arrived
			Do While Not _sendSeriesFinished
				Thread.Sleep(10)
			Loop
		End Sub

		' Tools
		Private Shared Function CountBytesInCommand(ByVal command As Command, ByVal printLfCr As Boolean) As Integer
			Dim bytes = command.CommandString().Length ' Command + command separator
			If printLfCr Then ' Add bytes for carriage return ('\r') and /or a newline ('\n')
				bytes += Environment.NewLine.Length
			End If
			Return bytes
		End Function

		' Return Milliseconds since 1970
		Public Shared ReadOnly Property Millis() As Long
			Get
				Return CLng(Fix((DateTime.Now.ToUniversalTime() - New DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds))
			End Get
		End Property
	End Class
End Namespace
