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

        private static Material _outlined;
        private static TMP_FontAsset _outlinedFor;
        private static float _outlinedWidth;

        /// <summary>
        /// The material for our labels: one shared copy of the font's material with a black outline,
        /// so HUD text stays readable against a bright sky or snow. Shared rather than per label,
        /// which would make a material instance for every text object. TMP centres the outline on
        /// the glyph edge, so half of it eats into the face; the face is dilated by the full width to
        /// put the edge back and grow the outline outward instead. Without that, small text keeps a
        /// sub-pixel face and reads as black. <c>TextOutline</c> 0 uses the font's own material.
        /// </summary>
        public static Material TextMaterial(TMP_FontAsset font)
        {
            float width = PluginConfig.TextOutline.Value;
            if (width <= 0f)
                return font.material;
            if (_outlined != null && _outlinedFor == font && _outlinedWidth == width)
                return _outlined;
            if (_outlined != null)
                Object.Destroy(_outlined);
            _outlined = new Material(font.material);
            _outlined.EnableKeyword(ShaderUtilities.Keyword_Outline);
            _outlined.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            _outlined.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 1f));
            if (_outlined.HasProperty(ShaderUtilities.ID_FaceDilate))
                _outlined.SetFloat(ShaderUtilities.ID_FaceDilate, _outlined.GetFloat(ShaderUtilities.ID_FaceDilate) + width);
            _outlined.hideFlags = HideFlags.HideAndDontSave;
            _outlinedFor = font;
            _outlinedWidth = width;
            return _outlined;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, float size, TextAlignmentOptions align)
        {
            RectTransform rt = Rect(name, parent);
            // Added while inactive so TMP's Awake runs after the font is set; otherwise it looks
            // for its default LiberationSans (not shipped with the game) and logs a warning.
            rt.gameObject.SetActive(false);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = Font;
            if (font != null)
            {
                t.font = font;
                t.fontSharedMaterial = TextMaterial(font);
            }
            rt.gameObject.SetActive(true);
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
