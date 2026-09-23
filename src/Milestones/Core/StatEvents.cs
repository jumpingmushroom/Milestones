using System;
using System.Collections.Generic;

namespace Milestones.Core
{
    [Flags]
    internal enum StatKind
    {
        None = 0,
        Player = 1,
        Enemy = 2,
        Pickup = 4,
        Craft = 8,
        Pickable = 16,
        Food = 32,
        Piece = 64,
        Unlock = 128,
        All = 255
    }

    /// <summary>
    /// Collects "something changed" marks from the stat postfixes. Some stats (DistanceTraveled)
    /// change every frame, so this only records; ProgressCache drains it at most four times a second.
    /// </summary>
    internal static class StatEvents
    {
        private static StatKind _kinds;
        private static readonly HashSet<PlayerStatType> Stats = new HashSet<PlayerStatType>();

        public static event Action<Achievement> Unlocked;

        public static bool Any => _kinds != StatKind.None;

        public static void Mark(StatKind kind)
        {
            _kinds |= kind;
        }

        public static void MarkStat(PlayerStatType stat)
        {
            _kinds |= StatKind.Player;
            Stats.Add(stat);
        }

        public static void MarkUnlocked(Achievement a)
        {
            _kinds |= StatKind.All;
            if (Unlocked != null)
                Unlocked(a);
        }

        public static StatKind Take(HashSet<PlayerStatType> statsOut)
        {
            statsOut.Clear();
            statsOut.UnionWith(Stats);
            Stats.Clear();
            StatKind k = _kinds;
            _kinds = StatKind.None;
            return k;
        }
    }
}
