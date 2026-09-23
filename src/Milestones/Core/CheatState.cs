namespace Milestones.Core
{
    /// <summary>
    /// Why the game has stopped recording achievement stats, or null when it records.
    /// Mirrors Achievements.IsCheatedAtAll (PLAN.md §1.4).
    /// </summary>
    internal static class CheatState
    {
        public static string PausedReason()
        {
            if (Achievements.m_instance == null || Game.instance == null)
                return null;
            if (Achievements.CanGetAchievements())
                return null;
            if (Game.isModded)
                return "a mod set Game.isModded";
            if (Achievements.IsWorldCheated())
                return "world modifiers";
            if (Game.instance.GetPlayerProfile().m_usedCheats)
                return "cheats used on this character";
            if (Player.m_localPlayer != null && Player.m_localPlayer.GetInventory().AnyCheatedItem())
                return "a cheated item in your inventory";
            return "the game's cheat check";
        }
    }
}
