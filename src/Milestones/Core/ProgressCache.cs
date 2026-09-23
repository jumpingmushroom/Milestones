using System;
using System.Collections.Generic;
using Milestones.Core.Model;

namespace Milestones.Core
{
    /// <summary>
    /// One AchievementProgress per achievement, recomputed only when a stat it reads was marked
    /// dirty. Changed fires only when something visible differs (ProgressCalc.SameValues).
    /// </summary>
    internal static class ProgressCache
    {
        private sealed class Index
        {
            public StatKind Kinds;
            public readonly HashSet<PlayerStatType> Stats = new HashSet<PlayerStatType>();
        }

        private static readonly Dictionary<string, AchievementProgress> ById = new Dictionary<string, AchievementProgress>();
        private static readonly Dictionary<string, Index> Indexes = new Dictionary<string, Index>();
        private static readonly HashSet<PlayerStatType> DirtyStats = new HashSet<PlayerStatType>();

        public static event Action<Achievement, AchievementProgress> Changed;

        public static void Clear()
        {
            ById.Clear();
            Indexes.Clear();
            AchievementReader.ClearLabels();
        }

        public static AchievementProgress Get(Achievement a)
        {
            AchievementProgress p;
            if (ById.TryGetValue(a.m_id, out p))
                return p;
            p = Compute(a);
            if (p != null)
                ById[a.m_id] = p;
            return p;
        }

        public static void Recompute(Achievement a)
        {
            AchievementProgress now = Compute(a);
            if (now == null)
                return;
            AchievementProgress old;
            bool had = ById.TryGetValue(a.m_id, out old);
            ById[a.m_id] = now;
            if (had && !ProgressCalc.SameValues(old, now) && Changed != null)
            {
                if (PluginConfig.Verbose.Value)
                    MilestonesPlugin.Log.LogInfo("Milestones: " + a.m_id + " → " + Labels.OverallText(now));
                Changed(a, now);
            }
        }

        public static void ProcessDirty()
        {
            StatKind kinds = StatEvents.Take(DirtyStats);
            if (kinds == StatKind.None)
                return;
            foreach (Achievement a in AchievementReader.All())
            {
                if (!ById.ContainsKey(a.m_id))
                    continue;
                Index idx = IndexOf(a);
                bool hit = (kinds & StatKind.Unlock) != 0
                    || (idx.Kinds & kinds & ~StatKind.Player) != 0
                    || ((kinds & StatKind.Player) != 0 && idx.Stats.Overlaps(DirtyStats));
                if (hit)
                    Recompute(a);
            }
        }

        /// <summary>Safety net for stats written outside the patched methods (known worlds, keys, commands, resets).</summary>
        public static void RefreshAll()
        {
            foreach (Achievement a in AchievementReader.All())
                Recompute(a);
        }

        private static AchievementProgress Compute(Achievement a)
        {
            PlayerProfile profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (profile == null)
                return null;
            return ProgressCalc.Compute(a.m_id, AchievementReader.Read(a, profile), a.m_unlocked,
                a.m_lenientBuildAchievement, PluginConfig.Overall.Value);
        }

        private static Index IndexOf(Achievement a)
        {
            Index idx;
            if (Indexes.TryGetValue(a.m_id, out idx))
                return idx;
            idx = new Index();
            foreach (Achievement.PlayerStatRequirement r in a.m_statTrigger)
            {
                idx.Kinds |= StatKind.Player;
                idx.Stats.Add(r.m_stat);
            }
            if (a.m_enemyStatsTriggers.Count > 0) idx.Kinds |= StatKind.Enemy;
            if (a.m_itemPickupTriggers.Count > 0) idx.Kinds |= StatKind.Pickup;
            if (a.m_itemCraftTriggers.Count > 0) idx.Kinds |= StatKind.Craft;
            if (a.m_pickableTriggers.Count > 0) idx.Kinds |= StatKind.Pickable;
            if (a.m_foodEatenTriggers.Count > 0) idx.Kinds |= StatKind.Food;
            if (a.m_piecePlacedTriggers.Count > 0) idx.Kinds |= StatKind.Piece;
            Indexes[a.m_id] = idx;
            return idx;
        }
    }
}
