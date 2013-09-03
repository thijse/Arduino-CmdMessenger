#region CmdMessenger - LGPL - (c) 2013 Thijs Elenbaas.
/*
  CmdMessenger - library that provides command based messaging

  The library is free software; you can redistribute it and/or
  modify it under the terms of the GNU Lesser General Public
  License as published by the Free Software Foundation; either
  version 2.1 of the License, or (at your option) any later version.

  This library is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
  Lesser General Public License for more details.

  You should have received a copy of the GNU Lesser General Public
  License along with this library; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA

    Copyright 2013 - Thijs Elenbaas
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;

namespace CommandMessenger
{
    /// <summary> Command messenger main class  </summary>
    public class CmdMessenger : IDisposable
    {


        public EventHandler NewLinesReceived;                               // Event handler for new lines received
        public EventHandler NewLineReceived;	                            // Event handler for a new line received
        public EventHandler NewLineSent;	                                // The new line sent
        private readonly Object _processSerialDataLock = new Object();      // The process serial data lock

        private SerialPortManager _communications;                          // The Serial port implementation
        private char _fieldSeparator;                                       // The field separator
        private char _commandSeparator;                                     // The command separator
        private char _escapeCharacter;                                      // The escape character

        private MessengerCallbackFunction _defaultCallback;                 // The default callback
        private Dictionary<int, MessengerCallbackFunction> _callbackList;   // List of callbacks

        /// <summary> Definition of the messenger callback function. </summary>
        /// <param name="receivedCommand"> The received command. </param>
        public delegate void MessengerCallbackFunction(ReceivedCommand receivedCommand);

        /// <summary> Gets or sets a whether to print a line feed carriage return after each command. </summary>
        /// <value> true if print line feed carriage return, false if not. </value>
        public bool PrintLfCr { get; set; }

        /// <summary> Gets or sets the current received command line. </summary>
        /// <value> The current received line. </value>
        public String CurrentReceivedLine { get; private set; }

        /// <summary> Gets or sets the current received command. </summary>
        /// <value> The current received command. </value>
        public ReceivedCommand CurrentReceivedCommand { get; private set; }

        /// <summary> Gets or sets the currently sent line. </summary>
        /// <value> The currently sent line. </value>
        public String CurrentSentLine { get; private set; }

        private Control _controlToInvokeOn; // The control to invoke the callback on

        /// <summary> Constructor. </summary>
        /// <param name="communications"> The Serial port object. </param>
        public CmdMessenger(SerialPortManager communications)
        {
            Init(communications, ',', ';', '/');
        }

        /// <summary> Constructor. </summary>
        /// <param name="communications"> The Serial port object. </param>
        /// <param name="fieldSeparator"> The field separator. </param>
        public CmdMessenger(SerialPortManager communications, char fieldSeparator)
        {
            Init(communications, fieldSeparator, ';', '/');
        }

        /// <summary> Constructor. </summary>
        /// <param name="communications">   The Serial port object. </param>
        /// <param name="fieldSeparator">   The field separator. </param>
        /// <param name="commandSeparator"> The command separator. </param>
        public CmdMessenger(SerialPortManager communications, char fieldSeparator, char commandSeparator)
        {
            Init(communications, fieldSeparator, commandSeparator, commandSeparator);
        }

        /// <summary> Constructor. </summary>
        /// <param name="communications">   The Serial port object. </param>
        /// <param name="fieldSeparator">   The field separator. </param>
        /// <param name="commandSeparator"> The command separator. </param>
        /// <param name="escapeCharacter">  The escape character. </param>
        public CmdMessenger(SerialPortManager communications, char fieldSeparator, char commandSeparator,
                            char escapeCharacter)
        {
            Init(communications, fieldSeparator, commandSeparator, escapeCharacter);
        }

        /// <summary> Initialises this object. </summary>
        /// <param name="communications">   The Serial port object. </param>
        /// <param name="fieldSeparator">   The field separator. </param>
        /// <param name="commandSeparator"> The command separator. </param>
        /// <param name="escapeCharacter">  The escape character. </param>
        private void Init(SerialPortManager communications, char fieldSeparator, char commandSeparator,
                          char escapeCharacter)
        {
            _controlToInvokeOn = null;
            _communications = communications;
            _communications = communications;
            _fieldSeparator = fieldSeparator;
            _commandSeparator = commandSeparator;
            _escapeCharacter = escapeCharacter;

            communications.EolDelimiter = _commandSeparator;
            Escaping.EscapeChars(fieldSeparator, commandSeparator, escapeCharacter);
            _callbackList = new Dictionary<int, MessengerCallbackFunction>();
            PrintLfCr = false;
            _communications.NewLineReceived += NewSerialDataReceived;
        }

        /// <summary> Sets a control to invoke on. </summary>
        /// <param name="controlToInvokeOn"> The control to invoke on. </param>
        public void SetControlToInvokeOn(Control controlToInvokeOn)
        {
            _controlToInvokeOn = controlToInvokeOn;
        }

        /// <summary>  Stop listening and end serial port connection. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public bool StopListening()
        {
            return _communications.StopListening();
        }

        /// <summary> Starts serial port connection and start listening. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public bool StartListening()
        {
            if (_communications.StartListening())
            {
                // Timestamp of this command is same as time stamp of serial line
                LastLineTimeStamp = _communications.LastLineTimeStamp;
                return true;
            }
            return false;
        }

        /// <summary> Attaches default callback for unsupported commands. </summary>
        /// <param name="newFunction"> The callback function. </param>
        public void Attach(MessengerCallbackFunction newFunction)
        {
            _defaultCallback = newFunction;
        }

        /// <summary> Attaches default callback for certain Message ID. </summary>
        /// <param name="messageId">   Command ID. </param>
        /// <param name="newFunction"> The callback function. </param>
        public void Attach(int messageId, MessengerCallbackFunction newFunction)
        {
            _callbackList[messageId] = newFunction;
        }

        /// <summary> Creates a new serial data received. </summary>
        /// <param name="sender"> Source of the event. </param>
        /// <param name="e"> Event information. </param>
        private void NewSerialDataReceived(object sender, EventArgs e)
        {
            lock (_processSerialDataLock)
            {
                ProcessLines();
                InvokeEvent(NewLinesReceived);
            }
        }

        /// <summary> Process the command lines and invokes callbacks. </summary>
        public void ProcessLines()
        {
            bool continueParsing = true;
            while (continueParsing)
            {
                var line = _communications.ReadLine();
                if (line != null)
                {
                    CurrentReceivedLine = CleanLine(line);
                    CurrentReceivedCommand = ParseMessage(CurrentReceivedLine);
                    LastLineTimeStamp = _communications.LastLineTimeStamp;
                    InvokeEvent(NewLineReceived);
                    HandleMessage(CurrentReceivedCommand);
                }
                else
                    continueParsing = false;
            }
        }

        /// <summary> Gets or sets the time stamp of the last command line received. </summary>
        /// <value> The last line time stamp. </value>
        public long LastLineTimeStamp { get; private set; }

        /// <summary> Clean line. </summary>
        /// <param name="line"> The line. </param>
        /// <returns> . </returns>
        private string CleanLine(string line)
        {
            var cleanedLine = line.Trim('\r', '\n');
            cleanedLine = Escaping.Remove(cleanedLine, _commandSeparator, _escapeCharacter);

            return cleanedLine;
        }

        /// <summary> Parse message. </summary>
        /// <param name="line"> The received command line. </param>
        /// <returns> The received command. </returns>
        private ReceivedCommand ParseMessage(string line)
        {
            // Split line in command and arguments     
            //var splitLine =  Escaping.Split(line, _fieldSeparator, _escapeCharacter, StringSplitOptions.RemoveEmptyEntries);
            //var command = new ReceivedCommand(splitLine);
            return
                new ReceivedCommand(Escaping.Split(line, _fieldSeparator, _escapeCharacter,
                                                   StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary> Handle message. </summary>
        /// <param name="receivedCommand"> The received command. </param>
        public void HandleMessage(ReceivedCommand receivedCommand)
        {
            MessengerCallbackFunction callback = null;
            //var commandId = -1;
            //ReceivedCommand receivedCommand;
            if (receivedCommand.Ok)
            {
                //receivedCommand = new ReceivedCommand(commandString);
                if (_callbackList.ContainsKey(receivedCommand.CommandId))
                {
                    callback = _callbackList[receivedCommand.CommandId];
                }
                else
                {
                    if (_defaultCallback != null) callback = _defaultCallback;
                }
            }
            else
            {
                // Empty command
                receivedCommand = new ReceivedCommand();
            }
            InvokeCallBack(callback, receivedCommand);
        }

        // Send command, including single argument

        /// <summary> Sends a command. </summary>
        /// <param name="cmdId"> Command ID. </param>
        /// <returns> . </returns>
        public ReceivedCommand SendCommand(int cmdId)
        {
            return SendCommand(cmdId, "");
        }

        /// <summary> Sends a command. </summary>
        /// <param name="cmdId">    Command ID. </param>
        /// <param name="argument"> The command argument. </param>
        /// <returns> . </returns>
        public ReceivedCommand SendCommand(int cmdId, string argument)
        {
            return SendCommand(cmdId, argument, false, 0, 0);
        }

        /// <summary> Sends a command. </summary>
        /// <param name="sendCommand"> The command to sent. </param>
        /// <returns> . </returns>
        public ReceivedCommand SendCommand(SendCommand sendCommand)
        {
            return SendCommand(sendCommand.CmdId, sendCommand.Arguments, sendCommand.ReqAc, sendCommand.AckCmdId,
                               sendCommand.Timeout);
        }

        /// <summary> Sends a command. </summary>
        /// <param name="cmdId">    Command ID. </param>
        /// <param name="argument"> The command argument. </param>
        /// <param name="reqAc">    true to request acknowledge command. </param>
        /// <param name="ackCmdId"> acknowledgement command ID </param>
        /// <param name="timeout">  Timeout on acknowlegde command. </param>
        /// <returns> . </returns>
        public ReceivedCommand SendCommand(int cmdId, string argument, bool reqAc, int ackCmdId, int timeout)
        {
            return SendCommand(cmdId, new[] {argument}, reqAc, ackCmdId, timeout);
        }

        /// <summary> Sends a command. </summary>
        /// <param name="cmdId">     Command ID. </param>
        /// <param name="arguments"> The arguments. </param>
        /// <param name="reqAc">     true to request acknowledge command. </param>
        /// <param name="ackCmdId">  acknowledgement command ID </param>
        /// <param name="timeout">   Timeout on acknowlegde command. </param>
        /// <returns> . </returns>
        public ReceivedCommand SendCommand(int cmdId, string[] arguments, bool reqAc, int ackCmdId, int timeout)
        {
            // Disable listening, all callbacks are disabled until after command was sent

            lock (_processSerialDataLock)
            {
                _communications.NewLineReceived -= NewSerialDataReceived;

                CurrentSentLine = cmdId.ToString(CultureInfo.InvariantCulture);

                foreach (var argument in arguments)
                {
                    CurrentSentLine += _fieldSeparator + argument;
                }

                if (PrintLfCr)
                    _communications.WriteLine(CurrentSentLine + _commandSeparator);
                else
                {
                    _communications.Write(CurrentSentLine + _commandSeparator);
                }

                InvokeEvent(NewLineSent);

                ReceivedCommand ackCommand = reqAc ? BlockedTillReply(ackCmdId, timeout) : new ReceivedCommand();
                _communications.NewLineReceived += NewSerialDataReceived;

                return ackCommand;
            }
        }

        /// <summary> Helper function to Invoke or directly call event. </summary>
        /// <param name="eventHandler"> The event handler. </param>
        private void InvokeEvent(EventHandler eventHandler)
        {
            try
            {
                if (eventHandler != null)
                {
                    if (_controlToInvokeOn != null && _controlToInvokeOn.InvokeRequired)
                    {
                        //Asynchronously call on UI thread
                        _controlToInvokeOn.Invoke(eventHandler, null);
                    }
                    else
                    {
                        //Directly call
                        eventHandler(this, null);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary> Helper function to Invoke or directly call callback function. </summary>
        /// <param name="messengerCallbackFunction"> The messenger callback function. </param>
        /// <param name="command">                   The command. </param>
        private void InvokeCallBack(MessengerCallbackFunction messengerCallbackFunction, ReceivedCommand command)
        {
            if (messengerCallbackFunction != null)
            {
                if (_controlToInvokeOn != null && _controlToInvokeOn.InvokeRequired)
                {
                    //Asynchronously call on UI thread
                    _controlToInvokeOn.Invoke(new MessengerCallbackFunction(messengerCallbackFunction), (object) command);
                }
                else
                {
                    //Directly call
                    messengerCallbackFunction(command);
                }
            }
        }

        /// <summary> Blocks until acknowlegdement reply has been received. </summary>
        /// <param name="ackCmdId"> acknowledgement command ID </param>
        /// <param name="timeout">  Timeout on acknowlegde command. </param>
        /// <returns> . </returns>
        private ReceivedCommand BlockedTillReply(int ackCmdId, int timeout)
        {
            // Disable listening, all callbacks are disabled until reply was received
            var start = TimeUtils.Millis;
            var time = start;
            var acknowledgeCommand = new ReceivedCommand();
            while ((time - start < timeout) && !acknowledgeCommand.Ok)
            {
                time = TimeUtils.Millis;
                acknowledgeCommand = CheckForAcknowledge(ackCmdId);
            }
            return acknowledgeCommand;
        }

        /// <summary> Check for acknowledgement. </summary>
        /// <param name="ackCmdId"> acknowledgement command ID </param>
        /// <returns> . </returns>
        private ReceivedCommand CheckForAcknowledge(int ackCmdId)
        {
            // Read single line from serial buffer
            string line = _communications.ReadLine();

            if (!String.IsNullOrEmpty(line))
            {
                CurrentReceivedLine = CleanLine(line);
                CurrentReceivedCommand = ParseMessage(CurrentReceivedLine);
                LastLineTimeStamp = _communications.LastLineTimeStamp;
                InvokeEvent(NewLineReceived);

                int commandId;
                if (!int.TryParse(CurrentReceivedCommand[0], out commandId)) return null;
                if (commandId == ackCmdId)
                {
                    return CurrentReceivedCommand;
                }
            }
            return new ReceivedCommand();
        }

        // Dispose 
        /// <summary> Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources. </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        // Dispose

        /// <summary> Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources. </summary>
        /// <param name="disposing"> true if resources should be disposed, false if not. </param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _controlToInvokeOn = null;
                _communications.NewLineReceived -= NewSerialDataReceived;
            }
        }
    }
}