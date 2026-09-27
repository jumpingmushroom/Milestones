using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Milestones.Core
{
    /// <summary>
    /// Whether Steam already has an achievement unlocked. The game never asks: m_unlocked comes
    /// only from the current character's stats (Achievement.CheckUnlocked), while Steam unlocks a
    /// stat-linked achievement such as The Captain (DistanceSailHelm) on its own once the account's
    /// stat reaches the threshold, and every character adds to that one stat
    /// (Achievements.AchievementStatIncrementEvent). Steamworks isn't a build reference, so
    /// SteamUserStats.GetAchievement is found by reflection; any failure means "not known".
    /// </summary>
    internal static class PlatformUnlocks
    {
        private static readonly HashSet<string> Known = new HashSet<string>();
        private static MethodInfo _getAchievement;
        private static bool _resolved;
        private static bool _failed;

        public static void Clear()
        {
            Known.Clear();
        }

        public static bool IsUnlocked(Achievement a)
        {
            if (Known.Contains(a.m_id))
                return true;
            if (_failed || !SteamManager.Initialized)
                return false;
            try
            {
                if (!_resolved)
                {
                    _resolved = true;
                    Type stats = AccessTools.TypeByName("Steamworks.SteamUserStats");
                    if (stats != null)
                        _getAchievement = AccessTools.Method(stats, "GetAchievement", new[] { typeof(string), typeof(bool).MakeByRefType() });
                }
                if (_getAchievement == null)
                    return false;
                // The same API name the game unlocks with (Achievements.AchievementEvent).
                string name = Achievements.GetAchievementInternalPlatformId(a);
                if (string.IsNullOrEmpty(name))
                    return false;
                object[] args = { name, false };
                if ((bool)_getAchievement.Invoke(null, args) && (bool)args[1])
                {
                    Known.Add(a.m_id);
                    return true;
                }
            }
            catch (Exception e)
            {
                _failed = true;
                MilestonesPlugin.WarnOnce("platform unlocks", e);
            }
            return false;
        }
    }
}
