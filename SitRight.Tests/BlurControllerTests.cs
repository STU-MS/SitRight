using Xunit;
using SitRight.Services;

namespace SitRight.Services;

public class BlurControllerTests
{
    [Fact]
    public void InitialDisplayValue_IsZero()
    {
        var controller = new BlurController();
        Assert.Equal(0, controller.DisplayValue);
    }

    [Fact]
    public void InitialTargetValue_IsZero()
    {
        var controller = new BlurController();
        Assert.Equal(0, controller.TargetValue);
    }

    [Fact]
    public void PushRawValue_UpdatesTargetValue()
    {
        var controller = new BlurController();
        controller.PushRawValue(50);
        Assert.Equal(50, controller.TargetValue);
    }

    [Fact]
    public void PushRawValue_UpdatesRawValue()
    {
        var controller = new BlurController();
        controller.PushRawValue(37);
        Assert.Equal(37, controller.RawValue);
    }

    [Fact]
    public void PushRawValue_DoesNotImmediatelyUpdateDisplayValue()
    {
        var controller = new BlurController();
        controller.PushRawValue(100);
        // Display value should still be 0 until Tick() is called
        Assert.Equal(0, controller.DisplayValue);
    }

    [Fact]
    public void Tick_MovesDisplayValueTowardTarget()
    {
        var controller = new BlurController(alpha: 1.0); // alpha=1 for immediate
        controller.PushRawValue(100);
        controller.Tick();
        Assert.Equal(100, controller.DisplayValue);
    }

    [Fact]
    public void Tick_WithPartialAlpha_MovesPartially()
    {
        var controller = new BlurController(alpha: 0.5);
        controller.PushRawValue(100);
        controller.Tick();
        // With alpha=0.5, display should move halfway: 0 + (100-0)*0.5 = 50
        Assert.Equal(50, controller.DisplayValue);
    }

    [Fact]
    public void Tick_MultipleCalls_ConvergesToTarget()
    {
        const double alpha = 0.18;
        var controller = new BlurController(alpha: alpha); // Recommended default
        controller.PushRawValue(100);

        // Simulate multiple ticks
        for (int i = 0; i < 10; i++)
        {
            controller.Tick();
        }

        var expected = 100 * (1 - Math.Pow(1 - alpha, 10));
        Assert.InRange(controller.DisplayValue, expected - 0.5, expected + 0.5);
    }

    [Fact]
    public void Tick_SmallValueBelowThreshold_ConvergesToZero()
    {
        var controller = new BlurController(smallValueThreshold: 5);
        controller.PushRawValue(3);

        // After ticks, should converge to 0
        controller.Tick();

        // Small values should quickly converge to zero
        Assert.True(controller.DisplayValue < 1);
    }

    [Fact]
    public void Reset_SetsAllValuesToZero()
    {
        var controller = new BlurController();
        controller.PushRawValue(50);
        controller.Tick(); // Move toward target

        controller.Reset();

        Assert.Equal(0, controller.RawValue);
        Assert.Equal(0, controller.TargetValue);
        Assert.Equal(0, controller.DisplayValue);
    }

    [Fact]
    public void OnDisplayValueChanged_EventIsRaisedOnTick()
    {
        var controller = new BlurController(alpha: 1.0);
        var changedValues = new List<double>();

        controller.OnDisplayValueChanged += value => changedValues.Add(value);
        controller.PushRawValue(50);
        controller.Tick();

        Assert.Contains(50, changedValues);
    }

    [Fact]
    public void Tick_DoesNotThrow()
    {
        var controller = new BlurController();
        controller.PushRawValue(50);
        var exception = Record.Exception(() => controller.Tick());
        Assert.Null(exception);
    }
}
