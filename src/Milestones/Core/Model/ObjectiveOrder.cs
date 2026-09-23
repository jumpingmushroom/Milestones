using System.Collections.Generic;

namespace Milestones.Core.Model
{
    public sealed class TrackerRows
    {
        public List<int> Indices = new List<int>();
        /// <summary>Unfinished objectives not shown.</summary>
        public int More;
    }

    public static class ObjectiveOrder
    {
        /// <summary>Unmet first (highest fill first), then met; ties keep the game's order.</summary>
        public static List<int> Sorted(AchievementProgress p)
        {
            var idx = new List<int>(p.Total);
            for (int i = 0; i < p.Total; i++)
                idx.Add(i);
            idx.Sort((a, b) =>
            {
                Objective oa = p.Objectives[a], ob = p.Objectives[b];
                if (oa.Met != ob.Met)
                    return oa.Met ? 1 : -1;
                if (!oa.Met && oa.Fraction != ob.Fraction)
                    return ob.Fraction.CompareTo(oa.Fraction);
                return a.CompareTo(b);
            });
            return idx;
        }

        /// <summary>
        /// Tracker rows: every objective in game order when there are at most <paramref name="max"/>,
        /// otherwise the unfinished ones closest to done, and a count of the unfinished rest.
        /// </summary>
        public static TrackerRows Select(AchievementProgress p, int max)
        {
            var rows = new TrackerRows();
            if (p.Total <= max)
            {
                for (int i = 0; i < p.Total; i++)
                    rows.Indices.Add(i);
                return rows;
            }
            int unmet = 0;
            foreach (int i in Sorted(p))
            {
                if (p.Objectives[i].Met)
                    break;
                unmet++;
                if (rows.Indices.Count < max)
                    rows.Indices.Add(i);
            }
            rows.More = unmet - rows.Indices.Count;
            return rows;
        }
    }
}
