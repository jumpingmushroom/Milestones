using BepInEx.Configuration;
using Milestones.Core.Model;
using UnityEngine;

namespace Milestones
{
    public enum TrackerAnchor
    {
        /// <summary>Right-aligned under the status-effect icons beside the minimap; follows their rows.</summary>
        UnderStatusEffects,
        TopLeft,
        TopRight,
        LeftMiddle,
        RightMiddle,
        BottomLeft,
        BottomRight
    }

    public enum ToastPosition
    {
        TopLeft,
        Center
    }

    public static class PluginConfig
    {
        // General
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<OverallMode> Overall;
        public static ConfigEntry<bool> SortObjectives;
        public static ConfigEntry<bool> HideUnmetNames;

        // Tracker
        public static ConfigEntry<bool> ShowTracker;
        public static ConfigEntry<TrackerAnchor> Anchor;
        public static ConfigEntry<float> OffsetX;
        public static ConfigEntry<float> OffsetY;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<float> Opacity;
        public static ConfigEntry<float> Width;
        public static ConfigEntry<int> MaxRowsPerAchievement;
        public static ConfigEntry<bool> HideWhenInventoryOpen;
        public static ConfigEntry<bool> AutoUnpinCompleted;

        // Toasts
        public static ConfigEntry<PinnedToastMode> PinnedToasts;
        public static ConfigEntry<UnpinnedToastMode> UnpinnedToasts;
        public static ConfigEntry<string> Thresholds;
        public static ConfigEntry<ToastPosition> ToastAt;

        // Colors
        public static ConfigEntry<Color> ColorNotStarted;
        public static ConfigEntry<Color> ColorInProgress;
        public static ConfigEntry<Color> ColorDone;

        // Logging
        public static ConfigEntry<bool> Verbose;

        private static ConfigurationManagerAttributes Attr(int order, bool advanced = false)
        {
            return new ConfigurationManagerAttributes { Order = order, IsAdvanced = advanced };
        }

        private static Color Hex(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            return c;
        }

        public static void Bind(ConfigFile cfg)
        {
            Enabled = cfg.Bind("General", "Enabled", true,
                new ConfigDescription("Master switch. Off restores the vanilla details panel and hides the tracker and toasts.", null, Attr(100)));

            Overall = cfg.Bind("General", "OverallMode", OverallMode.Auto,
                new ConfigDescription(
                    "How the overall bar is measured. Count: objectives met out of all (37 / 212). " +
                    "Average: mean fill of every objective. Auto: Count when every objective is " +
                    "'do this once' (eat every food, kill every creature), Average otherwise.",
                    null, Attr(95)));

            SortObjectives = cfg.Bind("General", "SortObjectives", true,
                new ConfigDescription("Unfinished objectives first, closest to done at the top. Off keeps the game's order.", null, Attr(90)));

            HideUnmetNames = cfg.Bind("General", "HideUnmetNames", false,
                new ConfigDescription(
                    "Spoiler guard: unmet objectives keep the game's ??? name but still show their bar " +
                    "and numbers. Useful for Kill every creature and Find every trophy on a first run.",
                    null, Attr(85)));

            ShowTracker = cfg.Bind("Tracker", "ShowTracker", true,
                new ConfigDescription("Show pinned achievements on the HUD.", null, Attr(80)));

            Anchor = cfg.Bind("Tracker", "Anchor", TrackerAnchor.UnderStatusEffects,
                new ConfigDescription(
                    "Where the tracker sits. UnderStatusEffects follows the status-effect icons beside " +
                    "the minimap and moves down when they wrap into a second row.",
                    null, Attr(78)));

            OffsetX = cfg.Bind("Tracker", "OffsetX", 0f,
                new ConfigDescription("Horizontal nudge in HUD pixels (positive is right).", new AcceptableValueRange<float>(-1000f, 1000f), Attr(76)));

            OffsetY = cfg.Bind("Tracker", "OffsetY", 0f,
                new ConfigDescription("Vertical nudge in HUD pixels (positive is up).", new AcceptableValueRange<float>(-1000f, 1000f), Attr(75)));

            Scale = cfg.Bind("Tracker", "Scale", 1f,
                new ConfigDescription("Size of the tracker.", new AcceptableValueRange<float>(0.5f, 2f), Attr(74)));

            Opacity = cfg.Bind("Tracker", "Opacity", 0.9f,
                new ConfigDescription("Opacity of the whole tracker.", new AcceptableValueRange<float>(0.2f, 1f), Attr(73)));

            Width = cfg.Bind("Tracker", "Width", 260f,
                new ConfigDescription("Width in HUD pixels before scaling.", new AcceptableValueRange<float>(160f, 500f), Attr(72)));

            MaxRowsPerAchievement = cfg.Bind("Tracker", "MaxRowsPerAchievement", 4,
                new ConfigDescription(
                    "Objective rows per pinned achievement. Larger ones show the unfinished objectives " +
                    "closest to done, then '+N more'.",
                    new AcceptableValueRange<int>(1, 10), Attr(70)));

            HideWhenInventoryOpen = cfg.Bind("Tracker", "HideWhenInventoryOpen", true,
                new ConfigDescription("Hide the tracker while the inventory or achievements screen is open.", null, Attr(68)));

            AutoUnpinCompleted = cfg.Bind("Tracker", "AutoUnpinCompleted", true,
                new ConfigDescription("Unpin an achievement 10 seconds after it unlocks. Off keeps it pinned, showing Done.", null, Attr(66)));

            PinnedToasts = cfg.Bind("Toasts", "PinnedToasts", PinnedToastMode.ObjectivesAndThresholds,
                new ConfigDescription("Toasts for pinned achievements.", null, Attr(60)));

            UnpinnedToasts = cfg.Bind("Toasts", "UnpinnedToasts", UnpinnedToastMode.ThresholdsOnly,
                new ConfigDescription("Toasts for every other achievement.", null, Attr(58)));

            Thresholds = cfg.Bind("Toasts", "Thresholds", "25,50,75",
                new ConfigDescription("Overall percentages that trigger a toast, comma-separated, 1-99.", null, Attr(56)));

            ToastAt = cfg.Bind("Toasts", "ToastPosition", ToastPosition.TopLeft,
                new ConfigDescription("TopLeft uses the game's pickup-message feed; Center the big centre message.", null, Attr(54)));

            ColorNotStarted = cfg.Bind("Colors", "NotStarted", Hex("#7a7a7a"),
                new ConfigDescription("Bar colour for an objective with no progress.", null, Attr(40)));

            ColorInProgress = cfg.Bind("Colors", "InProgress", Hex("#e0a030"),
                new ConfigDescription("Bar colour for an objective under way.", null, Attr(38)));

            ColorDone = cfg.Bind("Colors", "Done", Hex("#5ac85a"),
                new ConfigDescription("Bar colour for a met objective.", null, Attr(36)));

            Verbose = cfg.Bind("Logging", "Verbose", false,
                new ConfigDescription("Log recomputes, pin changes and toasts to the BepInEx log.", null, Attr(5, advanced: true)));
        }
    }
}
