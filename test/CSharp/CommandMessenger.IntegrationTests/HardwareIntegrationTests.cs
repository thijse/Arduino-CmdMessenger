using System;
using System.IO.Ports;
using System.Linq;
using CommandMessenger.Transport;
using Xunit;

namespace CommandMessenger.IntegrationTests
{
    /// <summary>
    /// Layer 3b: runs the same scenarios as <see cref="LoopbackIntegrationTests"/>
    /// against a real Arduino Nano running the LoopbackTestRunner sketch.
    ///
    /// Marked with the "Hardware" category so it's only run on demand:
    ///   dotnet test --filter "Category=Hardware"
    ///
    /// Port discovery:
    ///   1. Environment variable CMDMSG_HW_PORT (e.g. "COM11")
    ///   2. First available SerialPort.GetPortNames() entry
    ///   3. Throws if none — tests fail with a clear message
    ///
    /// The sketch must already be uploaded — this fixture does not flash the
    /// board. Use:
    ///   cd test/integration/sketch
    ///   pio run -e nano --target upload
    /// </summary>
    [Trait("Category", "Hardware")]
    public class HardwareIntegrationTests : LoopbackScenariosBase
    {
        // Hardware needs more slack: serial bring-up + Nano bootloader reset
        protected override int AckTimeoutMs => 3000;
        protected override int BootTimeoutMs => 8000;

        protected override ITransport CreateTransport()
        {
            var port = ResolvePort()
                ?? throw new InvalidOperationException(
                    "No serial port available for hardware tests. " +
                    "Set CMDMSG_HW_PORT=COMx or connect an Arduino Nano.");
            return new SerialPortTransport(port);
        }

        private static string ResolvePort()
        {
            var fromEnv = Environment.GetEnvironmentVariable("CMDMSG_HW_PORT");
            if (!string.IsNullOrWhiteSpace(fromEnv))
                return fromEnv;
            return SerialPort.GetPortNames().OrderBy(p => p).FirstOrDefault();
        }
    }
}
