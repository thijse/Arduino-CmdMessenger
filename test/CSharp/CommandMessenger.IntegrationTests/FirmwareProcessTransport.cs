using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CommandMessenger.Transport;

namespace CommandMessenger.IntegrationTests
{
    /// <summary>
    /// ITransport that spawns the loopback firmware as a subprocess and communicates
    /// via redirected stdin/stdout pipes. Used for cross-stack integration tests
    /// where we want to exercise both the C# host stack and the C++ firmware stack
    /// over a realistic byte-stream protocol — without any hardware.
    /// </summary>
    public class FirmwareProcessTransport : ITransport
    {
        private readonly string _executablePath;
        private Process _process;
        private Stream _stdin;
        private Stream _stdout;
        private Thread _readerThread;
        private readonly ConcurrentQueue<byte[]> _rxQueue = new();
        private volatile bool _running;

        public event EventHandler DataReceived;

        public FirmwareProcessTransport(string executablePath)
        {
            _executablePath = executablePath ?? throw new ArgumentNullException(nameof(executablePath));
        }

        public bool Connect()
        {
            if (!File.Exists(_executablePath))
                throw new FileNotFoundException("Firmware executable not found", _executablePath);

            var psi = new ProcessStartInfo
            {
                FileName = _executablePath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            _process = Process.Start(psi);
            if (_process == null) return false;

            _stdin = _process.StandardInput.BaseStream;
            _stdout = _process.StandardOutput.BaseStream;
            _running = true;

            _readerThread = new Thread(ReaderLoop) { IsBackground = true, Name = "FirmwareStdoutReader" };
            _readerThread.Start();

            return true;
        }

        public bool Disconnect()
        {
            _running = false;
            try { _stdin?.Close(); } catch { }
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    if (!_process.WaitForExit(2000))
                        _process.Kill();
                }
            }
            catch { }
            try { _readerThread?.Join(500); } catch { }
            return true;
        }

        public bool IsConnected() => _process != null && !_process.HasExited;

        public byte[] Read()
        {
            if (_rxQueue.TryDequeue(out var data))
                return data;
            return Array.Empty<byte>();
        }

        public void Write(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return;
            _stdin.Write(buffer, 0, buffer.Length);
            _stdin.Flush();
        }

        private void ReaderLoop()
        {
            var buf = new byte[256];
            try
            {
                while (_running)
                {
                    int n = _stdout.Read(buf, 0, buf.Length);
                    if (n <= 0) break; // EOF
                    var chunk = new byte[n];
                    Array.Copy(buf, chunk, n);
                    _rxQueue.Enqueue(chunk);
                    DataReceived?.Invoke(this, EventArgs.Empty);
                }
            }
            catch { /* pipe closed */ }
        }

        public void Dispose()
        {
            Disconnect();
            _process?.Dispose();
        }
    }
}
