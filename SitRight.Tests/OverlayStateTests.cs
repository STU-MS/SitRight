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
    public void FromDisplayLevel_LevelZero_ReturnsNoMessage()
    {
        var state = OverlayState.FromDisplayLevel(0);

        Assert.Equal(string.Empty, state.MessageText);
        Assert.Equal(0, state.MessageOpacity);
        Assert.Equal(0, state.SeverityLevel);
    }

    [Fact]
    public void FromDisplayLevel_HintThreshold_ShowsReminder()
    {
        var state = OverlayState.FromDisplayLevel(35, hintStart: 30, urgentLevel: 80);

        Assert.Equal("请调整坐姿", state.MessageText);
        Assert.True(state.MessageOpacity > 0);
    }

    [Fact]
    public void FromDisplayLevel_UrgentThreshold_ShowsUrgentReminder()
    {
        var state = OverlayState.FromDisplayLevel(85, hintStart: 30, urgentLevel: 80);

        Assert.Equal("请立即调整坐姿！", state.MessageText);
        Assert.True(state.MessageOpacity > 0.9);
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
