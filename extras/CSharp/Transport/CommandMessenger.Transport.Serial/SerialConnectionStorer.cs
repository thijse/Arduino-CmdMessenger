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
using System.IO;
using System.Text;

namespace CommandMessenger.Transport.Serial
{
    /// <summary>
    /// JSON-file backed implementation of <see cref="ISerialConnectionStorer"/>.
    /// Replaces the legacy BinaryFormatter implementation.
    /// Default file: SerialConnectionManagerSettings.json in the current directory.
    /// </summary>
    public class SerialConnectionStorer : ISerialConnectionStorer
    {
        private readonly string _settingsFileName;

        public SerialConnectionStorer()
        {
            _settingsFileName = "SerialConnectionManagerSettings.json";
        }

        public SerialConnectionStorer(string settingsFileName)
        {
            _settingsFileName = settingsFileName;
        }

        public void StoreSettings(SerialConnectionManagerSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            var dir = Path.GetDirectoryName(Path.GetFullPath(_settingsFileName));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"Port\": " + JsonString(settings.Port) + ",");
            json.AppendLine("  \"BaudRate\": " + settings.BaudRate);
            json.AppendLine("}");
            File.WriteAllText(_settingsFileName, json.ToString(), Encoding.UTF8);
        }

        public SerialConnectionManagerSettings RetrieveSettings()
        {
            var result = new SerialConnectionManagerSettings();
            if (!File.Exists(_settingsFileName)) return result;
            try
            {
                var text = File.ReadAllText(_settingsFileName, Encoding.UTF8);
                result.Port = ReadJsonString(text, "Port");
                var baudStr = ReadJsonNumber(text, "BaudRate");
                int baud;
                if (int.TryParse(baudStr, out baud)) result.BaudRate = baud;
            }
            catch { }
            return result;
        }

        // Minimal JSON helpers — avoids any external dependency on net4.0.

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

        private static string ReadJsonNumber(string json, string key)
        {
            var search = "\"" + key + "\"";
            var idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx = json.IndexOf(':', idx + search.Length);
            if (idx < 0) return null;
            idx++;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t' || json[idx] == '\r' || json[idx] == '\n')) idx++;
            var end = idx;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            return json.Substring(idx, end - idx);
        }
    }
}
