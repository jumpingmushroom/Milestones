using System;
using System.Globalization;
using System.Text;

namespace Milestones.Core.Model
{
    public static class Labels
    {
        /// <summary>Tick used in toasts. If the game font lacks the glyph (checked in Task 11), change it here only.</summary>
        public const string Check = "✓";

        /// <summary>
        /// Readable fallback for a key with no translation. "$item_serpentstew" → "Serpentstew",
        /// "MaxBuildingHeight" → "Max building height".
        /// </summary>
        public static string Humanize(string key)
        {
            if (string.IsNullOrEmpty(key))
                return "";
            string s = key.TrimStart('$');
            int us = s.IndexOf('_');
            if (key.StartsWith("$") && us >= 0 && us < s.Length - 1)
                s = s.Substring(us + 1);
            var sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '_')
                {
                    sb.Append(' ');
                    continue;
                }
                if (i > 0 && char.IsUpper(c) && char.IsLower(s[i - 1]))
                    sb.Append(' ');
                sb.Append(sb.Length == 0 ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        public static string Amount(float v)
        {
            if (Math.Abs(v) >= 100f)
                return Math.Round(v).ToString("0", CultureInfo.InvariantCulture);
            return v.ToString("0.#", CultureInfo.InvariantCulture);
        }

        /// <summary>Floored, so an achievement one step short never reads 100%.</summary>
        public static string Percent(float fraction)
        {
            float f = fraction < 0f ? 0f : fraction > 1f ? 1f : fraction;
            return ((int)Math.Floor(f * 100f + 1e-4f)).ToString(CultureInfo.InvariantCulture) + "%";
        }

        public static string ProgressText(Objective o)
        {
            if (!o.HasBar)
                return o.Met ? "met" : "not met";
            float shown = Math.Min(o.Current, o.Target);
            return Amount(shown) + " / " + Amount(o.Target) + "  " + Percent(o.Fraction);
        }

        public static string OverallText(AchievementProgress p)
        {
            return p.ShowAsCount ? p.MetCount + " / " + p.Total : Percent(p.Overall);
        }
    }
}
