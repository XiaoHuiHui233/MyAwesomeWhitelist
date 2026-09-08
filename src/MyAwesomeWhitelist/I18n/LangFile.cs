using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MyAwesomeWhitelist.I18n
{
    /// <summary>
    /// One parsed localization entry: key and value.
    /// </summary>
    internal readonly struct LangEntry
    {
        public readonly string Key;
        public readonly string Value;

        public LangEntry(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }

    /// <summary>
    /// Parser for one localization file. Format per line:
    /// <code>KEY:Translation</code>
    /// Rules: '#' lines are comments; blank lines ignored; everything after the first ':'
    /// is the value. '\n' escape becomes newline. Keys must match ^[A-Z][A-Z0-9_]*$.
    /// UTF-8 assumed. Malformed lines are skipped with a warning.
    /// </summary>
    internal static class LangFile
    {
        private static readonly Regex KeyPattern = new Regex(@"^[A-Z][A-Z0-9_]*$");

        /// <summary>Parse a file into entries. Returns empty if missing.</summary>
        public static List<LangEntry> Parse(string filePath, out string displayName)
        {
            displayName = null;
            var result = new List<LangEntry>();
            if (!File.Exists(filePath))
                return result;

            string[] raw;
            try { raw = File.ReadAllLines(filePath, Encoding.UTF8); }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[I18n] Failed to read {Path.GetFileName(filePath)}: {ex.Message}");
                return result;
            }

            return ParseLines(raw, Path.GetFileName(filePath), out displayName);
        }

        /// <summary>Parse in-memory lines.</summary>
        public static List<LangEntry> ParseLines(IList<string> lines, string sourceName, out string displayName)
        {
            displayName = null;
            var result = new List<LangEntry>();
            if (lines == null) return result;

            for (int i = 0; i < lines.Count; i++)
            {
                int lineNo = i + 1;
                string raw = lines[i];
                string line = raw.Trim();

                if (line.Length == 0) continue;
                if (line[0] == '#') continue;

                int colon = line.IndexOf(':');
                if (colon <= 0)
                {
                    Plugin.Logger.LogWarning($"[I18n] {sourceName}({lineNo}): expected 'KEY:Translation', skipping.");
                    continue;
                }

                string key = line.Substring(0, colon).Trim();
                string value = Unescape(line.Substring(colon + 1));

                if (key == "__LANG_NAME__")
                {
                    displayName = value;
                    continue;
                }

                if (!KeyPattern.IsMatch(key))
                {
                    Plugin.Logger.LogWarning($"[I18n] {sourceName}({lineNo}): invalid key '{key}', skipping.");
                    continue;
                }

                result.Add(new LangEntry(key, value));
            }
            return result;
        }

        private static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[i + 1];
                    switch (n)
                    {
                        case 'n': sb.Append('\n'); i++; break;
                        case 't': sb.Append('\t'); i++; break;
                        case '\\': sb.Append('\\'); i++; break;
                        case ':': sb.Append(':'); i++; break;
                        default: sb.Append(c); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
