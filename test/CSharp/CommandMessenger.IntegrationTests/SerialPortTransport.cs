using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Threading;
using CommandMessenger.Transport;

namespace CommandMessenger.IntegrationTests
{
    /// <summary>
    /// ITransport over a real serial port (e.g. an Arduino Nano on COM11).
    /// Background reader thread pumps bytes into a queue; DataReceived fires
    /// whenever bytes arrive. Used by Layer 3b hardware-in-the-loop tests.
    /// </summary>
    public class SerialPortTransport : ITransport
    {
        private readonly string _portName;
        private readonly int _baudRate;
        private SerialPort _port;
        private Thread _readerThread;
        private readonly ConcurrentQueue<byte[]> _rxQueue = new();
        private volatile bool _running;

        public event EventHandler DataReceived;

        public SerialPortTransport(string portName, int baudRate = 115200)
        {
            _portName = portName;
            _baudRate = baudRate;
        }

        public bool Connect()
        {
            _port = new SerialPort(_portName, _baudRate, Parity.None, 8, StopBits.One)
            {
                DtrEnable = true,   // Required for Nano reset-on-connect
                RtsEnable = true,
                ReadTimeout = 100,
                WriteTimeout = 500,
                Encoding = System.Text.Encoding.GetEncoding("ISO-8859-1"),
            };
            _port.Open();

            // Nano's bootloader needs ~1.5s after DTR pulse before the sketch starts.
            // The boot ack wait in LoopbackScenariosBase handles that; we just need
            // to discard any noise from the bootloader handshake.
            Thread.Sleep(50);
            _port.DiscardInBuffer();

            _running = true;
            _readerThread = new Thread(ReaderLoop) { IsBackground = true, Name = "SerialPortReader" };
            _readerThread.Start();
            return true;
        }

        public bool Disconnect()
        {
            _running = false;
            try { _readerThread?.Join(500); } catch { }
            try { _port?.Close(); } catch { }
            return true;
        }

        public bool IsConnected() => _port?.IsOpen == true;

        public byte[] Read()
        {
            if (_rxQueue.TryDequeue(out var data))
                return data;
            return Array.Empty<byte>();
        }

        public void Write(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return;
            _port.Write(buffer, 0, buffer.Length);
        }

        private void ReaderLoop()
        {
            var buf = new byte[256];
            while (_running)
            {
                try
                {
                    int n = _port.Read(buf, 0, buf.Length);
                    if (n <= 0) continue;
                    var chunk = new byte[n];
                    Array.Copy(buf, chunk, n);
                    _rxQueue.Enqueue(chunk);
                    DataReceived?.Invoke(this, EventArgs.Empty);
                }
                catch (TimeoutException) { /* expected at ReadTimeout */ }
                catch (System.IO.IOException) { break; } // port closed
                catch (InvalidOperationException) { break; }
            }
        }

        public void Dispose()
        {
            Disconnect();
            _port?.Dispose();
        }
    }
}
