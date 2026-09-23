using System;
using System.Collections.Generic;
using System.Globalization;

namespace Milestones.Core.Model
{
    /// <summary>What a viewer last saw of one achievement.</summary>
    public sealed class Snapshot
    {
        public float Overall;
        public bool[] Met;
        public bool Unlocked;

        public static Snapshot Take(AchievementProgress p)
        {
            var met = new bool[p.Total];
            for (int i = 0; i < met.Length; i++)
                met[i] = p.Objectives[i].Met;
            return new Snapshot { Overall = p.Overall, Met = met, Unlocked = p.Unlocked };
        }
    }

    public sealed class ToastRules
    {
        public PinnedToastMode Pinned;
        public UnpinnedToastMode Unpinned;
        public int[] Thresholds = new int[0];

        /// <summary>"75, 25,x,0,100" → [25, 75]: integers 1-99, sorted, distinct; junk ignored.</summary>
        public static int[] ParseThresholds(string raw)
        {
            var set = new SortedSet<int>();
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (string part in raw.Split(','))
                {
                    int v;
                    if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) && v >= 1 && v <= 99)
                        set.Add(v);
                }
            }
            var result = new int[set.Count];
            set.CopyTo(result);
            return result;
        }
    }

    /// <summary>
    /// Compares what a viewer last saw with the current progress and returns one toast line,
    /// or null. Several objectives or thresholds crossed in one step merge into one line; an
    /// unlock returns null because the game's own popup covers it.
    /// </summary>
    public static class ToastDiff
    {
        private const string Prefix = "Milestones: ";

        public static string Diff(Snapshot before, AchievementProgress now, string name, bool pinned, ToastRules rules)
        {
            if (before == null || now.Unlocked || before.Unlocked)
                return null;

            bool wantObjectives = pinned && rules.Pinned == PinnedToastMode.ObjectivesAndThresholds;
            bool wantThresholds = pinned ? rules.Pinned != PinnedToastMode.Off : rules.Unpinned == UnpinnedToastMode.ThresholdsOnly;

            int crossed = 0;
            if (wantThresholds)
            {
                int was = Pct(before.Overall), isNow = Pct(now.Overall);
                foreach (int t in rules.Thresholds)
                {
                    if (was < t && isNow >= t)
                        crossed = t;
                }
            }

            var newlyMet = new List<int>();
            if (wantObjectives && before.Met != null && before.Met.Length == now.Total)
            {
                for (int i = 0; i < now.Total; i++)
                {
                    if (!before.Met[i] && now.Objectives[i].Met)
                        newlyMet.Add(i);
                }
            }

            if (newlyMet.Count == 0)
                return crossed > 0 ? Prefix + name + " " + crossed + "%" : null;

            string what = newlyMet.Count == 1 ? now.Objectives[newlyMet[0]].Label : newlyMet.Count + " objectives";
            string line = Prefix + what + " " + Labels.Check + " · " + name + " " + Labels.OverallText(now);
            if (crossed > 0 && now.ShowAsCount)
                line += " · " + crossed + "%";
            return line;
        }

        private static int Pct(float f)
        {
            return (int)Math.Floor(Math.Max(0f, Math.Min(1f, f)) * 100f + 1e-4f);
        }
    }
}
