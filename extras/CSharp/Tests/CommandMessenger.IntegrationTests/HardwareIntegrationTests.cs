using System;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using CommandMessenger.Transport;
using Xunit;

namespace CommandMessenger.IntegrationTests
{
    /// <summary>
    /// Layer 3b: runs the same 33 scenarios against real hardware boards running
    /// the LoopbackTestRunner sketch.
    ///
    /// Each board gets its own test class. All classes share the "HardwareSerial"
    /// collection so xUnit runs them sequentially — only one serial port is open
    /// at a time, avoiding conflicts during board discovery and DTR resets.
    ///
    /// Run all hardware tests:
    ///   dotnet test --filter "Category=Hardware"
    ///
    /// Run tests for a single board:
    ///   dotnet test --filter "FullyQualifiedName~NanoHardwareTests"
    ///
    /// Legacy single-board mode (still works):
    ///   CMDMSG_HW_PORT=COM12 dotnet test --filter "Category=Hardware"
    /// </summary>
    [CollectionDefinition("HardwareSerial", DisableParallelization = true)]
    public class HardwareSerialCollection { }

    [Collection("HardwareSerial")]
    public abstract class HardwareTestBase : LoopbackScenariosBase
    {
        protected override int AckTimeoutMs => 3000;
        protected override int BootTimeoutMs => 8000;

        protected abstract string TargetModel { get; }

        protected override ITransport CreateTransport()
        {
            var port = ResolvePort()
                ?? throw new InvalidOperationException(
                    $"Board '{TargetModel}' not found. Is it connected and provisioned?");
            return new SerialPortTransport(port);
        }

        /// <summary>
        /// Boards that reset on DTR (AVR, ESP) will send a boot ack on serial open.
        /// Boards that don't (Teensy) won't — so we fall back to a ping/pong
        /// handshake to verify the firmware is alive.
        /// </summary>
        protected override void WaitForReady()
        {
            ReceivedCommand boot = null;
            using var ready = new ManualResetEventSlim();
            Messenger.Attach(kAcknowledge, cmd => { boot = cmd; ready.Set(); });

            if (ready.Wait(BootTimeoutMs))
            {
                // Got boot ack — board reset on DTR
                return;
            }

            // No boot ack — board didn't reset (e.g. Teensy). Try ping/pong.
            ReceivedCommand pong = null;
            using var pingReady = new ManualResetEventSlim();
            Messenger.Attach(kPong, cmd => { pong = cmd; pingReady.Set(); });
            Messenger.SendCommand(new SendCommand(kPing));

            Assert.True(pingReady.Wait(AckTimeoutMs),
                $"Board '{TargetModel}' did not respond to boot ack or ping within timeout. " +
                "Is the firmware running?");
        }

        private string ResolvePort()
        {
            // Legacy: single-port override via env var
            var fromEnv = Environment.GetEnvironmentVariable("CMDMSG_HW_PORT");
            if (!string.IsNullOrWhiteSpace(fromEnv))
                return fromEnv;

            // Auto-discover by identity
            return BoardDiscovery.FindPort(TargetModel);
        }
    }

    [Trait("Category", "Hardware")]
    [Trait("Board", "Nano")]
    [Collection("HardwareSerial")]
    public class NanoHardwareTests : HardwareTestBase
    {
        protected override string TargetModel => "NANO";
    }

    [Trait("Category", "Hardware")]
    [Trait("Board", "ESP32S3")]
    [Collection("HardwareSerial")]
    public class Esp32S3HardwareTests : HardwareTestBase
    {
        protected override string TargetModel => "ESP32S3";
    }

    [Trait("Category", "Hardware")]
    [Trait("Board", "ESP8266")]
    [Collection("HardwareSerial")]
    public class Esp8266HardwareTests : HardwareTestBase
    {
        protected override string TargetModel => "ESP8266";
    }

    [Trait("Category", "Hardware")]
    [Trait("Board", "Teensy")]
    [Collection("HardwareSerial")]
    public class TeensyHardwareTests : HardwareTestBase
    {
        protected override string TargetModel => "TEENSY";
    }
}
