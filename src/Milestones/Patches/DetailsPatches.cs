using System;
using HarmonyLib;
using Milestones.UI;

namespace Milestones.Patches
{
    [HarmonyPatch(typeof(AchievementsGui), nameof(AchievementsGui.OnOpenAchievementDetails))]
    internal static class OpenDetailsPatch
    {
        private static bool Prefix(AchievementsGui __instance, Achievement achievement, bool clickable)
        {
            if (!PluginConfig.Enabled.Value)
                return true;
            // A row build failed once this session; stop competing with vanilla for good.
            if (DetailsPanel.Failed)
                return true;
            // Same early-outs as vanilla, and locked secrets keep vanilla's ???.
            if (__instance.m_achievementDetails.activeSelf || !clickable)
                return true;
            if (achievement.m_isSecret && !achievement.m_unlocked)
                return true;
            try
            {
                DetailsPanel.Open(__instance, achievement);
                return false;
            }
            catch (Exception e)
            {
                MilestonesPlugin.WarnOnce("details panel", e);
                DetailsPanel.MarkFailed();
                DetailsPanel.Abort(__instance);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(AchievementsGui), nameof(AchievementsGui.CloseDetails))]
    internal static class CloseDetailsPatch
    {
        private static void Postfix() => DetailsPanel.Closed();
    }
}
