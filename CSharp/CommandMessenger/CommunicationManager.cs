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
using System.Text;
using CommandMessenger.TransportLayer;

namespace CommandMessenger
{
    /// <summary>Fas
    /// Manager for serial port data
    /// </summary>
    public class CommunicationManager : DisposableObject
    {
        private ITransport _transport;
        public readonly Encoding StringEncoder = Encoding.GetEncoding("ISO-8859-1");	// The string encoder
        private string _buffer = "";	                                                // The buffer
        private readonly Queue<string> _lineBuffer = new Queue<string>();               // Buffer for string lines

        /// <summary> Default constructor. </summary>
        /// /// <param name="disposeStack"> The DisposeStack</param>
        /// <param name="transport"> The Transport Layer</param>
        public CommunicationManager(DisposeStack disposeStack,ITransport transport)
        {
            Initialize(disposeStack,transport, ';', '/');
        }

        /// <summary> Constructor. </summary>
        /// <param name="eolSeparator">    The End-Of-Line separator. </param>
        /// <param name="escapeCharacter"> The escape character. </param>
        /// <param name="disposeStack"> The DisposeStack</param>
        /// <param name="transport"> The Transport Layer</param>
        public CommunicationManager(DisposeStack disposeStack,ITransport transport, char eolSeparator, char escapeCharacter)
        {
            Initialize(disposeStack,transport, eolSeparator, escapeCharacter);
        }

        /// <summary> Finaliser. </summary>
        ~CommunicationManager()
        {
            Dispose(false);
        }

        /// <summary> Initializes this object. </summary>
        /// <param name="eolSeparator">    The End-Of-Line separator. </param>
        /// <param name="escapeCharacter"> The escape character. </param>
        /// <param name="disposeStack"> The DisposeStack</param>
        /// /// <param name="transport"> The Transport Layer</param>
        public void Initialize(DisposeStack disposeStack,ITransport transport, char eolSeparator, char escapeCharacter)
        {
            disposeStack.Push(this);
            _transport = transport;
            _transport.NewDataReceived += NewDataReceived;
            EolDelimiter = eolSeparator;
            _isEscaped = new IsEscaped();
           
        }

        #region Fields

        private IsEscaped _isEscaped;                                           // The is escaped
        public event EventHandler NewLinesReceived;                              // Event queue for all listeners interested in NewLinesReceived events.

        /// <summary> Gets or sets the End-Of-Line delimiter. </summary>
        /// <value> The End-Of-Line delimiter. </value>
        public char EolDelimiter { get; set; }

        /// <summary> Gets or sets the time stamp of the last received line. </summary>
        /// <value> time stamp of the last received line. </value>
        public long LastLineTimeStamp { get; set; }

        #endregion

        #region Properties


        #endregion

        #region Event handlers

        /// <summary> Serial port data received. </summary>
        private void NewDataReceived(object o, EventArgs e)
        {
            ParseLines();
        }

        #endregion

        #region Methods

        /// <summary> Connects to a serial port defined through the current settings. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public bool StartListening()
        {
            return _transport.StartListening();
        }

        /// <summary> Stops listening to the serial port. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public bool StopListening()
        {
            return _transport.StopListening();
        }

        /// <summary> Writes a string to the serial port. </summary>
        /// <param name="value"> The string to write. </param>
        public void WriteLine(string value)
        {
            byte[] writeBytes = StringEncoder.GetBytes(value + '\n');
            _transport.Write(writeBytes);
        }

        /// <summary> Writes a parameter to the serial port followed by a NewLine. </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="value"> The value. </param>
        public void WriteLine<T>(T value)
        {        
            var writeString = value.ToString();
            byte[] writeBytes = StringEncoder.GetBytes(writeString + '\n');
            _transport.Write(writeBytes);
        }

        /// <summary> Writes a parameter to the serial port. </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="value"> The value. </param>
        public void Write<T>(T value)
        {
            var writeString = value.ToString();
            byte[] writeBytes = StringEncoder.GetBytes(writeString);
            _transport.Write(writeBytes);
        }


        /// <summary> Reads the serial buffer into the string buffer. </summary>
        private void ReadInBuffer()
        {
            var data = _transport.Read();
            _buffer += StringEncoder.GetString(data);
        }

        /// <summary> Reads all lines from the serial buffer. </summary>
        private void ParseLines()
        {
            LastLineTimeStamp = TimeUtils.Millis;
            ReadInBuffer();
            bool newDataAvailable = false;
            while (ParseLine())
            {
                newDataAvailable = true;
            }
            //Send data to whom ever interested
            if (newDataAvailable && NewLinesReceived != null)
                NewLinesReceived(this, null);
        }

        /// <summary> Reads a single line from the serial buffer, if complete. </summary>
        /// <returns> Whether a complete line was present in the serial buffer. </returns>
        private bool ParseLine()
        {
            lock (_lineBuffer)
            {
                if (_buffer != "")
                {
                    // Check if an End-Of-Line is present in the string, and split on first
                    //var i = _buffer.IndexOf(EolDelimiter);
                    var i = FindNextEol();
                    if (i >= 0 && i < _buffer.Length)
                    {
                        var line = _buffer.Substring(0, i + 1);
                        if (!String.IsNullOrEmpty(line))
                        {
                            _lineBuffer.Enqueue(line);
                            _buffer = _buffer.Substring(i + 1);
                            return true;
                        }
                        _buffer = _buffer.Substring(i + 1);
                        return false;
                    }
                }
                return false;
            }
        }

        /// <summary> Searches for the next End-Of-Line. </summary>
        /// <returns> The the location in the string of the next End-Of-Line. </returns>
        private int FindNextEol()
        {
            int pos = 0;
            while (pos < _buffer.Length)
            {
                var escaped = _isEscaped.EscapedChar(_buffer[pos]);
                if (_buffer[pos] == EolDelimiter && !escaped)
                {
                    return pos;
                }
                pos++;
            }
            return pos;
        }

        /// <summary> Reads a line from the string buffer. </summary>
        /// <returns> The read line. </returns>
        public string ReadLine()
        {
            // Force a last update, if update has waited to long
            // This helps if a code was stopped in Serial port reading
            //if ((TimeUtils.Millis - LastLineTimeStamp) > 2*SerialBufferPollFrequency)
            //{
            //    ParseLines();
            //}
            lock (_lineBuffer)
            {
                return _lineBuffer.Count == 0 ? null : _lineBuffer.Dequeue();
            }
        }

        // Dispose
        /// <summary> Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources. </summary>
        /// <param name="disposing"> true if resources should be disposed, false if not. </param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {                
                // Stop polling
                _transport.NewDataReceived -= NewDataReceived;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}