using HarmonyLib;
using Milestones.Core;

namespace Milestones.Patches
{
    // Every achievement stat write in the game goes through these PlayerProfile methods, and every
    // unlock through Achievements.AchievementEvent (PLAN.md §1.3). Postfixes only mark; nothing is
    // computed here, because DistanceTraveled fires every frame.

    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStat))]
    internal static class IncrementStatPatch
    {
        private static void Postfix(PlayerStatType stat) => StatEvents.MarkStat(stat);
    }

    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.SetStat))]
    internal static class SetStatPatch
    {
        private static void Postfix(PlayerStatType stat) => StatEvents.MarkStat(stat);
    }

    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStatEnemy))]
    internal static class IncrementStatEnemyPatch
    {
        private static void Postfix() => StatEvents.Mark(StatKind.Enemy);
    }

    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStatItemPickup))]
    internal static class IncrementStatItemPickupPatch
    {
        private static void Postfix() => StatEvents.Mark(StatKind.Pickup);
    }

    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStatItemCraft))]
    internal static class IncrementStatItemCraftPatch
    {
        private static void Postfix() => StatEvents.Mark(StatKind.Craft);
    }

    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStatPickable))]
    internal static class IncrementStatPickablePatch
    {
        private static void Postfix() => StatEvents.Mark(StatKind.Pickable);
    }

    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStatFoodEaten))]
    internal static class IncrementStatFoodEatenPatch
    {
        private static void Postfix() => StatEvents.Mark(StatKind.Food);
    }

    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStatBuildPiecePlaced))]
    internal static class IncrementStatBuildPiecePlacedPatch
    {
        private static void Postfix() => StatEvents.Mark(StatKind.Piece);
    }

    [HarmonyPatch(typeof(Achievements), nameof(Achievements.AchievementEvent))]
    internal static class AchievementEventPatch
    {
        private static void Postfix(Achievement ach) => StatEvents.MarkUnlocked(ach);
    }
}
