#!/usr/bin/env dotnet-script
#r "nuget: System.IO.Ports, 8.0.0"
using System.IO.Ports;
using System.Text;
using System.Threading;

string[] ports = new[] { "COM13", "COM12", "COM14" };
string[] labels = new[] { "Nano", "ESP32-S3", "ESP8266" };

for (int i = 0; i < ports.Length; i++)
{
    try
    {
        var sp = new SerialPort(ports[i], 115200) { DtrEnable = true, RtsEnable = true, ReadTimeout = 100 };
        sp.Open();
        Thread.Sleep(2500); // wait for boot
        sp.DiscardInBuffer();
        sp.Write("18;\n"); // kWhoAmI = 18
        Thread.Sleep(500);
        var sb = new StringBuilder();
        try { while (true) { int b = sp.ReadByte(); if (b < 0) break; sb.Append((char)b); } } catch {}
        sp.Close();
        Console.WriteLine($"{labels[i]} ({ports[i]}): {sb.ToString().Trim()}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{labels[i]} ({ports[i]}): ERROR - {ex.Message}");
    }
}
