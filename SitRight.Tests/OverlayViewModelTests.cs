using Xunit;
using SitRight.Models;
using SitRight.ViewModels;

namespace SitRight.Tests;

public class OverlayViewModelTests
{
    [Fact]
    public void InitialState_IsInvisible()
    {
        var vm = new OverlayViewModel();
        Assert.False(vm.IsVisible);
        Assert.Equal(0, vm.MaskOpacity);
        Assert.Equal(0, vm.EdgeOpacity);
    }

    [Fact]
    public void InitialState_HasDefaultColor()
    {
        var vm = new OverlayViewModel();
        Assert.Equal("#FFFFFF", vm.MaskColor);
    }

    [Fact]
    public void UpdateFrom_SetsAllProperties()
    {
        var vm = new OverlayViewModel();
        var state = new OverlayState
        {
            MaskOpacity = 0.5,
            MaskColor = "#E0E0E0",
            EdgeOpacity = 0.2,
            SeverityLevel = 2
        };

        vm.UpdateFrom(state);

        Assert.Equal(0.5, vm.MaskOpacity);
        Assert.Equal("#E0E0E0", vm.MaskColor);
        Assert.Equal(0.2, vm.EdgeOpacity);
        Assert.Equal(2, vm.SeverityLevel);
    }

    [Fact]
    public void UpdateFrom_ZeroOpacity_SetsInvisible()
    {
        var vm = new OverlayViewModel();
        vm.UpdateFrom(new OverlayState { MaskOpacity = 0 });
        Assert.False(vm.IsVisible);
    }

    [Fact]
    public void UpdateFrom_NonZeroOpacity_SetsVisible()
    {
        var vm = new OverlayViewModel();
        vm.UpdateFrom(new OverlayState { MaskOpacity = 0.3 });
        Assert.True(vm.IsVisible);
    }

    [Fact]
    public void PropertyChanged_IsRaised()
    {
        var vm = new OverlayViewModel();
        var changedProperties = new List<string>();

        vm.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        vm.UpdateFrom(new OverlayState { MaskOpacity = 0.5 });

        Assert.Contains("MaskOpacity", changedProperties);
        Assert.Contains("IsVisible", changedProperties);
    }

    [Theory]
    [InlineData(0, "#FFFFFF")]
    [InlineData(100, "#9E9E9E")]
    public void UpdateFrom_RespectsSeverityColor(int level, string expectedColor)
    {
        var vm = new OverlayViewModel();
        var state = OverlayState.FromDisplayLevel(level);
        vm.UpdateFrom(state);

        Assert.Equal(expectedColor, vm.MaskColor);
    }
}
