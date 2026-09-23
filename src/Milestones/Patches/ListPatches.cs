using System;
using HarmonyLib;
using Milestones.UI;

namespace Milestones.Patches
{
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateAchievementsList))]
    internal static class UpdateAchievementsListPatch
    {
        private static void Postfix(InventoryGui __instance)
        {
            if (!PluginConfig.Enabled.Value)
                return;
            try
            {
                ListMarks.Apply(__instance);
            }
            catch (Exception e)
            {
                MilestonesPlugin.WarnOnce("grid pin marks", e);
            }
        }
    }
}
