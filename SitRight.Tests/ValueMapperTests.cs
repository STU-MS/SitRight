using Xunit;
using SitRight.Models;
using SitRight.Services;

namespace SitRight.Tests;

public class ValueMapperTests
{
    private readonly ValueMapper _mapper = new(hintStartLevel: 30, urgentLevel: 80);

    [Fact]
    public void Map_LevelZero_ReturnsMinimalMask()
    {
        var state = _mapper.Map(0);
        Assert.True(state.MaskOpacity < 0.1);
    }

    [Fact]
    public void Map_Level100_ReturnsMaxMask()
    {
        var state = _mapper.Map(100);
        Assert.True(state.MaskOpacity > 0.6);
    }

    [Fact]
    public void Map_Level50_ReturnsModerateMask()
    {
        var state = _mapper.Map(50);
        Assert.True(state.MaskOpacity > 0.1);
        Assert.True(state.MaskOpacity < 0.6);
    }

    [Fact]
    public void Map_LevelBelowHintStart_NoBlock()
    {
        var state = _mapper.Map(20);
        Assert.False(state.BlockInput);
    }

    [Fact]
    public void Map_LevelAboveUrgent_NeverBlocksInput()
    {
        var state = _mapper.Map(90);
        Assert.False(state.BlockInput);
        Assert.Equal(3, state.SeverityLevel);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void Map_MaskOpacity_AlwaysBetween0And1(int level)
    {
        var state = _mapper.Map(level);
        Assert.InRange(state.MaskOpacity, 0.0, 1.0);
    }

    [Fact]
    public void Map_EdgeOpacity_IsNonLinear()
    {
        var state1 = _mapper.Map(50);
        var state2 = _mapper.Map(100);

        Assert.True(state2.EdgeOpacity > state1.EdgeOpacity);
        Assert.True(state2.EdgeOpacity / state1.EdgeOpacity > 2);
    }

    [Fact]
    public void NewMapper_HasDefaultLevels()
    {
        var mapper = new ValueMapper();
        var state = mapper.Map(0);
        Assert.NotNull(state);
    }
}
