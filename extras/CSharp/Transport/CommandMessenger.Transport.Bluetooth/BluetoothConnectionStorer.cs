#region CmdMessenger - MIT - (c) 2014 Thijs Elenbaas.
/*
  CmdMessenger - library that provides command based messaging

  Permission is hereby granted, free of charge, to any person obtaining
  a copy of this software and associated documentation files (the
  "Software"), to deal in the Software without restriction, including
  without limitation the rights to use, copy, modify, merge, publish,
  distribute, sublicense, and/or sell copies of the Software, and to
  permit persons to whom the Software is furnished to do so, subject to
  the following conditions:

  The above copyright notice and this permission notice shall be
  included in all copies or substantial portions of the Software.

  Copyright 2014 - Thijs Elenbaas
*/
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using InTheHand.Net;

namespace CommandMessenger.Transport.Bluetooth
{
    /// <summary>
    /// JSON-file backed implementation of <see cref="IBluetoothConnectionStorer"/>.
    /// Replaces the legacy BinaryFormatter implementation.
    /// Default file: BluetoothConnectionManagerSettings.json in the current directory.
    /// </summary>
    public class BluetoothConnectionStorer : IBluetoothConnectionStorer
    {
        private readonly string _settingsFileName;

        public BluetoothConnectionStorer()
        {
            _settingsFileName = "BluetoothConnectionManagerSettings.json";
        }

        public BluetoothConnectionStorer(string settingsFileName)
        {
            _settingsFileName = settingsFileName;
        }

        public void StoreSettings(BluetoothConnectionManagerSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            var dir = Path.GetDirectoryName(Path.GetFullPath(_settingsFileName));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = new StringBuilder();
            json.AppendLine("{");
            var addr = settings.BluetoothAddress != null ? settings.BluetoothAddress.ToString() : "";
            json.AppendLine("  \"BluetoothAddress\": " + JsonString(addr) + ",");
            json.AppendLine("  \"StoredDevicePins\": {");
            if (settings.StoredDevicePins != null)
            {
                var pins = new List<string>();
                foreach (var kv in settings.StoredDevicePins)
                    pins.Add("    " + JsonString(kv.Key.ToString()) + ": " + JsonString(kv.Value));
                json.AppendLine(string.Join("," + Environment.NewLine, pins));
            }
            json.AppendLine("  }");
            json.AppendLine("}");
            File.WriteAllText(_settingsFileName, json.ToString(), Encoding.UTF8);
        }

        public BluetoothConnectionManagerSettings RetrieveSettings()
        {
            var result = new BluetoothConnectionManagerSettings();
            if (!File.Exists(_settingsFileName)) return result;
            try
            {
                var text = File.ReadAllText(_settingsFileName, Encoding.UTF8);

                var addrStr = ReadJsonString(text, "BluetoothAddress");
                if (!string.IsNullOrEmpty(addrStr))
                {
                    BluetoothAddress addr;
                    if (BluetoothAddress.TryParse(addrStr, out addr))
                        result.BluetoothAddress = addr;
                }

                // Parse StoredDevicePins object
                var pinsStart = text.IndexOf("\"StoredDevicePins\"", StringComparison.Ordinal);
                if (pinsStart >= 0)
                {
                    var braceOpen = text.IndexOf('{', pinsStart);
                    var braceClose = text.IndexOf('}', braceOpen + 1);
                    if (braceOpen >= 0 && braceClose > braceOpen)
                    {
                        var pinsBlock = text.Substring(braceOpen + 1, braceClose - braceOpen - 1);
                        var pos = 0;
                        while (pos < pinsBlock.Length)
                        {
                            var keyStart = pinsBlock.IndexOf('"', pos);
                            if (keyStart < 0) break;
                            var keyEnd = pinsBlock.IndexOf('"', keyStart + 1);
                            if (keyEnd < 0) break;
                            var key = pinsBlock.Substring(keyStart + 1, keyEnd - keyStart - 1);
                            var colon = pinsBlock.IndexOf(':', keyEnd + 1);
                            if (colon < 0) break;
                            var valStart = pinsBlock.IndexOf('"', colon + 1);
                            if (valStart < 0) break;
                            var valEnd = pinsBlock.IndexOf('"', valStart + 1);
                            if (valEnd < 0) break;
                            var val = pinsBlock.Substring(valStart + 1, valEnd - valStart - 1);
                            BluetoothAddress pinAddr;
                            if (BluetoothAddress.TryParse(key, out pinAddr))
                                result.StoredDevicePins[pinAddr] = val;
                            pos = valEnd + 1;
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        private static string JsonString(string value)
        {
            if (value == null) return "null";
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string ReadJsonString(string json, string key)
        {
            var search = "\"" + key + "\"";
            var idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx = json.IndexOf(':', idx + search.Length);
            if (idx < 0) return null;
            idx = json.IndexOf('"', idx + 1);
            if (idx < 0) return null;
            var end = json.IndexOf('"', idx + 1);
            if (end < 0) return null;
            return json.Substring(idx + 1, end - idx - 1)
                       .Replace("\\\"", "\"")
                       .Replace("\\\\", "\\");
        }
    }
}
