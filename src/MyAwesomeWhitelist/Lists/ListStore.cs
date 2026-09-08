using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;

namespace MyAwesomeWhitelist.Lists
{
    /// <summary>
    /// Loads/saves a <see cref="NameList"/> as a minimal hand-rolled JSON array:
    /// <code>[{"id":"7656...","name":"Player","at":"2026-08-16 12:00:00"}]</code>
    /// SteamIDs are digits only, so only the name field needs escaping (" \ and
    /// control chars). Writes are atomic (.tmp then move); a corrupt file is
    /// backed up as .bak and an empty list is started from.
    /// </summary>
    internal static class ListStore
    {
        private static string Dir
        {
            get { return Path.Combine(Paths.ConfigPath, PluginInfo.PLUGIN_NAME); }
        }

        public static string FilePath(string fileName)
        {
            return Path.Combine(Dir, fileName);
        }

        public static NameList Load(string fileName)
        {
            var list = new NameList();
            try
            {
                string path = FilePath(fileName);
                if (!File.Exists(path))
                {
                    return list;
                }
                string json = File.ReadAllText(path, Encoding.UTF8);
                foreach (var entry in Parse(json))
                {
                    list.Add(entry);
                }
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning($"Failed to read {fileName}: {e.Message} — starting from an empty list");
                try
                {
                    string src = FilePath(fileName);
                    if (File.Exists(src))
                    {
                        File.Copy(src, src + ".bak", true);
                    }
                }
                catch { /* best effort backup */ }
            }
            return list;
        }

        public static void Save(string fileName, NameList list)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string path = FilePath(fileName);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, Serialize(list), Encoding.UTF8);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"Failed to save {fileName}: {e.Message}");
            }
        }

        // ── Serialization ─────────────────────────────────────────────

        private static string Serialize(NameList list)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            var entries = list.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                sb.Append("{\"id\":");
                sb.Append(Quote(entries[i].SteamId));
                sb.Append(",\"name\":");
                sb.Append(Quote(entries[i].Name));
                sb.Append(",\"at\":");
                sb.Append(Quote(entries[i].AddedAt));
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string Quote(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"' || c == '\\')
                {
                    sb.Append('\\').Append(c);
                }
                else if (c == '\n')
                {
                    sb.Append("\\n");
                }
                else if (c == '\r')
                {
                    sb.Append("\\r");
                }
                else if (c == '\t')
                {
                    sb.Append("\\t");
                }
                else if (c < ' ')
                {
                    sb.Append("\\u").Append(((int)c).ToString("x4"));
                }
                else
                {
                    sb.Append(c);
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        // ── Parsing ───────────────────────────────────────────────────

        private static IEnumerable<ListEntry> Parse(string json)
        {
            int i = SkipWs(json, 0);
            if (i >= json.Length || json[i] != '[')
            {
                throw new FormatException("expected '[' at start");
            }
            i = SkipWs(json, i + 1);
            while (i < json.Length && json[i] != ']')
            {
                string id = null, name = null, at = null;
                i = SkipWs(json, i);
                if (json[i] != '{')
                {
                    throw new FormatException("expected '{'");
                }
                i = SkipWs(json, i + 1);
                while (i < json.Length && json[i] != '}')
                {
                    string key = ReadString(json, ref i);
                    i = SkipWs(json, i);
                    if (json[i] != ':')
                    {
                        throw new FormatException("expected ':'");
                    }
                    i = SkipWs(json, i + 1);
                    string value = ReadString(json, ref i);
                    switch (key)
                    {
                        case "id": id = value; break;
                        case "name": name = value; break;
                        case "at": at = value; break;
                    }
                    i = SkipWs(json, i);
                    if (i < json.Length && json[i] == ',')
                    {
                        i = SkipWs(json, i + 1);
                    }
                }
                if (id == null)
                {
                    throw new FormatException("entry missing \"id\"");
                }
                yield return new ListEntry(id, name, at);
                i = SkipWs(json, i + 1);
                if (i < json.Length && json[i] == ',')
                {
                    i = SkipWs(json, i + 1);
                }
            }
        }

        private static int SkipWs(string s, int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r'))
            {
                i++;
            }
            if (i >= s.Length)
            {
                throw new FormatException("unexpected end of file");
            }
            return i;
        }

        private static string ReadString(string s, ref int i)
        {
            if (s[i] != '"')
            {
                throw new FormatException("expected '\"' at " + i);
            }
            i++;
            var sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    i++;
                    char e = s[i];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 < s.Length)
                            {
                                sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16));
                                i += 4;
                            }
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
                i++;
            }
            if (i >= s.Length)
            {
                throw new FormatException("unterminated string");
            }
            i++; // closing quote
            return sb.ToString();
        }
    }
}
