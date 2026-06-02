using System;
using System.IO;

namespace CommandMessenger.IntegrationTests
{
    /// <summary>
    /// Locates the loopback firmware executable built by PlatformIO
    /// at test/integration/firmware/.pio/build/native/program.exe (or program on Linux).
    /// </summary>
    internal static class FirmwareLocator
    {
        public static string FindFirmwareExe()
        {
            // Walk up from the test assembly dir to find the repo root
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 10 && dir != null; i++)
            {
                var candidate = Path.Combine(dir, "test", "integration", "firmware",
                    ".pio", "build", "native",
                    OperatingSystem.IsWindows() ? "program.exe" : "program");
                if (File.Exists(candidate))
                    return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            throw new FileNotFoundException(
                "Loopback firmware not built. Run: pio run -e native in test/integration/firmware/");
        }
    }
}
