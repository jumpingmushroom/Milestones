using System.Collections.Generic;

namespace Milestones.Core.Model
{
    // Everything in Core/Model is plain C#: no UnityEngine, no game types. The xUnit project
    // compiles these files directly, so keep it that way.

    /// <summary>How an objective compares its stat to the target. Mirrors the game's RequirementOperator.</summary>
    public enum Op { AtLeast, AtMost, Exactly, NotEqual }

    /// <summary>Which trigger list, and so which stat dictionary, an objective came from.</summary>
    public enum Source
    {
        PlayerStat, Enemy, ItemPickup, ItemCraft, FoodEaten, Pickable, PiecePlaced,
        KnownWorld, KnownWorldKey, KnownCommand, OtherAchievement
    }

    public enum OverallMode { Auto, Average, Count }
    public enum PinnedToastMode { ObjectivesAndThresholds, ThresholdsOnly, Off }
    public enum UnpinnedToastMode { ThresholdsOnly, Off }

    /// <summary>One trigger as read from the game, before evaluation.</summary>
    public sealed class ObjectiveInput
    {
        public Source Source;
        /// <summary>The stat key as the game stores it ("$item_berries", "Tree", an achievement id).</summary>
        public string Key;
        /// <summary>Display text, already localized.</summary>
        public string Label;
        public Op Op;
        public float Target;
        /// <summary>False when the key is absent from the stat dictionary; the game counts that as not met.</summary>
        public bool HasValue;
        public float Value;
    }

    public sealed class Objective
    {
        public Source Source;
        public string Key;
        public string Label;
        public Op Op;
        public float Current;
        public float Target;
        public bool Met;
        /// <summary>True for AtLeast, where a partial fill means something. The rest are met or not.</summary>
        public bool HasBar;
        /// <summary>0..1. AtLeast: current / target, capped. Others: 1 when met, else 0.</summary>
        public float Fraction;

        public bool Started => Met || Current > 0f;
    }

    public sealed class AchievementProgress
    {
        public string Id;
        public List<Objective> Objectives = new List<Objective>();
        public int MetCount;
        /// <summary>0..1.</summary>
        public float Overall;
        /// <summary>Show "37 / 212" rather than a percentage.</summary>
        public bool ShowAsCount;
        public bool Unlocked;

        public int Total => Objectives.Count;
    }
}
