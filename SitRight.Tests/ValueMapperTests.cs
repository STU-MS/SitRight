using Xunit;
using SitRight.Models;
using SitRight.Services;

namespace SitRight.Tests;

public class ValueMapperTests
{
    private readonly ValueMapper _mapper;

    public ValueMapperTests()
    {
        _mapper = new ValueMapper(hintStartLevel: 30, urgentLevel: 80);
    }

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
        Assert.True(state.MaskOpacity < 0.3);
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
    [InlineData(0, "#FFFFFF")]
    [InlineData(50, "#E0E0E0")]
    [InlineData(70, "#BDBDBD")]
    [InlineData(100, "#9E9E9E")]
    public void Map_Level_ReturnsCorrectColor(int level, string expectedColor)
    {
        var state = _mapper.Map(level);
        Assert.Equal(expectedColor, state.MaskColor);
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
