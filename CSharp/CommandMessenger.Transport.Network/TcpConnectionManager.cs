using System;

namespace CommandMessenger.Transport.Network
{
    public class TcpConnectionManager : ConnectionManager
    {
        private readonly TcpTransport _transport;

        public TcpConnectionManager(TcpTransport tcpTransport, CmdMessenger cmdMessenger, int identifyCommandId = 0, string uniqueDeviceId = null) 
            : base(cmdMessenger, identifyCommandId, uniqueDeviceId)
        {
            DeviceScanEnabled = false;

            if (tcpTransport == null)
                throw new ArgumentNullException("tcpTransport", "Transport is null.");

            _transport = tcpTransport;
        }

        protected override void DoWorkConnect()
        {
            if (_transport.Connect())
            {
                ConnectionFoundEvent();
            } 
        }

        protected override void DoWorkScan()
        {
            throw new NotSupportedException("ScanMode not supported by TcpConnectionManager.");
        }
    }
}
