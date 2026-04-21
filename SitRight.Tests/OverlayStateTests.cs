using Xunit;
using SitRight.Models;

namespace SitRight.Models;

public class OverlayStateTests
{
    [Fact]
    public void NewInstance_HasDefaultValues()
    {
        var state = new OverlayState();
        Assert.Equal(0, state.MaskOpacity);
        Assert.Equal("#FFFFFF", state.MaskColor);
        Assert.Equal(0, state.EdgeOpacity);
        Assert.Equal(string.Empty, state.MessageText);
        Assert.Equal(0, state.MessageOpacity);
        Assert.Equal(0, state.SeverityLevel);
    }

    [Fact]
    public void FromDisplayLevel_LevelZero_ReturnsMinimalMask()
    {
        var state = OverlayState.FromDisplayLevel(0);
        Assert.True(state.MaskOpacity < 0.1);
        Assert.Equal(string.Empty, state.MessageText);
        Assert.Equal(0, state.SeverityLevel);
    }

    [Fact]
    public void FromDisplayLevel_Level100_ReturnsMaxMask()
    {
        var state = OverlayState.FromDisplayLevel(100);
        Assert.True(state.MaskOpacity > 0.6);
        Assert.NotEmpty(state.MessageText);
        Assert.Equal(3, state.SeverityLevel);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(20, 0)]
    [InlineData(50, 1)]
    [InlineData(79, 2)]
    [InlineData(100, 3)]
    public void FromDisplayLevel_ReturnsCorrectSeverity(int level, int expectedSeverity)
    {
        var state = OverlayState.FromDisplayLevel(level);
        Assert.Equal(expectedSeverity, state.SeverityLevel);
    }
}
