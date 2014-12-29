#region CmdMessenger - MIT - (c) 2013 Thijs Elenbaas.
/*
  CmdMessenger - library that provides command based messaging

  Permission is hereby granted, free of charge, to any person obtaining
  a copy of this software and associated documentation files (the
  "Software"), to deal in the Software without restriction, including
  without limitation the rights to use, copy, modify, merge, publish,
  distribute, sublicense, and/or sell copies of the Software, and to
  permit persons to whom the Software is furnished to do so, subject to
  the following conditions:

  The above copyright notice and this permission notice shall be
  included in all copies or substantial portions of the Software.

  Copyright 2013 - Thijs Elenbaas
*/
#endregion

using System;
using System.Text;
using System.Threading;
using CommandMessenger.Queue;
using CommandMessenger.Transport;

namespace CommandMessenger
{
    /// <summary>
    /// Manager for data over transport layer. 
    /// </summary>
    public class CommunicationManager : IDisposable
    {
        private readonly Encoding _stringEncoder = Encoding.GetEncoding("ISO-8859-1");	// The string encoder
        private readonly object _sendCommandDataLock = new object();        // The process serial data lock
        private readonly object _parseLinesLock = new object();
        private readonly ReceiveCommandQueue _receiveCommandQueue;

        private readonly ITransport _transport;
        private readonly IsEscaped _isEscaped;                                       // The is escaped

        private string _buffer = string.Empty;

        /// <summary> The field separator </summary>
        public char FieldSeparator { get; private set; }

        /// <summary>The command separator </summary>
        public char CommandSeparator { get; private set; }

        /// <summary> The escape character </summary>
        public char EscapeCharacter { get; private set; }

        /// <summary> Gets or sets a whether to print a line feed carriage return after each command. </summary>
        /// <value> true if print line feed carriage return, false if not. </value>
        public bool PrintLfCr { get; set; }

        public BoardType BoardType { get; set; }

        /// <summary> Gets or sets the time stamp of the last received line. </summary>
        /// <value> time stamp of the last received line. </value>
        public long LastLineTimeStamp { get; private set; }

        /// <summary> Constructor. </summary>
        /// <param name="receiveCommandQueue"></param>
        /// <param name="boardType">The Board Type. </param>
        /// <param name="commandSeparator">The End-Of-Line separator. </param>
        /// <param name="fieldSeparator"></param>
        /// <param name="escapeCharacter"> The escape character. </param>
        /// <param name="transport"> The Transport Layer</param>
        public CommunicationManager(ITransport transport, ReceiveCommandQueue receiveCommandQueue, 
            BoardType boardType, char commandSeparator,  char fieldSeparator, char escapeCharacter)
        {
            _transport = transport;
            _transport.DataReceived += NewDataReceived;

            _receiveCommandQueue = receiveCommandQueue;

            BoardType = boardType;
            CommandSeparator = commandSeparator;
            FieldSeparator = fieldSeparator;
            EscapeCharacter = escapeCharacter;

            _isEscaped = new IsEscaped();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void NewDataReceived(object o, EventArgs e)
        {
            ParseLines();
        }

        /// <summary> Connects to a transport layer defined through the current settings. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public bool Connect()
        {
            return !_transport.IsConnected() && _transport.Connect();
        }

        /// <summary> Stops listening to the transport layer </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public bool Disconnect()
        {
            return _transport.IsConnected() && _transport.Disconnect();
        }

        /// <summary> Writes a string to the transport layer. </summary>
        /// <param name="value"> The string to write. </param>
        public void WriteLine(string value)
        {
            Write(value + "\r\n");
        }

        /// <summary> Writes a parameter to the transport layer followed by a NewLine. </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="value"> The value. </param>
        public void WriteLine<T>(T value)
        {
            WriteLine(value.ToString());
        }

        /// <summary> Writes a parameter to the transport layer. </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="value"> The value. </param>
        public void Write<T>(T value)
        {
            Write(value.ToString());
        }

        /// <summary> Writes a string to the transport layer. </summary>
        /// <param name="value"> The string to write. </param>
        public void Write(string value)
        {
            byte[] writeBytes = _stringEncoder.GetBytes(value);
            _transport.Write(writeBytes);
        }

        /// <summary> Directly executes the send command operation. </summary>
        /// <param name="sendCommand">    The command to sent. </param>
        /// <param name="sendQueueState"> Property to optionally clear the send and receive queues. </param>
        /// <returns> A received command. The received command will only be valid if the ReqAc of the command is true. </returns>
        public ReceivedCommand ExecuteSendCommand(SendCommand sendCommand, SendQueue sendQueueState)
        {
            // Disable listening, all callbacks are disabled until after command was sent

            ReceivedCommand ackCommand;
            lock (_sendCommandDataLock)
            {
                sendCommand.CommunicationManager = this;
                sendCommand.InitArguments();

                if (sendCommand.ReqAc)
                {
                    // Stop processing receive queue before sending. Wait until receive queue is actualy done
                    _receiveCommandQueue.Suspend();
                }

                if (PrintLfCr)
                    WriteLine(sendCommand.CommandString());
                else
                    Write(sendCommand.CommandString());

                ackCommand = sendCommand.ReqAc ? BlockedTillReply(sendCommand.AckCmdId, sendCommand.Timeout, sendQueueState) : new ReceivedCommand();
                ackCommand.CommunicationManager = this;
            }

            if (sendCommand.ReqAc)
            {
                // Stop processing receive queue before sending
                _receiveCommandQueue.Resume();
            }

            return ackCommand;
        }

        /// <summary> Directly executes the send string operation. </summary>
        /// <param name="commandString"> The string to sent. </param>
        /// <param name="sendQueueState"> Property to optionally clear the send and receive queues. </param>
        /// <returns> The received command is added for compatibility. It will not yield a response. </returns>
        public ReceivedCommand ExecuteSendString(String commandString, SendQueue sendQueueState)
        {
            lock (_sendCommandDataLock)
            {
                if (PrintLfCr)
                    WriteLine(commandString);
                else
                {
                    Write(commandString);
                }
            }
            return new ReceivedCommand { CommunicationManager = this };
        }

        /// <summary> Blocks until acknowledgement reply has been received. </summary>
        /// <param name="ackCmdId"> acknowledgement command ID </param>
        /// <param name="timeout">  Timeout on acknowledge command. </param>
        /// <param name="sendQueueState"></param>
        /// <returns> A received command. </returns>
        private ReceivedCommand BlockedTillReply(int ackCmdId, int timeout, SendQueue sendQueueState)
        {
            var start = TimeUtils.Millis;
            var time = start;
            var acknowledgeCommand = new ReceivedCommand { CommunicationManager = this };
            while ((time - start < timeout) && !acknowledgeCommand.Ok)
            {
                time = TimeUtils.Millis;
                // Yield to other threads in order to process data in the buffer
                Thread.Yield();
                // Check if an acknowledgment command has come in
                acknowledgeCommand = CheckForAcknowledge(ackCmdId, sendQueueState);
            }

            return acknowledgeCommand;
        }

        /// <summary> Listen to the receive queue and check for a specific acknowledge command. </summary>
        /// <param name="ackCmdId">        acknowledgement command ID. </param>
        /// <param name="sendQueueState"> Property to optionally clear the send and receive queues. </param>
        /// <returns> The first received command that matches the command ID. </returns>
        private ReceivedCommand CheckForAcknowledge(int ackCmdId, SendQueue sendQueueState)
        {
            // Read command from received queue
            ReceivedCommand currentReceivedCommand = _receiveCommandQueue.DequeueCommand();
            if (currentReceivedCommand != null)
            {
                // Check if received command is valid
                if (!currentReceivedCommand.Ok) return currentReceivedCommand;

                // If valid, check if is same as command we are waiting for
                if (currentReceivedCommand.CmdId == ackCmdId)
                {
                    // This is command we are waiting for, so return
                    return currentReceivedCommand;
                }

                // This is not command we are waiting for
                if (sendQueueState != SendQueue.ClearQueue)
                {
                    // Add to queue for later processing
                    _receiveCommandQueue.QueueCommand(currentReceivedCommand);
                }
            }

            // Return not Ok received command
            return new ReceivedCommand { CommunicationManager = this };
        }

        private void ParseLines()
        {
            lock(_parseLinesLock) 
            {
                var data = _transport.Read();
                _buffer += _stringEncoder.GetString(data);

                do
                {
                    string currentLine = ParseLine();
                    if (string.IsNullOrEmpty(currentLine)) break;

                    LastLineTimeStamp = TimeUtils.Millis;
                    ProcessLine(currentLine);
                } 
                while (true);
            }
        }

        /// <summary> Processes the byte message and add to queue. </summary>
        private void ProcessLine(string line)
        {            
            // Read line from raw buffer and make command
            var currentReceivedCommand = ParseMessage(line);
            currentReceivedCommand.RawString = line;
            // Set time stamp
            currentReceivedCommand.TimeStamp = LastLineTimeStamp;
            // And put on queue
            _receiveCommandQueue.QueueCommand(currentReceivedCommand);
        }

        /// <summary> Parse message. </summary>
        /// <param name="line"> The received command line. </param>
        /// <returns> The received command. </returns>
        private ReceivedCommand ParseMessage(string line)
        {
            // Trim and clean line
            var cleanedLine = line.Trim('\r', '\n');
            cleanedLine = Escaping.Remove(cleanedLine, CommandSeparator, EscapeCharacter);

            return new ReceivedCommand(
                Escaping.Split(cleanedLine, FieldSeparator, EscapeCharacter, StringSplitOptions.RemoveEmptyEntries)) { CommunicationManager = this };
        }

        /// <summary> Reads a float line from the buffer, if complete. </summary>
        /// <returns> Whether a complete line was present in the buffer. </returns>
        private string ParseLine()
        {
            if (!string.IsNullOrEmpty(_buffer))
            {
                // Check if an End-Of-Line is present in the string, and split on first
                //var i = _buffer.IndexOf(CommandSeparator);
                var i = FindNextEol();
                if (i >= 0 && i < _buffer.Length)
                {
                    var line = _buffer.Substring(0, i + 1);
                    if (!string.IsNullOrEmpty(line))
                    {
                        _buffer = _buffer.Substring(i + 1);
                        return line;
                    }
                    _buffer = _buffer.Substring(i + 1);
                    return string.Empty;
                }
            }
            return string.Empty;
        }

        /// <summary> Searches for the next End-Of-Line. </summary>
        /// <returns> The the location in the string of the next End-Of-Line. </returns>
        private int FindNextEol()
        {
            int pos = 0;
            while (pos < _buffer.Length)
            {
                var escaped = _isEscaped.EscapedChar(_buffer[pos]);
                if (_buffer[pos] == CommandSeparator && !escaped)
                {
                    return pos;
                }
                pos++;
            }
            return pos;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {                
                // Stop polling
                _transport.DataReceived -= NewDataReceived;
            }
        }
    }
}