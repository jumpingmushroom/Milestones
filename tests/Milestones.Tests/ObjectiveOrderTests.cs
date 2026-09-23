using System.Collections.Generic;
using Milestones.Core.Model;
using Xunit;

public class ObjectiveOrderTests
{
    private static AchievementProgress P(params float[] fractions)
    {
        var inputs = new List<ObjectiveInput>();
        for (int i = 0; i < fractions.Length; i++)
            inputs.Add(new ObjectiveInput { Key = "o" + i, Label = "o" + i, Op = Op.AtLeast, Target = 100, HasValue = true, Value = fractions[i] * 100 });
        return ProgressCalc.Compute("x", inputs, false, false, OverallMode.Average);
    }

    [Fact]
    public void Sorted_puts_unmet_first_closest_to_done_first_and_met_last_in_game_order()
    {
        // index: 0 met, 1 at 20%, 2 met, 3 at 80%, 4 at 20%
        AchievementProgress p = P(1f, 0.2f, 1f, 0.8f, 0.2f);
        Assert.Equal(new List<int> { 3, 1, 4, 0, 2 }, ObjectiveOrder.Sorted(p));
    }

    [Fact]
    public void Select_shows_everything_when_small()
    {
        TrackerRows r = ObjectiveOrder.Select(P(1f, 0.5f, 0f), 4);
        Assert.Equal(new List<int> { 0, 1, 2 }, r.Indices);
        Assert.Equal(0, r.More);
    }

    [Fact]
    public void Select_shows_unfinished_closest_first_and_counts_the_rest()
    {
        // 6 objectives, max 2: unmet are 1 (.5), 2 (0), 4 (.9), 5 (.1); met are 0 and 3.
        TrackerRows r = ObjectiveOrder.Select(P(1f, 0.5f, 0f, 1f, 0.9f, 0.1f), 2);
        Assert.Equal(new List<int> { 4, 1 }, r.Indices);
        Assert.Equal(2, r.More);
    }
}
