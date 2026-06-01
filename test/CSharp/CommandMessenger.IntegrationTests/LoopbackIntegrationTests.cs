using CommandMessenger.Transport;

namespace CommandMessenger.IntegrationTests
{
    /// <summary>
    /// Layer 3a: runs <see cref="LoopbackScenariosBase"/> against the loopback
    /// firmware running as a native subprocess (stdin/stdout pipes, no hardware).
    /// </summary>
    public class LoopbackIntegrationTests : LoopbackScenariosBase
    {
        protected override ITransport CreateTransport()
        {
            var exe = FirmwareLocator.FindFirmwareExe();
            return new FirmwareProcessTransport(exe);
        }
    }
}
