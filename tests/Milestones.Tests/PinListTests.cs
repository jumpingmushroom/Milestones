using Milestones.Core.Model;
using Xunit;

public class PinListTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("a,b", "a,b")]
    [InlineData(" a , ,b,a ", "a,b")]
    [InlineData("a,b,c,d", "a,b,c")]
    public void Parse_cleans_dedupes_and_caps(string raw, string expected)
    {
        Assert.Equal(expected, PinList.Parse(raw).Serialize());
    }

    [Fact]
    public void Add_refuses_duplicates_and_a_fourth()
    {
        PinList p = PinList.Parse("a,b");
        Assert.False(p.Add("a"));
        Assert.True(p.Add("c"));
        Assert.True(p.IsFull);
        Assert.False(p.Add("d"));
        Assert.Equal("a,b,c", p.Serialize());
    }

    [Fact]
    public void Remove_and_RemoveWhere()
    {
        PinList p = PinList.Parse("a,b,c");
        Assert.True(p.Remove("b"));
        Assert.False(p.Remove("b"));
        Assert.Equal(1, p.RemoveWhere(id => id == "c"));
        Assert.Equal("a", p.Serialize());
        Assert.True(p.Contains("a"));
        Assert.Equal(1, p.Count);
    }

    [Fact]
    public void Ids_with_commas_are_rejected()
    {
        Assert.False(PinList.Parse("").Add("a,b"));
    }
}
