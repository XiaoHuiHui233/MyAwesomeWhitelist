using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyAwesomeWhitelist.UI
{
    /// <summary>
    /// Parses a human-friendly hotkey spec like "Ctrl+T", "Ctrl+Shift+W" or
    /// "Alt+F6" into modifier flags + a main key, and exposes two checks:
    /// <see cref="IsDown"/> for Update (Input API — unreliable while a focused
    /// IMGUI TextField swallows key events) and <see cref="Matches(Event)"/> for
    /// OnGUI, where the swallowed keydown is still visible.
    /// "Ctrl" matches either the Control key or the Command key, so Windows and
    /// macOS players get the same spec.
    /// </summary>
    internal sealed class Hotkey
    {
        public bool Ctrl { get; private set; }
        public bool Shift { get; private set; }
        public bool Alt { get; private set; }
        public KeyCode Key { get; private set; }

        private readonly string _spec;

        public Hotkey(string spec)
        {
            _spec = (spec ?? "").Trim();
            Parse(_spec, out bool ctrl, out bool shift, out bool alt, out KeyCode key, out bool ok);
            Ctrl = ctrl;
            Shift = shift;
            Alt = alt;
            Key = key;
            Valid = ok && key != KeyCode.None;
        }

        /// <summary>True if the spec parsed into something usable.</summary>
        public bool Valid { get; }

        /// <summary>Main key pressed this frame (edge), modifiers held — for Update.</summary>
        public bool IsDown
        {
            get { return Valid && ModifiersHeld() && Input.GetKeyDown(Key); }
        }

        /// <summary>The given GUI event is a keydown of the main key with the configured modifiers.</summary>
        public bool Matches(Event ev)
        {
            if (!Valid || ev.type != EventType.KeyDown || ev.keyCode != Key)
            {
                return false;
            }
            bool ctrlHeld = ev.control || ev.command;
            return ctrlHeld == Ctrl && ev.shift == Shift && ev.alt == Alt;
        }

        public override string ToString()
        {
            return _spec;
        }

        // ── Parsing ────────────────────────────────────────────────────

        private static readonly Dictionary<string, KeyCode> NamedKeys = new Dictionary<string, KeyCode>(StringComparer.OrdinalIgnoreCase)
        {
            { "Space", KeyCode.Space }, { "Enter", KeyCode.Return }, { "Return", KeyCode.Return },
            { "KeypadEnter", KeyCode.KeypadEnter }, { "Tab", KeyCode.Tab }, { "Esc", KeyCode.Escape },
            { "Escape", KeyCode.Escape }, { "Backspace", KeyCode.Backspace }, { "Delete", KeyCode.Delete },
            { "Up", KeyCode.UpArrow }, { "Down", KeyCode.DownArrow }, { "Left", KeyCode.LeftArrow }, { "Right", KeyCode.RightArrow },
            { "Home", KeyCode.Home }, { "End", KeyCode.End }, { "PageUp", KeyCode.PageUp }, { "PageDown", KeyCode.PageDown },
            { "Insert", KeyCode.Insert },
        };

        private bool ModifiersHeld()
        {
            if (Ctrl)
            {
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                            || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
                if (!ctrl) return false;
            }
            if (Shift && !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) return false;
            if (Alt && !(Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))) return false;
            return true;
        }

        private static void Parse(string spec, out bool ctrl, out bool shift, out bool alt, out KeyCode key, out bool ok)
        {
            ctrl = shift = alt = false;
            key = KeyCode.None;
            ok = false;

            if (string.IsNullOrWhiteSpace(spec)) return;
            string[] tokens = spec.Split('+');
            for (int i = 0; i < tokens.Length; i++)
            {
                string t = tokens[i].Trim();
                if (t.Length == 0) continue;
                if (i < tokens.Length - 1)
                {
                    switch (t.ToLowerInvariant())
                    {
                        case "ctrl":
                        case "control":
                        case "cmd":
                        case "command":
                            ctrl = true; break;
                        case "shift": shift = true; break;
                        case "alt":
                        case "option": alt = true; break;
                        default:
                            return; // unknown modifier — parse failure
                    }
                }
                else
                {
                    if (t.Length == 1 && char.IsLetterOrDigit(t[0]))
                    {
                        // Unity KeyCode letters are LOWERCASE ascii; digits use the
                        // ascii code of '0'..'9' — normalise uppercase input.
                        char c = t[0];
                        if (c >= 'A' && c <= 'Z') c = (char)(c - 'A' + 'a');
                        key = (KeyCode)c;
                    }
                    else if (NamedKeys.TryGetValue(t, out KeyCode named))
                    {
                        key = named;
                    }
                    else if (Enum.TryParse(t, ignoreCase: true, out KeyCode parsed))
                    {
                        key = parsed;
                    }
                    else
                    {
                        return; // unknown key name
                    }
                }
            }
            ok = key != KeyCode.None;
        }
    }
}
