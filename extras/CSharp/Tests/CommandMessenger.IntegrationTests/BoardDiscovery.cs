using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CommandMessenger.IntegrationTests
{
    /// <summary>
    /// Discovers provisioned CmdMessenger boards by querying kWhoAmI on all
    /// available serial ports. Results are cached for the lifetime of the process.
    ///
    /// Discovery order: first attempt USB VID:PID matching (fast, no DTR reset),
    /// then fall back to serial query for any unmatched ports.
    /// </summary>
    public static class BoardDiscovery
    {
        private const int kWhoAmI = 18;
        private static readonly Lazy<Dictionary<string, string>> _cache = new(Discover);

        /// <summary>
        /// Returns the COM port for a given board model (e.g. "NANO", "ESP32S3").
        /// Returns null if the board is not connected.
        /// </summary>
        public static string FindPort(string model)
        {
            _cache.Value.TryGetValue(model, out var port);
            return port;
        }

        /// <summary>
        /// Returns all discovered boards as a model→port dictionary.
        /// </summary>
        public static IReadOnlyDictionary<string, string> All => _cache.Value;

        private static Dictionary<string, string> Discover()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Phase 1: USB VID:PID discovery via WMI (no port opens, no DTR resets)
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");
                foreach (var obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString() ?? "";
                    var deviceId = obj["DeviceID"]?.ToString() ?? "";

                    var portMatch = Regex.Match(name, @"\(COM(\d+)\)");
                    if (!portMatch.Success) continue;
                    var portName = "COM" + portMatch.Groups[1].Value;

                    // Match known VID:PID combos to models
                    // Teensy: VID_16C0&PID_0483
                    if (deviceId.Contains("VID_16C0") && deviceId.Contains("PID_0483"))
                    {
                        result["TEENSY"] = portName;
                        matched.Add(portName);
                        continue;
                    }
                    // CP210x (ESP32-S3 in our setup): VID_10C4&PID_EA60
                    if (deviceId.Contains("VID_10C4") && deviceId.Contains("PID_EA60"))
                    {
                        result["ESP32S3"] = portName;
                        matched.Add(portName);
                        continue;
                    }
                }
            }
            catch
            {
                // WMI not available — fall through to serial query
            }

            // Phase 2: For CH340 ports (Nano + ESP8266 share VID:PID), query via serial
            var ports = SerialPort.GetPortNames();
            foreach (var portName in ports)
            {
                if (matched.Contains(portName)) continue;
                try
                {
                    var identity = QueryIdentity(portName);
                    if (identity.model != null && identity.model != "UNPROVISIONED")
                    {
                        result[identity.model] = portName;
                    }
                }
                catch
                {
                    // Port busy, not a CmdMessenger board, or timeout — skip
                }
            }

            return result;
        }

        private static (string model, string id) QueryIdentity(string portName)
        {
            using var sp = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One)
            {
                DtrEnable = true,
                RtsEnable = true,
                ReadTimeout = 100,
                WriteTimeout = 500,
            };

            sp.Open();

            // Wait for board to boot (DTR toggle causes reset on AVR)
            Thread.Sleep(2500);
            sp.DiscardInBuffer();

            // Send kWhoAmI
            sp.Write("18;\n");
            Thread.Sleep(600);

            // Read response
            var sb = new StringBuilder();
            var deadline = Environment.TickCount + 2000;
            while (Environment.TickCount < deadline)
            {
                try
                {
                    int b = sp.ReadByte();
                    if (b < 0) break;
                    char c = (char)b;
                    sb.Append(c);
                    if (c == ';') break;
                }
                catch (TimeoutException) { break; }
            }

            sp.Close();

            // Let the board settle after we close the port
            Thread.Sleep(200);

            // Parse "19,MODEL,ID;"
            var raw = sb.ToString().Trim().TrimEnd(';');
            var parts = raw.Split(',');
            if (parts.Length >= 3 && parts[0] == "19")
                return (parts[1], parts[2]);

            return (null, null);
        }
    }
}
