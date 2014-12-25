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
using System.IO;
using System.Net.Sockets;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

namespace CommandMessenger.Transport.Bluetooth
{
    /// <summary>
    /// Manager for Bluetooth connection
    /// </summary>
    public class BluetoothTransport : ITransport
    {
        private const int BufferSize = 4096;

        private NetworkStream _stream;
        private readonly AsyncWorker _worker;
        private readonly object _readLock = new object();
        private readonly object _writeLock = new object();
        readonly byte[] _readBuffer = new byte[BufferSize];
        private int _bufferFilled;

        // Event queue for all listeners interested in NewLinesReceived events.
        public event EventHandler DataReceived;

        /// <summary> Gets or sets the current serial port settings. </summary>
        /// <value> The current serial settings. </value>
        public BluetoothDeviceInfo CurrentBluetoothDeviceInfo { get; set; }

        public BluetoothClient BluetoothClient
        {
            get { return BluetoothUtils.LocalClient; }
        }

        public BluetoothTransport()
        {
            _worker = new AsyncWorker(Poll);
        }

        private bool Poll()
        {
            var bytes = UpdateBuffer();
            if (bytes > 0 && DataReceived != null) DataReceived(this, EventArgs.Empty);

            return true;
        }        

        /// <summary> Connects to a serial port defined through the current settings. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public bool Connect()
        {
            // Closing serial port if it is open
            _stream = null;

            // set pin of device to connect with            
            // check if device is paired
            //CurrentBluetoothDeviceInfo.Refresh();
            try
            {
                if (!CurrentBluetoothDeviceInfo.Authenticated)
                {
                    //Console.WriteLine("Not authenticated");
                    return false;
                }

                if (BluetoothClient.Connected)
                {
                    //Console.WriteLine("Previously connected, setting up new connection");
                    BluetoothUtils.UpdateClient();
                }

                // synchronous connection method
                BluetoothClient.Connect(CurrentBluetoothDeviceInfo.DeviceAddress, BluetoothService.SerialPort);

                if (!Open())
                {
                    Console.WriteLine("Stream not opened");
                    return false;
                }

                _worker.Start();
                
                return true;
            }
            catch (SocketException)
            {
                //Console.WriteLine("Socket exception while trying to connect");
                return false;
            }
            catch (InvalidOperationException)
            {
                BluetoothUtils.UpdateClient();
                return false;
            }
        }

        /// <summary> Opens the serial port. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public bool Open()
        {
            if (!BluetoothClient.Connected) return false;
            _stream = BluetoothClient.GetStream();
            _stream.ReadTimeout = 2000;
            _stream.WriteTimeout = 1000;
            return true;
        }

        public bool IsConnected()
        {
            // note: this does not always work. Perhaps do a scan
            return BluetoothClient.Connected;
        }

        public bool IsOpen()
        {
            // note: this does not always work. Perhaps do a scan
            return IsConnected() && (_stream != null);
        }


        /// <summary> Closes the Bluetooth stream port. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public bool Close()
        {
            // No closing needed
            if (_stream == null) return true;
            _stream.Close();
            _stream = null;
            return true;
        }

        /// <summary> Disconnect the bluetooth stream. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public bool Disconnect()
        {
            _worker.Stop();
            return Close();
        }

        /// <summary> Writes a byte array to the bluetooth stream. </summary>
        /// <param name="buffer"> The buffer to write. </param>
        public void Write(byte[] buffer)
        {
            try
            {
                if (IsOpen())
                {
                    lock (_writeLock)
                    {
                        _stream.Write(buffer,0,buffer.Length);
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary> Retrieves the address of the local bluetooth radio. </summary>
        /// <returns> The address of the local bluetooth radio. </returns>
        public BluetoothAddress RetreiveLocalBluetoothAddress()
        {
            var primaryRadio = BluetoothRadio.PrimaryRadio;
            if (primaryRadio == null) return null;
            return primaryRadio.LocalAddress;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private int UpdateBuffer()
        {
            if (IsOpen())
            {
                try
                {
                    lock (_readLock)
                    {
                        var nbrDataRead = _stream.Read(_readBuffer, _bufferFilled, (BufferSize - _bufferFilled));
                        _bufferFilled += nbrDataRead;
                    }
                    return _bufferFilled;
                }
                catch (IOException)
                {
                    // Timeout (expected)
                }
            }
            return 0;
        }

        /// <summary> Reads the serial buffer into the string buffer. </summary>
        public byte[] Read()
        {
            if (IsOpen())
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
            return IsOpen() ? _bufferFilled : 0;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disconnect();
                //if (IsOpen()) Close();
            }
        }
    }
}