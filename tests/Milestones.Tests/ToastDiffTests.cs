using System.Collections.Generic;
using Milestones.Core.Model;
using Xunit;

public class ToastDiffTests
{
    private static readonly ToastRules Defaults = new ToastRules
    {
        Pinned = PinnedToastMode.ObjectivesAndThresholds,
        Unpinned = UnpinnedToastMode.ThresholdsOnly,
        Thresholds = new[] { 25, 50, 75 }
    };

    // Four do-it-once objectives named a..d; `met` says which are done.
    private static AchievementProgress Once(bool unlocked, params bool[] met)
    {
        var inputs = new List<ObjectiveInput>();
        for (int i = 0; i < met.Length; i++)
            inputs.Add(new ObjectiveInput { Key = "k" + i, Label = ((char)('a' + i)).ToString(), Op = Op.AtLeast, Target = 1, HasValue = met[i], Value = met[i] ? 1 : 0 });
        return ProgressCalc.Compute("x", inputs, unlocked, false, OverallMode.Auto);
    }

    [Fact]
    public void Nothing_changed_says_nothing()
    {
        var p = Once(false, true, false, false, false);
        Assert.Null(ToastDiff.Diff(Snapshot.Take(p), p, "Eat", true, Defaults));
    }

    [Fact]
    public void Pinned_objective_and_threshold_merge_into_one_line()
    {
        var before = Snapshot.Take(Once(false, false, false, false, false));
        var now = Once(false, true, false, false, false);
        Assert.Equal("Milestones: a ✓ · Eat 1 / 4 · 25%", ToastDiff.Diff(before, now, "Eat", true, Defaults));
    }

    [Fact]
    public void Pinned_objective_without_threshold()
    {
        // 4 of 8 → 5 of 8 crosses nothing (50% → 62%).
        var before = Snapshot.Take(Once(false, true, true, true, true, false, false, false, false));
        var now = Once(false, true, true, true, true, true, false, false, false);
        Assert.Equal("Milestones: e ✓ · Eat 5 / 8", ToastDiff.Diff(before, now, "Eat", true, Defaults));
    }

    [Fact]
    public void Several_objectives_at_once_are_counted()
    {
        var before = Snapshot.Take(Once(false, false, false, false, false, false, false, false, false));
        var now = Once(false, true, true, false, false, false, false, false, false);
        Assert.Equal("Milestones: 2 objectives ✓ · Eat 2 / 8 · 25%", ToastDiff.Diff(before, now, "Eat", true, Defaults));
    }

    [Fact]
    public void Unpinned_gets_thresholds_only_and_the_highest_one_crossed()
    {
        var before = Snapshot.Take(Once(false, false, false, false, false));
        Assert.Null(ToastDiff.Diff(before, Once(false, false, false, false, false), "Eat", false, Defaults));
        var now = Once(false, true, true, true, false);
        Assert.Equal("Milestones: Eat 75%", ToastDiff.Diff(before, now, "Eat", false, Defaults));
    }

    [Fact]
    public void Unpinned_objective_alone_is_silent()
    {
        // 1 of 8 → 2 of 8: 12% → 25% crosses 25, so use 2 → 3 of 8 (25% → 37%) instead.
        var before = Snapshot.Take(Once(false, true, true, false, false, false, false, false, false));
        var now = Once(false, true, true, true, false, false, false, false, false);
        Assert.Null(ToastDiff.Diff(before, now, "Eat", false, Defaults));
    }

    [Fact]
    public void Unlock_is_left_to_the_vanilla_popup()
    {
        var before = Snapshot.Take(Once(false, true, true, true, false));
        Assert.Null(ToastDiff.Diff(before, Once(true, true, true, true, true), "Eat", true, Defaults));
    }

    [Fact]
    public void Going_down_never_toasts()
    {
        var before = Snapshot.Take(Once(false, true, true, true, false));
        Assert.Null(ToastDiff.Diff(before, Once(false, false, false, false, false), "Eat", true, Defaults));
    }

    [Fact]
    public void Modes_off_silence_everything()
    {
        var rules = new ToastRules { Pinned = PinnedToastMode.Off, Unpinned = UnpinnedToastMode.Off, Thresholds = new[] { 25 } };
        var before = Snapshot.Take(Once(false, false, false, false, false));
        var now = Once(false, true, true, false, false);
        Assert.Null(ToastDiff.Diff(before, now, "Eat", true, rules));
        Assert.Null(ToastDiff.Diff(before, now, "Eat", false, rules));
    }

    [Fact]
    public void Pinned_thresholds_only()
    {
        var rules = new ToastRules { Pinned = PinnedToastMode.ThresholdsOnly, Unpinned = UnpinnedToastMode.Off, Thresholds = new[] { 50 } };
        var before = Snapshot.Take(Once(false, false, false, false, false));
        Assert.Null(ToastDiff.Diff(before, Once(false, true, false, false, false), "Eat", true, rules));
        Assert.Equal("Milestones: Eat 50%", ToastDiff.Diff(before, Once(false, true, true, false, false), "Eat", true, rules));
    }

    [Fact]
    public void Objective_list_that_changed_shape_skips_objective_toasts()
    {
        var before = Snapshot.Take(Once(false, false, false));
        var now = Once(false, true, false, false, false, false, false, false, false);
        Assert.Null(ToastDiff.Diff(before, now, "Eat", true, Defaults));
    }

    [Theory]
    [InlineData("25,50,75", new[] { 25, 50, 75 })]
    [InlineData(" 75, 25 ,x,0,100,50,50", new[] { 25, 50, 75 })]
    [InlineData("", new int[0])]
    public void ParseThresholds(string raw, int[] expected)
    {
        Assert.Equal(expected, ToastRules.ParseThresholds(raw));
    }
}
