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
using System.ComponentModel;
using System.IO.Ports;
using System.Reflection;
using System.Text;
using System.Linq;

namespace CommandMessenger
{
    /// <summary>Fas
    /// Manager for serial port data
    /// </summary>
    public class SerialPortManager : IDisposable
    {
        private TimedAction _pollBuffer;	                                            // Buffer for poll data
        private const double SerialBufferPollFrequency = 50;	                        // The serial buffer poll frequency
        public readonly Encoding StringEncoder = Encoding.GetEncoding("ISO-8859-1");	// The string encoder
        private string _buffer = "";	                                                // The buffer
        private readonly Queue<string> _lineBuffer = new Queue<string>();               // Buffer for string lines

        /// <summary> Default constructor. </summary>
        public SerialPortManager()
        {
            Initialize(';', '/');
        }

        /// <summary> Constructor. </summary>
        /// <param name="eolSeparator">    The End-Of-Line separator. </param>
        /// <param name="escapeCharacter"> The escape character. </param>
        public SerialPortManager(char eolSeparator, char escapeCharacter)
        {
            Initialize(eolSeparator, escapeCharacter);
        }

        /// <summary> Finaliser. </summary>
        ~SerialPortManager()
        {
            Dispose(false);
        }

        /// <summary> Initializes this object. </summary>
        /// <param name="eolSeparator">    The End-Of-Line separator. </param>
        /// <param name="escapeCharacter"> The escape character. </param>
        public void Initialize(char eolSeparator, char escapeCharacter)
        {
            // Find installed serial ports on hardware
            _currentSerialSettings.PortNameCollection = SerialPort.GetPortNames();
            _currentSerialSettings.PropertyChanged += CurrentSerialSettingsPropertyChanged;

            // If serial ports are found, we select the first one
            if (_currentSerialSettings.PortNameCollection.Length > 0)
                _currentSerialSettings.PortName = _currentSerialSettings.PortNameCollection[0];
            EolDelimiter = eolSeparator;
            _isEscaped = new IsEscaped();
            _pollBuffer = new TimedAction(SerialBufferPollFrequency, SerialPortDataReceived);
        }

        #region Fields

        private SerialPort _serialPort;                                         // The serial port
        private SerialSettings _currentSerialSettings = new SerialSettings();   // The current serial settings
        private IsEscaped _isEscaped;                                           // The is escaped
        public event EventHandler NewLineReceived;                              // Event queue for all listeners interested in NewLineReceived events.

        /// <summary> Gets or sets the End-Of-Line delimiter. </summary>
        /// <value> The End-Of-Line delimiter. </value>
        public char EolDelimiter { get; set; }

        /// <summary> Gets or sets the time stamp of the last received line. </summary>
        /// <value> time stamp of the last received line. </value>
        public long LastLineTimeStamp { get; set; }

        #endregion

        #region Properties

        /// <summary> Gets or sets the current serial port settings. </summary>
        /// <value> The current serial settings. </value>
        public SerialSettings CurrentSerialSettings
        {
            get { return _currentSerialSettings; }
            set { _currentSerialSettings = value; }
        }

        /// <summary> Gets the serial port. </summary>
        /// <value> The serial port. </value>
        public SerialPort SerialPort
        {
            get { return _serialPort; }
        }

        #endregion

        #region Event handlers

        /// <summary> Current serial settings property changed. </summary>
        /// <param name="sender"> Source of the event. </param>
        /// <param name="e">      Property changed event information. </param>
        private void CurrentSerialSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // if serial port is changed, a new baud query is issued
            if (e.PropertyName.Equals("PortName"))
                UpdateBaudRateCollection();
        }

        /// <summary> Serial port data received. </summary>
        private void SerialPortDataReceived()
        {
            ParseLines();
        }

        #endregion

        #region Methods

        /// <summary> Connects to a serial port defined through the current settings. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public bool StartListening()
        {
            // Closing serial port if it is open

            if (IsOpen()) Close();

            // Setting serial port settings
            _serialPort = new SerialPort(
                _currentSerialSettings.PortName,
                _currentSerialSettings.BaudRate,
                _currentSerialSettings.Parity,
                _currentSerialSettings.DataBits,
                _currentSerialSettings.StopBits);

            // Subscribe to event and open serial port for data
            _pollBuffer.Start();
            return Open();
        }

        /// <summary> Opens the serial port. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public bool Open()
        {
            try
            {
                if (SerialPort != null && PortExists())
                {
                    _serialPort.Open();
                    return _serialPort.IsOpen;
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        /// <summary> Queries if a given port exists. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public bool PortExists()
        {
            return SerialPort.GetPortNames().Contains(_serialPort.PortName);
        }

        /// <summary> Closes the serial port. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public bool Close()
        {
            try
            {
                if (SerialPort != null && PortExists())
                {
                    _serialPort.Close();
                    return true;
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        /// <summary> Query ifthe serial port is open. </summary>
        /// <returns> true if open, false if not. </returns>
        public bool IsOpen()
        {
            try
            {
                return _serialPort != null && PortExists() && _serialPort.IsOpen;
            }
            catch
            {
                return false;
            }
        }

        /// <summary> Stops listening to the serial port. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public bool StopListening()
        {
            _pollBuffer.StopAndWait();
            return Close();
        }

        /// <summary> Writes a string to the serial port. </summary>
        /// <param name="value"> The string to write. </param>
        public void WriteLine(string value)
        {
            var writeString = value;
            try
            {
                byte[] writeBytes = StringEncoder.GetBytes(writeString + '\n');
                _serialPort.Write(writeBytes, 0, writeBytes.Length);
            }
            catch
            {
            }
        }

        /// <summary> Writes a parameter to the serial port followed by a NewLine. </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="value"> The value. </param>
        public void WriteLine<T>(T value)
        {
            var writeString = value.ToString();
            try
            {
                byte[] writeBytes = StringEncoder.GetBytes(writeString + '\n');
                _serialPort.Write(writeBytes, 0, writeBytes.Length);
            }
            catch
            {
            }
        }

        /// <summary> Writes a parameter to the serial port. </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="value"> The value. </param>
        public void Write<T>(T value)
        {
            var writeString = value.ToString();
            try
            {
                byte[] writeBytes = StringEncoder.GetBytes(writeString);
                _serialPort.Write(writeBytes, 0, writeBytes.Length);
            }
            catch
            {
            }
        }

        /// <summary> Retrieves the possible baud rates for the currently selected serial port. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public bool UpdateBaudRateCollection()
        {
            try
            {
                Close();
                _serialPort = new SerialPort(_currentSerialSettings.PortName);
                if (Open())
                {
                    var fieldInfo = _serialPort.BaseStream.GetType()
                                               .GetField("commProp", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (fieldInfo != null)
                    {
                        object p = fieldInfo.GetValue(_serialPort.BaseStream);
                        var fieldInfoValue = p.GetType()
                                              .GetField("dwSettableBaud",
                                                        BindingFlags.Instance | BindingFlags.NonPublic |
                                                        BindingFlags.Public);
                        if (fieldInfoValue != null)
                        {
                            var dwSettableBaud = (Int32) fieldInfoValue.GetValue(p);
                            Close();
                            _currentSerialSettings.UpdateBaudRateCollection(dwSettableBaud);
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        /// <summary> Reads the serial buffer into the string buffer. </summary>
        private void ReadInBuffer()
        {
            if (IsOpen())
            {
                try
                {
                    int dataLength = _serialPort.BytesToRead;
                    var data = new byte[dataLength];
                    int nbrDataRead = _serialPort.Read(data, 0, dataLength);
                    if (nbrDataRead == 0) return;

                    // Add the data to the buffer         

                    //_buffer += Encoding.ASCII.GetString(data);
                    _buffer += StringEncoder.GetString(data);
                    //_buffer += ToString(data);
                }
                catch (Exception)
                {
                }
            }
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
            if (newDataAvailable && NewLineReceived != null)
                NewLineReceived(this, null);
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
                        string line = _buffer.Substring(0, i + 1);
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
                else
                {
                    pos++;
                }
            }
            return pos;
        }

        /// <summary> Reads a line from the string buffer. </summary>
        /// <returns> The read line. </returns>
        public string ReadLine()
        {
            // Force a last update, if update has waited to long
            // This helps if a code was stopped in Serial port reading
            if ((TimeUtils.Millis - LastLineTimeStamp) > 2*SerialBufferPollFrequency)
            {
                ParseLines();
            }
            lock (_lineBuffer)
            {
                if (_lineBuffer.Count == 0) return null;
                return _lineBuffer.Dequeue();
            }
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
                _pollBuffer.StopAndWait();
                // Releasing serial port (and other unmanaged objects)

                if (IsOpen()) Close();
                _serialPort.Dispose();
            }
        }

        #endregion
    }
}