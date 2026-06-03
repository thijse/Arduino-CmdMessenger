#!/usr/bin/env dotnet-script
#r "nuget: System.IO.Ports, 8.0.0"
using System.IO.Ports;
using System.Text;
using System.Threading;

// Provision all three boards with identities
var boards = new[] {
    ("COM13", "NANO", "001"),
    ("COM12", "ESP32S3", "001"),
    ("COM14", "ESP8266", "001"),
};

foreach (var (port, model, id) in boards)
{
    try
    {
        var sp = new SerialPort(port, 115200) { DtrEnable = true, RtsEnable = true, ReadTimeout = 100 };
        sp.Open();
        Thread.Sleep(2500);
        sp.DiscardInBuffer();

        // Send kSetId (20) with model and id
        sp.Write($"20,{model},{id};\n");
        Thread.Sleep(500);

        // Read SetId response
        var sb = new StringBuilder();
        try { while (true) { int b = sp.ReadByte(); if (b < 0) break; sb.Append((char)b); } } catch {}
        Console.WriteLine($"SetId  {port} ({model}-{id}): {sb.ToString().Trim()}");

        // Verify with WhoAmI
        sp.DiscardInBuffer();
        sp.Write("18;\n");
        Thread.Sleep(500);
        sb.Clear();
        try { while (true) { int b = sp.ReadByte(); if (b < 0) break; sb.Append((char)b); } } catch {}
        Console.WriteLine($"WhoAmI {port}: {sb.ToString().Trim()}");

        sp.Close();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{port}: ERROR - {ex.Message}");
    }
    Console.WriteLine();
}
