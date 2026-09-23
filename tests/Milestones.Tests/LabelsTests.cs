using System.Collections.Generic;
using Milestones.Core.Model;
using Xunit;

public class LabelsTests
{
    [Theory]
    [InlineData("MaxBuildingHeight", "Max building height")]
    [InlineData("$item_serpentstew", "Serpentstew")]
    [InlineData("$enemy_greydwarf_shaman", "Greydwarf shaman")]
    [InlineData("$stat_Tree", "Tree")]
    [InlineData("", "")]
    public void Humanize(string key, string expected)
    {
        Assert.Equal(expected, Labels.Humanize(key));
    }

    [Theory]
    [InlineData(12f, "12")]
    [InlineData(12.5f, "12.5")]
    [InlineData(12345.678f, "12346")]
    [InlineData(0.25f, "0.3")]
    public void Amount(float v, string expected)
    {
        Assert.Equal(expected, Labels.Amount(v));
    }

    [Fact]
    public void Percent_floors_so_locked_never_reads_100()
    {
        Assert.Equal("99%", Labels.Percent(0.999f));
        Assert.Equal("100%", Labels.Percent(1f));
        Assert.Equal("0%", Labels.Percent(-1f));
    }

    private static Objective O(Op op, float target, float value, bool has = true)
    {
        return ProgressCalc.Evaluate(new ObjectiveInput { Key = "k", Label = "k", Op = op, Target = target, HasValue = has, Value = value });
    }

    [Fact]
    public void ProgressText()
    {
        Assert.Equal("12 / 30  40%", Labels.ProgressText(O(Op.AtLeast, 30, 12)));
        Assert.Equal("30 / 30  100%", Labels.ProgressText(O(Op.AtLeast, 30, 45)));
        Assert.Equal("met", Labels.ProgressText(O(Op.AtMost, 3, 1)));
        Assert.Equal("not met", Labels.ProgressText(O(Op.Exactly, 3, 1)));
    }

    [Fact]
    public void OverallText()
    {
        var ones = new List<ObjectiveInput>
        {
            new ObjectiveInput { Key = "a", Op = Op.AtLeast, Target = 1, HasValue = true, Value = 1 },
            new ObjectiveInput { Key = "b", Op = Op.AtLeast, Target = 1, HasValue = false }
        };
        Assert.Equal("1 / 2", Labels.OverallText(ProgressCalc.Compute("x", ones, false, false, OverallMode.Auto)));
        Assert.Equal("50%", Labels.OverallText(ProgressCalc.Compute("x", ones, false, false, OverallMode.Average)));
    }
}
