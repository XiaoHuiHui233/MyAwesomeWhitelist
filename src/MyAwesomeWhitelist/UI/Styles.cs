using UnityEngine;

namespace MyAwesomeWhitelist.UI
{
    /// <summary>
    /// Lazily-built IMGUI styles with a CJK-capable dynamic font. Toggle/button
    /// styles are derived from GUI.skin.toggle/button — passing a label-derived
    /// style would make the checkbox / pressed background disappear.
    /// </summary>
    internal sealed class Styles
    {
        public GUIStyle Label, Value, Section, Small, Toggle, Button, TextField, MiniButton, Header, Box, ListBox, Path;
        private static Texture2D _bgTex;
        public bool Ready { get; private set; }

        public void Ensure()
        {
            if (Ready)
            {
                return;
            }
            Font font = null;
            try
            {
                font = Font.CreateDynamicFontFromOSFont(new[]
                {
                    "Microsoft YaHei", "PingFang SC", "Heiti SC", "STHeiti",
                    "Noto Sans CJK SC", "WenQuanYi Zen Hei", "Arial Unicode MS", "Arial",
                }, 14);
            }
            catch { font = null; }
            Label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 13, wordWrap = false };
            Value = new GUIStyle(GUI.skin.label) { font = font, fontSize = 13 };
            Section = new GUIStyle(GUI.skin.label) { font = font, fontSize = 14, fontStyle = FontStyle.Bold };
            Small = new GUIStyle(GUI.skin.label) { font = font, fontSize = 11, wordWrap = true };
            Header = new GUIStyle(GUI.skin.label) { font = font, fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.72f, 0.78f, 0.9f) } };
            Toggle = new GUIStyle(GUI.skin.toggle) { font = font, fontSize = 13, wordWrap = false };
            Button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 13, wordWrap = false };
            MiniButton = new GUIStyle(GUI.skin.button) { font = font, fontSize = 11, wordWrap = false };
            TextField = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 13 };

            // Semi-transparent dark background for the panel area (BeginArea).
            _bgTex = DarkBg();
            Box = new GUIStyle(GUI.skin.box);
            Box.normal.background = _bgTex;
            Box.padding = new RectOffset(8, 8, 6, 6);

            // Bordered, slightly lighter box for list entries (white/blacklist tab).
            var listBoxBg = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            listBoxBg.SetPixel(0, 0, new Color(0.10f, 0.11f, 0.14f, 0.9f));
            listBoxBg.Apply();
            ListBox = new GUIStyle(GUI.skin.box);
            ListBox.normal.background = listBoxBg;
            ListBox.padding = new RectOffset(0, 0, 0, 0);

            // Single-line file path — no wrapping, so long paths stay on one line.
            Path = new GUIStyle(GUI.skin.label) { font = font, fontSize = 11, wordWrap = false };

            Ready = true;
        }

        private static Texture2D DarkBg()
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, new Color(0.07f, 0.08f, 0.10f, 0.94f));
            t.Apply();
            return t;
        }
    }
}
