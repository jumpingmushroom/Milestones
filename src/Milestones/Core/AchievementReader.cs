using System;
using System.Collections.Generic;
using Milestones.Core.Model;

namespace Milestones.Core
{
    /// <summary>
    /// Reads an Achievement's trigger lists into model inputs, from exactly the dictionaries
    /// Achievement.CheckUnlocked reads: profile.m_playerStats[(int)m_difficultyRequirement].
    /// </summary>
    internal static class AchievementReader
    {
        private static readonly Dictionary<string, string> LabelCache = new Dictionary<string, string>();

        public static IEnumerable<Achievement> All()
        {
            Achievements inst = Achievements.m_instance;
            if (inst == null)
                yield break;
            foreach (AchievementList list in inst.m_achievementLists)
            {
                if (list == null)
                    continue;
                foreach (Achievement a in list.m_achievements)
                {
                    if (a != null)
                        yield return a;
                }
            }
        }

        public static Achievement ById(string id)
        {
            foreach (Achievement a in All())
            {
                if (a.m_id == id)
                    return a;
            }
            return null;
        }

        /// <summary>Exact id (any case), then the first whose localized name contains the query.</summary>
        public static Achievement Find(string query)
        {
            foreach (Achievement a in All())
            {
                if (string.Equals(a.m_id, query, StringComparison.OrdinalIgnoreCase))
                    return a;
            }
            foreach (Achievement a in All())
            {
                if (Name(a).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    return a;
            }
            return null;
        }

        public static string Name(Achievement a)
        {
            return Loc(a.m_name);
        }

        /// <summary>
        /// The grid order InventoryGui.UpdateAchievementsList uses: buckets by the first two digits
        /// of m_iconLocked's name (unparsable goes to bucket 9), then buckets 1..9. Bucket 0 is
        /// never shown by the game, so it is dropped here too.
        /// </summary>
        public static List<Achievement> GridOrder()
        {
            var buckets = new List<List<Achievement>>();
            for (int i = 0; i <= 9; i++)
                buckets.Add(new List<Achievement>());
            foreach (Achievement a in All())
            {
                int tier;
                string n = a.m_iconLocked != null ? a.m_iconLocked.name : "";
                if (n.Length >= 2 && int.TryParse(n.Substring(0, 2), out tier) && tier >= 0 && tier <= 9)
                    buckets[tier].Add(a);
                else
                    buckets[9].Add(a);
            }
            var result = new List<Achievement>();
            for (int j = 1; j <= 9; j++)
                result.AddRange(buckets[j]);
            return result;
        }

        public static List<ObjectiveInput> Read(Achievement a, PlayerProfile profile)
        {
            var list = new List<ObjectiveInput>();
            int slot = (int)a.m_difficultyRequirement;
            if (slot < 0 || slot >= profile.m_playerStats.Length)
                return list;
            PlayerProfile.PlayerStats stats = profile.m_playerStats[slot];

            foreach (Achievement.PlayerStatRequirement r in a.m_statTrigger)
            {
                float v;
                bool has = stats.m_stats.TryGetValue(r.m_stat, out v);
                string key = r.m_stat.ToString();
                list.Add(new ObjectiveInput
                {
                    Source = Source.PlayerStat, Key = key, Label = Label("$stat_" + key, key),
                    Op = Op.AtLeast, Target = r.m_amountAboveEquals, HasValue = has, Value = v
                });
            }

            foreach (Achievement.EnemyStatRequirement r in a.m_enemyStatsTriggers)
            {
                int m = (int)r.m_modifier;
                if (m < 0 || m >= stats.m_enemyStats.Length)
                    continue;
                float v;
                bool has = stats.m_enemyStats[m].TryGetValue(r.m_stat, out v);
                list.Add(new ObjectiveInput
                {
                    Source = Source.Enemy, Key = r.m_stat, Label = Label(r.m_stat, r.m_stat) + ModifierSuffix(r.m_modifier),
                    Op = Map(r.m_operator), Target = r.m_amount, HasValue = has, Value = v
                });
            }

            AddDict(list, a.m_itemPickupTriggers, stats.m_itemPickupStats, Source.ItemPickup);
            AddDict(list, a.m_itemCraftTriggers, stats.m_itemCraftStats, Source.ItemCraft);
            AddDict(list, a.m_foodEatenTriggers, stats.m_foodEatenStats, Source.FoodEaten);
            AddDict(list, a.m_pickableTriggers, stats.m_pickableStats, Source.Pickable);
            AddDict(list, a.m_piecePlacedTriggers, stats.m_piecesPlacedStats, Source.PiecePlaced);
            AddDict(list, a.m_knownWorldTriggers, stats.m_knownWorlds, Source.KnownWorld);
            AddDict(list, a.m_knownWorldKeysTriggers, stats.m_knownWorldKeys, Source.KnownWorldKey);
            AddDict(list, a.m_knownCommandsTriggers, stats.m_knownCommands, Source.KnownCommand);

            foreach (Achievement other in a.m_otherAchievementTriggers)
            {
                if (other == null)
                    continue;
                list.Add(new ObjectiveInput
                {
                    Source = Source.OtherAchievement, Key = other.m_id, Label = Loc(other.m_name),
                    Op = Op.AtLeast, Target = 1f, HasValue = true, Value = other.m_unlocked ? 1f : 0f
                });
            }
            return list;
        }

        private static void AddDict(List<ObjectiveInput> list, List<Achievement.DictStatRequirement> triggers, Dictionary<string, float> dict, Source source)
        {
            foreach (Achievement.DictStatRequirement r in triggers)
            {
                float v = 0f;
                bool has = r.m_stat != null && dict.TryGetValue(r.m_stat, out v);
                list.Add(new ObjectiveInput
                {
                    Source = source, Key = r.m_stat, Label = Label(r.m_stat, r.m_stat),
                    Op = Map(r.m_operator), Target = r.m_amount, HasValue = has, Value = v
                });
            }
        }

        private static Op Map(RequirementOperator op)
        {
            switch (op)
            {
                case RequirementOperator.BelowEquals: return Op.AtMost;
                case RequirementOperator.Equals: return Op.Exactly;
                case RequirementOperator.NotEquals: return Op.NotEqual;
                default: return Op.AtLeast;
            }
        }

        private static string ModifierSuffix(KillModifiers m)
        {
            switch (m)
            {
                case KillModifiers.Unarmed: return " (unarmed)";
                case KillModifiers.Magic: return " (magic)";
                case KillModifiers.Ranged: return " (ranged)";
                case KillModifiers.Melee: return " (melee)";
                default: return "";
            }
        }

        /// <summary>Localized text for a token, cached; a key with no translation falls back to Labels.Humanize.</summary>
        private static string Label(string token, string fallbackKey)
        {
            if (string.IsNullOrEmpty(token))
                return "";
            string cached;
            if (LabelCache.TryGetValue(token, out cached))
                return cached;
            string loc = Loc(token);
            if (loc == token || loc.Length == 0 || (loc.StartsWith("[") && loc.EndsWith("]")))
                loc = Labels.Humanize(fallbackKey);
            LabelCache[token] = loc;
            return loc;
        }

        private static string Loc(string token)
        {
            if (string.IsNullOrEmpty(token))
                return "";
            string s = Localization.instance != null ? Localization.instance.Localize(token) : token;
            return s ?? token;
        }
    }
}
