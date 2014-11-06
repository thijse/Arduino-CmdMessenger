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
using System.IO.Ports;
using System.Threading;
using CommandMessenger.TransportLayer;
using System.IO;

namespace CommandMessenger.Serialport
{
    /// <summary>Fas
    /// Manager for serial port data
    /// </summary>
    public class SerialTransport : DisposableObject, ITransport
    {
        enum ThreadRunStates
        {
            Start,
            Stop,
            Abort,
        }

        private const int BufferMax = 4096;

        private Thread _queueThread;
        private ThreadRunStates _threadRunState;
        private readonly object _threadRunStateLock = new object();
        private readonly object _serialReadWriteLock = new object();
        private readonly object _readLock = new object();
        private readonly byte[] _readBuffer = new byte[BufferMax];
        private int _bufferFilled;

        /// <summary> Gets or sets the run state of the thread. </summary>
        /// <value> The thread run state. </value>
        private ThreadRunStates ThreadRunState  
        {
            set
            {
                lock (_threadRunStateLock)
                {
                    _threadRunState = value;
                }
            }
            get
            {
                ThreadRunStates result;
                lock (_threadRunStateLock)
                {
                    result = _threadRunState;
                }
                return result;
            }
        }

        /// <summary> Default constructor. </summary>
        public SerialTransport()
        {          
            Initialize();
        }

        #region Fields

        private SerialPort _serialPort;                                         // The serial port
        private SerialSettings _currentSerialSettings = new SerialSettings();   // The current serial settings
        public event EventHandler NewDataReceived;                              // Event queue for all listeners interested in NewLinesReceived events.

        #endregion

        #region Properties

        /// <summary> Gets or sets the current serial port settings. </summary>
        /// <value> The current serial settings. </value>
        public SerialSettings CurrentSerialSettings
        {
            get { return _currentSerialSettings; }
            set { _currentSerialSettings = value; }
        }

        #endregion

        #region Methods

        /// <summary> Initializes this object. </summary>
        private void Initialize()
        {
            // Create queue thread and wait for it to start
            _queueThread = new Thread(ProcessQueue)
            {
                Priority = ThreadPriority.Normal,
                Name = "SerialTransport"
            };
            ThreadRunState = ThreadRunStates.Start;
            _queueThread.Start();
            while (!_queueThread.IsAlive) { Thread.Sleep(50); }
        }

        private void ProcessQueue()
        {
            while (ThreadRunState != ThreadRunStates.Abort)
            {
                Poll(ThreadRunState);
            }
        }        

        /// <summary>
        /// Start Listening
        /// </summary>
        public void StartListening()
        {
            ThreadRunState = ThreadRunStates.Start;
        }

        /// <summary>
        /// Stop Listening
        /// </summary>
        public void StopListening()
        {
            ThreadRunState = ThreadRunStates.Stop;
        }

        private void Poll(ThreadRunStates threadRunState)
        {
            var bytes = UpdateBuffer();
            if (threadRunState == ThreadRunStates.Start)
            {
                if (bytes > 0 && NewDataReceived != null)
                    NewDataReceived(this, null);
            }
        }

        public void Poll()
        {
            Poll(ThreadRunStates.Start);
        }

        /// <summary> Connects to a serial port defined through the current settings. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public bool Connect()
        {
            if (!_currentSerialSettings.IsValid())
                throw new InvalidOperationException("Unable to open connection - serial settings invalid.");

            // Closing serial port if it is open
            Close();

            // Setting serial port settings
            _serialPort = new SerialPort(
                _currentSerialSettings.PortName,
                _currentSerialSettings.BaudRate,
                _currentSerialSettings.Parity,
                _currentSerialSettings.DataBits,
                _currentSerialSettings.StopBits)
                {
                    DtrEnable = _currentSerialSettings.DtrEnable,
                    WriteTimeout = _currentSerialSettings.Timeout,
                    ReadTimeout  = _currentSerialSettings.Timeout  
                };
       
            // Subscribe to event and open serial port for data
            ThreadRunState = ThreadRunStates.Start;
            return Open();
        }

        /// <summary> Query if the serial port is open. </summary>
        /// <returns> true if open, false if not. </returns>
        public bool IsConnected()
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
        public bool Disconnect()
        {
            ThreadRunState = ThreadRunStates.Stop;
            var state = Close();
            return state;
        }

        /// <summary> Writes a parameter to the serial port. </summary>
        /// <param name="buffer"> The buffer to write. </param>
        public void Write(byte[] buffer)
        {
            try
            {
                if (IsConnected())
                {
                    lock (_serialReadWriteLock)
                    {
                        _serialPort.Write(buffer, 0, buffer.Length);
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary> Reads the serial buffer into the string buffer. </summary>
        public byte[] Read()
        {
            if (IsConnected())
            {
                byte[] buffer;
                lock (_readLock)
                {
                    buffer = new byte[_bufferFilled];
                    Array.Copy(_readBuffer, buffer, _bufferFilled);
                    _bufferFilled = 0;
                }
                return buffer;
            }
            return new byte[0];
        }

        /// <summary> Gets the bytes in buffer. </summary>
        /// <returns> Bytes in buffer </returns>
        public int BytesInBuffer()
        {
            //return IsOpen()? _serialPort.BytesToRead:0;
            return _bufferFilled;
        }

        /// <summary> Opens the serial port. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        private bool Open()
        {
            if (_serialPort != null && PortExists() && !_serialPort.IsOpen)
            {
                try
                {
                    _serialPort.Open();
                    _serialPort.DiscardInBuffer();
                    _serialPort.DiscardOutBuffer();
                    return _serialPort.IsOpen;
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary> Closes the serial port. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        private bool Close()
        {
            try
            {
                if (_serialPort == null || !PortExists()) return false;
                if (!_serialPort.IsOpen) return true;
                _serialPort.Close();
                return true;
            }
            catch
            {
                return false;
            }            
        }

        /// <summary> Queries if a current port exists. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        private bool PortExists()
        {
            return SerialUtils.PortExists(_serialPort.PortName);
        }

        private int UpdateBuffer()
        {
            if (IsConnected())
            {
                try
                {
                    lock (_readLock)
                    {
                        var nbrDataRead = _serialPort.Read(_readBuffer, _bufferFilled, (BufferMax - _bufferFilled));
                        _bufferFilled += nbrDataRead;
                    }
                    return _bufferFilled;
                }
                catch (IOException)
                {
                    // Already communicating, is not
                }
                catch (TimeoutException)
                {
                    // Timeout (expected)
                }
            }
            return 0;
        }

        /// <summary> Kills this object. </summary>
        private void Kill()
        {
            // Signal thread to stop
            ThreadRunState = ThreadRunStates.Abort;

            //Wait for thread to die
            Join(1200);
            if (_queueThread.IsAlive) _queueThread.Abort();

            // Releasing serial port 
            Close();
            if (_serialPort != null)
            {
                _serialPort.Dispose();
                _serialPort = null;
            }

        }

        private bool Join(int millisecondsTimeout)
        {
            if (!_queueThread.IsAlive) return true;
            return _queueThread.Join(millisecondsTimeout);
        }

        // Dispose
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Kill();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}