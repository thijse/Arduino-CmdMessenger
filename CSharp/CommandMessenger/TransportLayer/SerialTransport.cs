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
using System.ComponentModel;
using System.IO.Ports;
using System.Reflection;
using System.Linq;
using System.Threading;

namespace CommandMessenger.TransportLayer
{
    public enum threadRunStates
    {
        Start,
        Stop,
    }

    /// <summary>Fas
    /// Manager for serial port data
    /// </summary>
    public class SerialTransport : DisposableObject, ITransport
    {
        //private TimedAction _pollBuffer;	                                            // Buffer for poll data
        private const double SerialBufferPollFrequency = 50;	                        // The serial buffer poll frequency
        private QueueSpeed queueSpeed = new QueueSpeed(4);
        private Thread _queueThread;

        public threadRunStates ThreadRunState = threadRunStates.Start;

        /// <summary> Default constructor. </summary>
        public SerialTransport()
        {          
            Initialize();
        }


        /// <summary> Initializes this object. </summary>
        public void Initialize()
        {
            queueSpeed.Name = "Serial";
            // Find installed serial ports on hardware
            _currentSerialSettings.PortNameCollection = SerialPort.GetPortNames();
            _currentSerialSettings.PropertyChanged += CurrentSerialSettingsPropertyChanged;

            // If serial ports are found, we select the first one
            if (_currentSerialSettings.PortNameCollection.Length > 0)
                _currentSerialSettings.PortName = _currentSerialSettings.PortNameCollection[0];
           // _pollBuffer = new TimedAction(DisposeStack,SerialBufferPollFrequency, CheckForNewData);

            // Create queue thread and wait for it to start
            _queueThread = new Thread(ProcessQueue) { Priority = ThreadPriority.Normal };
            _queueThread.Start();
            while (!_queueThread.IsAlive) { }
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

        protected  void ProcessQueue()
        {
            // Endless loop
            while (ThreadRunState == threadRunStates.Start)
            {
                var bytes = BytesInBuffer();
                if (bytes > 0)
                {
                    
                    queueSpeed.addCount(bytes);
                    queueSpeed.Sleep();
                    queueSpeed.CalcSleepTimeWithoutLoad();
                    if (NewDataReceived != null) NewDataReceived(this, null);
                }
                    

            }
        }

        /// <summary> Serial port data received. </summary>
        //private void CheckForNewData()
        //{
        //    if (BytesInBuffer()>0)
        //    {
        //        if (NewDataReceived!=null) NewDataReceived(this,null);
        //    }
        //}

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
            ThreadRunState = threadRunStates.Start;
            //_pollBuffer.Start();
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
            ThreadRunState = threadRunStates.Start;
            //_pollBuffer.StopAndWait();
            return Close();
        }

        /// <summary> Writes a parameter to the serial port. </summary>
        /// <param name="buffer"> The buffer to write. </param>
        public void Write(byte[] buffer)
        {
            try
            {
                if (IsOpen())
                {
                    _serialPort.Write(buffer, 0, buffer.Length);
                }
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
        public byte[] Read()
        {
            var buffer = new byte[0];
            if (IsOpen())
            {
                try
                {
                    var dataLength = _serialPort.BytesToRead;
                    buffer = new byte[dataLength];
                    int nbrDataRead = _serialPort.Read(buffer, 0, dataLength);
                    if (nbrDataRead == 0) return new byte[0];
                }
                catch
                { }
            }
            return buffer;
        }

        /// <summary> Gets the bytes in buffer. </summary>
        /// <returns> Bytes in buffer </returns>
        public int BytesInBuffer()
        {
            return IsOpen()? _serialPort.BytesToRead:0;
        }

        // Dispose
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                //
              //  _pollBuffer.StopAndWait();
                // Stop polling
                _queueThread.Abort();
                _queueThread.Join();
                _currentSerialSettings.PropertyChanged -= CurrentSerialSettingsPropertyChanged;
                // Releasing serial port 
                if (IsOpen()) Close();
                _serialPort.Dispose();
                _serialPort = null;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}