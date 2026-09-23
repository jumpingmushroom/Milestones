using Milestones.Core.Model;
using TMPro;
using UnityEngine;

namespace Milestones.UI
{
    internal static class UiUtil
    {
        private static Sprite _white;

        /// <summary>A filled Image needs a sprite; with none it draws a plain quad and ignores the fill.</summary>
        public static Sprite White
        {
            get
            {
                if (_white != null)
                    return _white;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++)
                    px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply();
                tex.hideFlags = HideFlags.HideAndDontSave;
                _white = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                _white.hideFlags = HideFlags.HideAndDontSave;
                return _white;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        /// <summary>The HUD's own font, so the tracker matches the game.</summary>
        public static TMP_FontAsset Font
        {
            get
            {
                Hud hud = Hud.instance;
                return hud != null && hud.m_healthText != null ? hud.m_healthText.font : null;
            }
        }

        public static TextMeshProUGUI Text(Transform parent, string name, float size, TextAlignmentOptions align)
        {
            RectTransform rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = Font;
            if (font != null)
                t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            t.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            return t;
        }

        public static Color BarColor(Objective o)
        {
            return o.Met ? PluginConfig.ColorDone.Value : o.Started ? PluginConfig.ColorInProgress.Value : PluginConfig.ColorNotStarted.Value;
        }

        public static Color BarColor(float fraction, bool done)
        {
            return done ? PluginConfig.ColorDone.Value : fraction > 0f ? PluginConfig.ColorInProgress.Value : PluginConfig.ColorNotStarted.Value;
        }

        public static Color TextColor(bool done)
        {
            return done ? PluginConfig.ColorDone.Value : new Color(0.85f, 0.85f, 0.85f, 1f);
        }
    }
}
