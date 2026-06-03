#!/usr/bin/env dotnet-script
// provision.csx — Interactive provisioning tool for CmdMessenger test boards.
//
// Flashes the LoopbackTestRunner sketch and writes a persistent identity
// (model + unique ID) into EEPROM so the hardware test runner can identify
// boards by serial query rather than by COM port number.
//
// Usage:
//   dotnet script test/provision.csx
//
// Flow:
//   1. Shows current USB-serial ports.
//   2. Asks user to plug in a board (waits for a new port to appear).
//   3. Detects the chip type (AVR/ESP32/ESP8266/RP2040) via PlatformIO.
//   4. Flashes the LoopbackTestRunner sketch for that platform.
//   5. Sends a kWhoAmI command to check current identity.
//   6. Assigns and writes a new identity (or confirms the existing one).
//   7. Loops — plug in next board or Ctrl-C to quit.
//
// Board registry is stored in test/provisioned.json.

#r "nuget: System.IO.Ports, 8.0.0"
#r "nuget: Newtonsoft.Json, 13.0.3"

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

// ─── Constants ───────────────────────────────────────────────────────────────
const int kAcknowledge  = 0;
const int kWhoAmI       = 18;
const int kWhoAmIResult = 19;
const int kSetId        = 20;
const int kSetIdResult  = 21;

// ─── Paths ───────────────────────────────────────────────────────────────────
string repoRoot = Path.GetFullPath(
    File.Exists(Path.Combine(Environment.CurrentDirectory, "library.properties"))
        ? Environment.CurrentDirectory
        : Path.Combine(Path.GetDirectoryName(Args.Count > 0 ? Args[0] : ".") ?? ".", ".."));

string sketchDir = Path.Combine(repoRoot, "test", "integration", "sketch");
string registryPath = Path.Combine(repoRoot, "test", "provisioned.json");

// ─── Board registry (persisted JSON) ─────────────────────────────────────────
class BoardEntry
{
    public string Model { get; set; }
    public string Id { get; set; }
    public string ChipInfo { get; set; }
    public string LastPort { get; set; }
    public DateTime ProvisionedAt { get; set; }
}

Dictionary<string, BoardEntry> LoadRegistry()
{
    if (!File.Exists(registryPath)) return new Dictionary<string, BoardEntry>();
    var json = File.ReadAllText(registryPath);
    return JsonConvert.DeserializeObject<Dictionary<string, BoardEntry>>(json)
           ?? new Dictionary<string, BoardEntry>();
}

void SaveRegistry(Dictionary<string, BoardEntry> reg)
{
    var json = JsonConvert.SerializeObject(reg, Formatting.Indented);
    File.WriteAllText(registryPath, json);
}

// ─── Model definitions ───────────────────────────────────────────────────────
// Maps a detected chip signature to (model prefix, PIO environment name)
record BoardDef(string ModelPrefix, string PioEnv);

var boardDefs = new Dictionary<string, BoardDef>(StringComparer.OrdinalIgnoreCase)
{
    // AVR signatures (from avrdude output)
    ["m328p"]    = new BoardDef("NANO",    "nano"),
    ["m328"]     = new BoardDef("UNO",     "uno"),
    ["atmega328p"] = new BoardDef("NANO",  "nano"),
    ["atmega328"]  = new BoardDef("UNO",   "uno"),
    // ESP chips (from esptool chip_id / PIO board detection)
    ["esp32-s3"] = new BoardDef("ESP32S3", "esp32s3"),
    ["esp32s3"]  = new BoardDef("ESP32S3", "esp32s3"),
    ["esp32"]    = new BoardDef("ESP32",   "esp32"),
    ["esp8266"]  = new BoardDef("ESP8266", "esp8266"),
    // RP2040
    ["rp2040"]   = new BoardDef("RP2040",  "rp2040"),
};

// ─── Helpers ─────────────────────────────────────────────────────────────────
string ResolvePio()
{
    // Try common locations
    var candidates = new[]
    {
        "pio",
        "platformio",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                     ".platformio", "penv", "Scripts", "pio.exe"),
    };
    foreach (var c in candidates)
    {
        try
        {
            var psi = new ProcessStartInfo(c, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
            if (p?.ExitCode == 0) return c;
        }
        catch { }
    }
    return null;
}

(int exitCode, string output) RunProcess(string exe, string args, string cwd = null, int timeoutMs = 60000)
{
    var psi = new ProcessStartInfo(exe, args)
    {
        WorkingDirectory = cwd ?? repoRoot,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
    };
    using var p = Process.Start(psi);
    var sb = new StringBuilder();
    p.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
    p.ErrorDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
    p.BeginOutputReadLine();
    p.BeginErrorReadLine();
    p.WaitForExit(timeoutMs);
    return (p.ExitCode, sb.ToString());
}

string[] GetUsbSerialPorts()
{
    return SerialPort.GetPortNames().OrderBy(p => p).ToArray();
}

// ─── Chip detection ──────────────────────────────────────────────────────────
string DetectChip(string port, string pio)
{
    Console.Write($"  Probing {port} ...");

    // 1. Try PIO device list (fast, uses USB descriptors when available)
    var (rc, output) = RunProcess(pio, "device list --serial", timeoutMs: 10000);
    // The output lists ports with descriptions; look for our port
    // This doesn't always give chip type, so we also try tools directly.

    // 2. Try esptool (ESP32/ESP8266)
    // PIO bundles esptool; use pio pkg exec
    var (rc2, out2) = RunProcess(pio, $"pkg exec -- esptool.py --port {port} --no-stub chip_id",
                                 cwd: sketchDir, timeoutMs: 15000);
    if (rc2 == 0)
    {
        var lower = out2.ToLowerInvariant();
        if (lower.Contains("esp32-s3")) { Console.WriteLine(" ESP32-S3"); return "esp32-s3"; }
        if (lower.Contains("esp32-s2")) { Console.WriteLine(" ESP32-S2"); return "esp32"; }
        if (lower.Contains("esp32-c3")) { Console.WriteLine(" ESP32-C3"); return "esp32"; }
        if (lower.Contains("esp32"))    { Console.WriteLine(" ESP32");    return "esp32"; }
        if (lower.Contains("esp8266"))  { Console.WriteLine(" ESP8266");  return "esp8266"; }
    }

    // 3. Try avrdude (AVR — Nano/Uno)
    // Use PIO's bundled avrdude
    var (rc3, out3) = RunProcess(pio,
        $"pkg exec -- avrdude -p m328p -c arduino -P {port} -b 115200 -n",
        cwd: sketchDir, timeoutMs: 10000);
    if (rc3 == 0 && out3.ToLowerInvariant().Contains("0x1e950f"))
    {
        Console.WriteLine(" ATmega328P (Nano)");
        return "m328p";
    }
    // Try Uno baud rate
    var (rc4, out4) = RunProcess(pio,
        $"pkg exec -- avrdude -p m328p -c arduino -P {port} -b 115200 -n",
        cwd: sketchDir, timeoutMs: 10000);
    if (rc4 == 0 && out4.Contains("0x1e950f"))
    {
        Console.WriteLine(" ATmega328P (Uno)");
        return "atmega328p";
    }

    // 4. Check if it responds as RP2040 (no standard probe — rely on USB descriptor)
    if (output.ToLowerInvariant().Contains("rp2040") ||
        output.ToLowerInvariant().Contains("raspberry"))
    {
        Console.WriteLine(" RP2040");
        return "rp2040";
    }

    Console.WriteLine(" Unknown");
    return null;
}

// ─── Serial communication (minimal CmdMessenger text protocol) ───────────────
class MiniMessenger : IDisposable
{
    private SerialPort _sp;
    private StringBuilder _buf = new();

    public MiniMessenger(string port, int baud = 115200)
    {
        _sp = new SerialPort(port, baud, Parity.None, 8, StopBits.One)
        {
            DtrEnable = true,
            RtsEnable = true,
            ReadTimeout = 100,
            WriteTimeout = 500,
            Encoding = Encoding.GetEncoding("ISO-8859-1"),
        };
    }

    public void Open() => _sp.Open();

    /// <summary>Wait for a command with a specific ID. Returns raw CSV fields or null on timeout.</summary>
    public string[] WaitForCmd(int cmdId, int timeoutMs)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            try
            {
                int b = _sp.ReadByte();
                if (b < 0) continue;
                char c = (char)b;
                if (c == ';')
                {
                    var line = _buf.ToString().Trim();
                    _buf.Clear();
                    if (string.IsNullOrEmpty(line)) continue;
                    var parts = SplitCmd(line);
                    if (parts.Length > 0 && int.TryParse(parts[0], out int id) && id == cmdId)
                        return parts;
                }
                else
                {
                    _buf.Append(c);
                }
            }
            catch (TimeoutException) { }
        }
        return null;
    }

    /// <summary>Send a command (text mode).</summary>
    public void SendCmd(int cmdId, params string[] args)
    {
        var sb = new StringBuilder();
        sb.Append(cmdId);
        foreach (var a in args)
        {
            sb.Append(',');
            sb.Append(a);
        }
        sb.Append(";\n");
        var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(sb.ToString());
        _sp.Write(bytes, 0, bytes.Length);
    }

    /// <summary>Drain and discard all pending input.</summary>
    public void Drain()
    {
        _sp.DiscardInBuffer();
        _buf.Clear();
    }

    private static string[] SplitCmd(string line)
    {
        // Simple split on ',' respecting escape char '/'
        var result = new List<string>();
        var cur = new StringBuilder();
        bool escaped = false;
        foreach (char c in line)
        {
            if (escaped) { cur.Append(c); escaped = false; continue; }
            if (c == '/') { escaped = true; continue; }
            if (c == ',') { result.Add(cur.ToString()); cur.Clear(); continue; }
            cur.Append(c);
        }
        result.Add(cur.ToString());
        return result.ToArray();
    }

    public void Dispose() { try { _sp?.Close(); } catch { } _sp?.Dispose(); }
}

// ─── Main provisioning loop ──────────────────────────────────────────────────
var pio = ResolvePio();
if (pio == null)
{
    Console.Error.WriteLine("ERROR: PlatformIO (pio) not found. Install it first.");
    Environment.Exit(1);
}
Console.WriteLine($"PlatformIO: {pio}");
Console.WriteLine($"Sketch dir: {sketchDir}");
Console.WriteLine($"Registry:   {registryPath}");
Console.WriteLine();

while (true)
{
    var registry = LoadRegistry();

    var currentPorts = GetUsbSerialPorts();
    Console.WriteLine($"Current USB-serial ports: {(currentPorts.Length > 0 ? string.Join(", ", currentPorts) : "(none)")}");
    Console.WriteLine();
    Console.WriteLine("Plug in a board to provision (or Ctrl-C to quit)...");

    // Wait for a new port to appear
    string newPort = null;
    while (newPort == null)
    {
        Thread.Sleep(500);
        var nowPorts = GetUsbSerialPorts();
        var added = nowPorts.Except(currentPorts).ToArray();
        if (added.Length > 0)
        {
            newPort = added[0];
            if (added.Length > 1)
                Console.WriteLine($"  Multiple new ports detected ({string.Join(", ", added)}), using {newPort}");
        }
    }

    Console.WriteLine($"\n  New port detected: {newPort}");
    Thread.Sleep(1500); // let the port settle (USB enumeration + driver init)

    // Detect chip
    var chip = DetectChip(newPort, pio);
    if (chip == null)
    {
        Console.WriteLine("  Could not identify chip. Skipping.");
        Console.WriteLine("  (Press Enter to try next board)");
        Console.ReadLine();
        continue;
    }

    if (!boardDefs.TryGetValue(chip, out var def))
    {
        Console.WriteLine($"  Chip '{chip}' has no matching board definition. Skipping.");
        Console.ReadLine();
        continue;
    }

    Console.WriteLine($"  Board type: {def.ModelPrefix} (PIO env: {def.PioEnv})");

    // Flash the sketch
    Console.Write($"  Flashing LoopbackTestRunner ({def.PioEnv})...");
    var (flashRc, flashOut) = RunProcess(pio,
        $"run -e {def.PioEnv} --target upload --upload-port {newPort}",
        cwd: sketchDir, timeoutMs: 120000);
    if (flashRc != 0)
    {
        Console.WriteLine(" FAILED");
        Console.WriteLine(flashOut);
        Console.WriteLine("  (Press Enter to try next board)");
        Console.ReadLine();
        continue;
    }
    Console.WriteLine(" OK");

    // Wait for board to reboot and send boot ack
    Thread.Sleep(2000);

    // Open serial and check identity
    using (var msg = new MiniMessenger(newPort))
    {
        msg.Open();
        Thread.Sleep(500);
        msg.Drain();

        // In case we missed the boot ack, send a ping to wake it up
        // Then query identity
        msg.SendCmd(kWhoAmI);
        var whoReply = msg.WaitForCmd(kWhoAmIResult, 5000);

        string existingModel = null;
        string existingId = null;

        if (whoReply != null && whoReply.Length >= 3)
        {
            existingModel = whoReply[1];
            existingId = whoReply[2];
        }

        if (existingModel == "UNPROVISIONED" || string.IsNullOrEmpty(existingModel))
        {
            Console.WriteLine("  Status: UNPROVISIONED");
        }
        else
        {
            Console.WriteLine($"  Current identity: {existingModel}-{existingId}");
            Console.Write("  Re-provision? (y/N): ");
            var ans = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (ans != "y" && ans != "yes")
            {
                // Update registry with current port info
                var key = $"{existingModel}-{existingId}";
                registry[key] = new BoardEntry
                {
                    Model = existingModel,
                    Id = existingId,
                    ChipInfo = chip,
                    LastPort = newPort,
                    ProvisionedAt = registry.ContainsKey(key) ? registry[key].ProvisionedAt : DateTime.UtcNow,
                };
                SaveRegistry(registry);
                Console.WriteLine($"  Kept existing identity. Registry updated with port {newPort}.");
                Console.WriteLine();
                continue;
            }
        }

        // Generate next ID for this model
        int maxSeq = 0;
        foreach (var entry in registry.Values.Where(e => e.Model == def.ModelPrefix))
        {
            if (int.TryParse(entry.Id, out int seq) && seq > maxSeq)
                maxSeq = seq;
        }
        int nextSeq = maxSeq + 1;
        string newId = nextSeq.ToString("D3"); // e.g. "001", "002"

        Console.Write($"  Assigning identity: {def.ModelPrefix}-{newId} ... ");

        // Send SetId command
        msg.SendCmd(kSetId, def.ModelPrefix, newId);
        var setReply = msg.WaitForCmd(kSetIdResult, 5000);

        if (setReply == null || setReply.Length < 3)
        {
            Console.WriteLine("FAILED (no response to kSetId)");
            Console.ReadLine();
            continue;
        }

        // Verify
        msg.SendCmd(kWhoAmI);
        var verifyReply = msg.WaitForCmd(kWhoAmIResult, 3000);
        if (verifyReply != null && verifyReply.Length >= 3 &&
            verifyReply[1] == def.ModelPrefix && verifyReply[2] == newId)
        {
            Console.WriteLine("OK (verified)");
        }
        else
        {
            Console.WriteLine($"WARNING: Verification read back {verifyReply?[1]}-{verifyReply?[2]}");
        }

        // Update registry
        var regKey = $"{def.ModelPrefix}-{newId}";
        registry[regKey] = new BoardEntry
        {
            Model = def.ModelPrefix,
            Id = newId,
            ChipInfo = chip,
            LastPort = newPort,
            ProvisionedAt = DateTime.UtcNow,
        };
        SaveRegistry(registry);
        Console.WriteLine($"  Saved to registry: {registryPath}");
    }

    Console.WriteLine();
}
