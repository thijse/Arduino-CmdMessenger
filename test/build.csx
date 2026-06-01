#!/usr/bin/env dotnet-script
// build.csx — One-shot CI-style build for CmdMessenger.
//
// Builds (in order):
//   1. C#   solution  : extras/CSharp/CmdMessenger.sln
//   2. VB   solution  : extras/VisualBasic/CmdMessengerVB.sln
//   3. Arduino sketches under examples/ for the Arduino Nano, using both
//      arduino-cli and PlatformIO (whichever is installed; both if both).
//
// Run from the repository root:
//
//     dotnet script test/build.csx
//
// Optional args:
//     --skip-csharp        skip the C# solution
//     --skip-vb            skip the VB solution
//     --skip-arduino-cli   skip arduino-cli sketch builds
//     --skip-pio           skip PlatformIO sketch builds
//     --fqbn <fqbn>        override the arduino-cli FQBN (default: arduino:avr:nano)
//     --board <board>      override the PlatformIO board ID (default: nanoatmega328)
//     --sketch <name>      build only the named sketch (folder name under examples/)
//
// Exit code is non-zero if any step fails. A summary table is printed at the end.

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

// ─── Argument parsing ────────────────────────────────────────────────────────
var argList = Args.ToList();
bool skipCSharp     = argList.Remove("--skip-csharp");
bool skipVb         = argList.Remove("--skip-vb");
bool skipArduinoCli = argList.Remove("--skip-arduino-cli");
bool skipPio        = argList.Remove("--skip-pio");

string TakeValue(string flag, string defaultValue)
{
    var i = argList.IndexOf(flag);
    if (i < 0) return defaultValue;
    if (i + 1 >= argList.Count) throw new ArgumentException($"{flag} needs a value");
    var v = argList[i + 1];
    argList.RemoveRange(i, 2);
    return v;
}

string fqbn       = TakeValue("--fqbn",   "arduino:avr:nano");
string pioBoard   = TakeValue("--board",  "nanoatmega328");
string? oneSketch = argList.IndexOf("--sketch") is int si && si >= 0
                    ? TakeValue("--sketch", "") : null;

if (argList.Count > 0)
{
    Console.Error.WriteLine("Unknown args: " + string.Join(" ", argList));
    Environment.Exit(2);
}

// ─── Locate repo root ────────────────────────────────────────────────────────
string ScriptDir() => Path.GetDirectoryName(GetScriptPath()) ?? Environment.CurrentDirectory;
string GetScriptPath()
{
    // dotnet-script exposes script path via the call stack; fall back to cwd.
    var asm = System.Reflection.Assembly.GetEntryAssembly();
    return asm?.Location ?? Environment.CurrentDirectory;
}

string repoRoot = Path.GetFullPath(Path.Combine(ScriptDir(), ".."));
// If invoked from the repo root, ScriptDir() points elsewhere; prefer cwd if it
// looks like the repo root.
if (File.Exists(Path.Combine(Environment.CurrentDirectory, "CmdMessenger.h")))
    repoRoot = Environment.CurrentDirectory;

Console.WriteLine($"Repo root: {repoRoot}");
Console.WriteLine();

// ─── Helpers ─────────────────────────────────────────────────────────────────
record StepResult(string Name, bool Ok, string Detail);
var results = new List<StepResult>();

bool ToolExists(string exe)
{
    try
    {
        var psi = new ProcessStartInfo(exe, "--version")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit(5000);
        return p.ExitCode == 0;
    }
    catch { return false; }
}

int Run(string exe, string args, string? cwd = null, bool quiet = false)
{
    var psi = new ProcessStartInfo(exe, args)
    {
        WorkingDirectory       = cwd ?? repoRoot,
        UseShellExecute        = false,
        RedirectStandardOutput = quiet,
        RedirectStandardError  = quiet,
    };
    Console.WriteLine($"> {exe} {args}");
    using var p = Process.Start(psi)!;
    if (quiet)
    {
        p.OutputDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine("  " + e.Data); };
        p.ErrorDataReceived  += (_, e) => { if (e.Data != null) Console.Error.WriteLine("  " + e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
    }
    p.WaitForExit();
    return p.ExitCode;
}

void Step(string name, Func<int> action)
{
    Console.WriteLine($"\n=== {name} ===");
    int code;
    try { code = action(); }
    catch (Exception ex)
    {
        results.Add(new StepResult(name, false, ex.Message));
        Console.Error.WriteLine($"  ! {ex.Message}");
        return;
    }
    results.Add(new StepResult(name, code == 0, code == 0 ? "ok" : $"exit {code}"));
}

// ─── 1. C# solution ──────────────────────────────────────────────────────────
if (!skipCSharp)
{
    var sln = Path.Combine(repoRoot, "extras", "CSharp", "CmdMessenger.sln");
    if (!File.Exists(sln))
        results.Add(new StepResult("C# solution", false, "sln not found"));
    else if (!ToolExists("dotnet"))
        results.Add(new StepResult("C# solution", false, "dotnet not installed"));
    else
        Step("C# solution", () => Run("dotnet", $"build \"{sln}\" -nologo -v quiet"));
}

// ─── 2. VB solution ──────────────────────────────────────────────────────────
if (!skipVb)
{
    var sln = Path.Combine(repoRoot, "extras", "VisualBasic", "CmdMessengerVB.sln");
    if (!File.Exists(sln))
        results.Add(new StepResult("VB solution", false, "sln not found"));
    else if (!ToolExists("dotnet"))
        results.Add(new StepResult("VB solution", false, "dotnet not installed"));
    else
        Step("VB solution", () => Run("dotnet", $"build \"{sln}\" -nologo -v quiet"));
}

// ─── 3. Arduino sketches ─────────────────────────────────────────────────────
var examplesDir = Path.Combine(repoRoot, "examples");
var sketches = Directory.Exists(examplesDir)
    ? Directory.GetDirectories(examplesDir)
        .Where(d => Directory.GetFiles(d, "*.ino").Any())
        .Where(d => oneSketch == null || Path.GetFileName(d).Equals(oneSketch, StringComparison.OrdinalIgnoreCase))
        .OrderBy(d => d)
        .ToList()
    : new List<string>();

if (sketches.Count == 0)
    Console.WriteLine("\n(no Arduino sketches matched)");

// 3a. arduino-cli
if (!skipArduinoCli && sketches.Count > 0)
{
    if (!ToolExists("arduino-cli"))
        results.Add(new StepResult("arduino-cli", false, "not installed (skip with --skip-arduino-cli)"));
    else
    {
        foreach (var sketch in sketches)
        {
            var name = Path.GetFileName(sketch);
            Step($"arduino-cli: {name} ({fqbn})",
                 () => Run("arduino-cli",
                           $"compile --fqbn {fqbn} --library \"{repoRoot}\" \"{sketch}\"",
                           quiet: true));
        }
    }
}

// 3b. PlatformIO
if (!skipPio && sketches.Count > 0)
{
    if (!ToolExists("pio") && !ToolExists("platformio"))
        results.Add(new StepResult("PlatformIO", false, "pio not installed (skip with --skip-pio)"));
    else
    {
        var pio = ToolExists("pio") ? "pio" : "platformio";
        foreach (var sketch in sketches)
        {
            var name = Path.GetFileName(sketch);
            Step($"pio ci: {name} ({pioBoard})",
                 () => Run(pio,
                           $"ci --board={pioBoard} --lib=\"{repoRoot}\" \"{sketch}\"",
                           quiet: true));
        }
    }
}

// ─── Summary ─────────────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine(new string('=', 70));
Console.WriteLine("Build summary");
Console.WriteLine(new string('=', 70));

int nameWidth = Math.Max(20, results.Max(r => r.Name.Length));
foreach (var r in results)
{
    var mark = r.Ok ? "  OK " : "FAIL ";
    Console.WriteLine($"  {mark}  {r.Name.PadRight(nameWidth)}  {r.Detail}");
}

int failed  = results.Count(r => !r.Ok);
int passed  = results.Count(r =>  r.Ok);
Console.WriteLine(new string('-', 70));
Console.WriteLine($"  {passed} passed, {failed} failed");

Environment.Exit(failed == 0 ? 0 : 1);
