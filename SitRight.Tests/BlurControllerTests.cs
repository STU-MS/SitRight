using SitRight.Services;

namespace SitRight.Services;

public class BlurControllerTests
{
    [Fact]
    public void InitialState_IsZero()
    {
        var controller = new BlurController();
        Assert.Equal(0, controller.RawValue);
        Assert.Equal(0, controller.TargetValue);
        Assert.Equal(0, controller.DisplayValue);
    }

    [Fact]
    public void PushRawValue_UpdatesRawAndTarget()
    {
        var controller = new BlurController();

        controller.PushRawValue(80);

        Assert.Equal(80, controller.RawValue);
        Assert.Equal(80, controller.TargetValue);
        Assert.Equal(0, controller.DisplayValue);
    }

    [Fact]
    public void Tick_WithAlphaOne_JumpsToTarget()
    {
        var controller = new BlurController(alpha: 1.0);
        controller.PushRawValue(65);

        controller.Tick();

        Assert.Equal(65, controller.DisplayValue, 3);
    }

    [Fact]
    public void Reset_ClearsResidualBlurImmediately()
    {
        var controller = new BlurController(alpha: 1.0);
        controller.PushRawValue(100);
        controller.Tick();

        controller.Reset();

        Assert.Equal(0, controller.RawValue);
        Assert.Equal(0, controller.TargetValue);
        Assert.Equal(0, controller.DisplayValue);
    }
}
