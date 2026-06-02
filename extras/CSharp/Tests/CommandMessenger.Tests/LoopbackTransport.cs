using System;
using System.Collections.Concurrent;
using CommandMessenger.Transport;

namespace CommandMessenger.Tests
{
    /// <summary>
    /// In-memory ITransport implementation for unit testing.
    /// Write() enqueues bytes; Read() dequeues them.
    /// Raises DataReceived after every Write().
    /// </summary>
    public class LoopbackTransport : ITransport
    {
        private readonly ConcurrentQueue<byte[]> _buffer = new();
        private bool _connected;

        public event EventHandler DataReceived;

        public bool Connect()
        {
            _connected = true;
            return true;
        }

        public bool Disconnect()
        {
            _connected = false;
            return true;
        }

        public bool IsConnected() => _connected;

        public byte[] Read()
        {
            if (_buffer.TryDequeue(out var data))
                return data;
            return Array.Empty<byte>();
        }

        public void Write(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return;
            _buffer.Enqueue((byte[])buffer.Clone());
            DataReceived?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Inject bytes as if they arrived from the remote side.
        /// This simulates incoming data without going through Write().
        /// </summary>
        public void SimulateReceive(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            _buffer.Enqueue((byte[])data.Clone());
            DataReceived?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() { _connected = false; }
    }
}
