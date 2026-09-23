using System;
using System.Collections.Generic;

namespace Milestones.Core.Model
{
    /// <summary>
    /// Turns trigger inputs into progress, following Achievement.CheckUnlocked: a key that is
    /// absent fails every operator, AtLeast is value &gt;= target, and so on. Lenient build
    /// achievements follow Piece.CheckLenientBuildAchUnlocked: each stat counts up to 1.15x its
    /// target and the achievement unlocks when the pooled sum exceeds the pooled targets.
    /// </summary>
    public static class ProgressCalc
    {
        public const float LenientFactor = 1.15f;

        public static Objective Evaluate(ObjectiveInput i)
        {
            float current = i.HasValue ? i.Value : 0f;
            bool met;
            if (!i.HasValue)
                met = false;
            else
            {
                switch (i.Op)
                {
                    case Op.AtLeast: met = current >= i.Target; break;
                    case Op.AtMost: met = current <= i.Target; break;
                    case Op.Exactly: met = current == i.Target; break;
                    default: met = current != i.Target; break;
                }
            }

            bool hasBar = i.Op == Op.AtLeast;
            float fraction;
            if (met)
                fraction = 1f;
            else if (hasBar && i.Target > 0f)
                fraction = Clamp01(current / i.Target);
            else
                fraction = 0f;

            return new Objective
            {
                Source = i.Source, Key = i.Key, Label = i.Label, Op = i.Op,
                Current = current, Target = i.Target, Met = met, HasBar = hasBar, Fraction = fraction
            };
        }

        public static AchievementProgress Compute(string id, IList<ObjectiveInput> inputs, bool unlocked, bool lenient, OverallMode mode)
        {
            var p = new AchievementProgress { Id = id, Unlocked = unlocked };
            bool allOnce = inputs.Count > 0;
            float sumFraction = 0f;
            float lenientTargets = 0f, lenientCapped = 0f;

            foreach (ObjectiveInput input in inputs)
            {
                Objective o = Evaluate(input);
                p.Objectives.Add(o);
                if (o.Met)
                    p.MetCount++;
                sumFraction += o.Fraction;
                if (o.Op != Op.AtLeast || o.Target != 1f)
                    allOnce = false;
                if (lenient && o.Source == Source.PlayerStat)
                {
                    lenientTargets += o.Target;
                    lenientCapped += Math.Min(o.Current, o.Target * LenientFactor);
                }
            }

            int n = p.Objectives.Count;
            if (lenient)
            {
                p.ShowAsCount = false;
                p.Overall = lenientTargets > 0f ? Clamp01(lenientCapped / lenientTargets) : 0f;
            }
            else
            {
                p.ShowAsCount = mode == OverallMode.Count || (mode == OverallMode.Auto && allOnce);
                if (n == 0)
                    p.Overall = 0f;
                else if (p.ShowAsCount)
                    p.Overall = (float)p.MetCount / n;
                else
                    p.Overall = sumFraction / n;
            }

            if (unlocked)
                p.Overall = 1f;
            return p;
        }

        /// <summary>True when nothing a viewer could see differs, so no event needs to fire.</summary>
        public static bool SameValues(AchievementProgress a, AchievementProgress b)
        {
            if (a == null || b == null)
                return false;
            if (a.Unlocked != b.Unlocked || a.MetCount != b.MetCount || a.Overall != b.Overall || a.Total != b.Total)
                return false;
            for (int i = 0; i < a.Total; i++)
            {
                if (a.Objectives[i].Current != b.Objectives[i].Current || a.Objectives[i].Met != b.Objectives[i].Met)
                    return false;
            }
            return true;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }
    }
}
