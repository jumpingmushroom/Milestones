using System.Collections.Generic;
using Milestones.Core.Model;
using Xunit;

public class ProgressCalcTests
{
    private static ObjectiveInput In(float target, float? value, Op op = Op.AtLeast, Source src = Source.ItemCraft, string key = "k")
    {
        return new ObjectiveInput
        {
            Source = src, Key = key, Label = key, Op = op, Target = target,
            HasValue = value.HasValue, Value = value ?? 0f
        };
    }

    [Fact]
    public void AtLeast_partial_fills_proportionally()
    {
        Objective o = ProgressCalc.Evaluate(In(30, 12));
        Assert.False(o.Met);
        Assert.True(o.HasBar);
        Assert.Equal(0.4f, o.Fraction, 3);
        Assert.Equal(12f, o.Current);
        Assert.True(o.Started);
    }

    [Fact]
    public void AtLeast_over_target_is_met_and_capped()
    {
        Objective o = ProgressCalc.Evaluate(In(5, 9));
        Assert.True(o.Met);
        Assert.Equal(1f, o.Fraction);
    }

    [Fact]
    public void Missing_key_is_not_met_for_every_operator()
    {
        foreach (Op op in new[] { Op.AtLeast, Op.AtMost, Op.Exactly, Op.NotEqual })
        {
            Objective o = ProgressCalc.Evaluate(In(0, null, op));
            Assert.False(o.Met);
            Assert.Equal(0f, o.Fraction);
            Assert.False(o.Started);
        }
    }

    [Fact]
    public void Non_count_operators_are_binary()
    {
        Assert.True(ProgressCalc.Evaluate(In(3, 2, Op.AtMost)).Met);
        Assert.False(ProgressCalc.Evaluate(In(3, 4, Op.AtMost)).Met);
        Assert.True(ProgressCalc.Evaluate(In(3, 3, Op.Exactly)).Met);
        Assert.True(ProgressCalc.Evaluate(In(3, 1, Op.NotEqual)).Met);
        Objective o = ProgressCalc.Evaluate(In(3, 4, Op.AtMost));
        Assert.False(o.HasBar);
        Assert.Equal(0f, o.Fraction);
    }

    [Fact]
    public void Auto_uses_count_when_every_target_is_one()
    {
        var inputs = new List<ObjectiveInput> { In(1, 1, key: "a"), In(1, null, key: "b"), In(1, 0, key: "c"), In(1, 3, key: "d") };
        AchievementProgress p = ProgressCalc.Compute("AllFoodEaten", inputs, false, false, OverallMode.Auto);
        Assert.True(p.ShowAsCount);
        Assert.Equal(2, p.MetCount);
        Assert.Equal(4, p.Total);
        Assert.Equal(0.5f, p.Overall, 3);
    }

    [Fact]
    public void Auto_uses_average_when_targets_differ()
    {
        var inputs = new List<ObjectiveInput> { In(10, 5, key: "a"), In(4, 4, key: "b") };
        AchievementProgress p = ProgressCalc.Compute("x", inputs, false, false, OverallMode.Auto);
        Assert.False(p.ShowAsCount);
        Assert.Equal(0.75f, p.Overall, 3);
    }

    [Fact]
    public void Forced_modes_override_auto()
    {
        var inputs = new List<ObjectiveInput> { In(10, 5, key: "a"), In(4, 4, key: "b") };
        Assert.Equal(0.5f, ProgressCalc.Compute("x", inputs, false, false, OverallMode.Count).Overall, 3);
        var ones = new List<ObjectiveInput> { In(1, 1, key: "a"), In(1, 0, key: "b") };
        AchievementProgress avg = ProgressCalc.Compute("x", ones, false, false, OverallMode.Average);
        Assert.False(avg.ShowAsCount);
        Assert.Equal(0.5f, avg.Overall, 3);
    }

    [Fact]
    public void Unlocked_is_always_full()
    {
        var inputs = new List<ObjectiveInput> { In(10, 0) };
        AchievementProgress p = ProgressCalc.Compute("x", inputs, true, false, OverallMode.Auto);
        Assert.Equal(1f, p.Overall);
        Assert.True(p.Unlocked);
    }

    [Fact]
    public void No_objectives_reads_zero_until_unlocked()
    {
        Assert.Equal(0f, ProgressCalc.Compute("x", new List<ObjectiveInput>(), false, false, OverallMode.Auto).Overall);
    }

    [Fact]
    public void Lenient_caps_each_stat_at_115_percent_and_pools_them()
    {
        // Targets 10 + 10 = 20. Stat a at 20 counts as 11.5 (cap), b at 4: 15.5 / 20.
        var inputs = new List<ObjectiveInput>
        {
            In(10, 20, src: Source.PlayerStat, key: "a"),
            In(10, 4, src: Source.PlayerStat, key: "b")
        };
        AchievementProgress p = ProgressCalc.Compute("Build", inputs, false, true, OverallMode.Count);
        Assert.False(p.ShowAsCount);
        Assert.Equal(15.5f / 20f, p.Overall, 3);
    }

    [Fact]
    public void Lenient_never_exceeds_one()
    {
        var inputs = new List<ObjectiveInput> { In(10, 50, src: Source.PlayerStat, key: "a"), In(10, 50, src: Source.PlayerStat, key: "b") };
        Assert.Equal(1f, ProgressCalc.Compute("Build", inputs, false, true, OverallMode.Auto).Overall);
    }

    [Fact]
    public void SameValues_detects_any_change()
    {
        var a = ProgressCalc.Compute("x", new List<ObjectiveInput> { In(10, 5) }, false, false, OverallMode.Auto);
        var b = ProgressCalc.Compute("x", new List<ObjectiveInput> { In(10, 5) }, false, false, OverallMode.Auto);
        var c = ProgressCalc.Compute("x", new List<ObjectiveInput> { In(10, 6) }, false, false, OverallMode.Auto);
        var d = ProgressCalc.Compute("x", new List<ObjectiveInput> { In(10, 5) }, true, false, OverallMode.Auto);
        Assert.True(ProgressCalc.SameValues(a, b));
        Assert.False(ProgressCalc.SameValues(a, c));
        Assert.False(ProgressCalc.SameValues(a, d));
        Assert.False(ProgressCalc.SameValues(a, null));
    }
}
