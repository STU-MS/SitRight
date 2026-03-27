namespace SitRight.Services;

/// <summary>
/// BlurController：第5章核心数据流、第11章关键模块说明
/// 维护 RawValue、TargetValue、DisplayValue 三级状态
/// 使用指数滑动平均实现平滑过渡
/// </summary>
public class BlurController
{
    private readonly double _alpha;
    private readonly double _smallValueThreshold;
    private readonly double _convergeSpeed;

    public event Action<double>? OnDisplayValueChanged;

    public double RawValue { get; private set; }
    public double TargetValue { get; private set; }
    public double DisplayValue { get; private set; }

    public BlurController(
        double alpha = 0.18,
        double smallValueThreshold = 5,
        double convergeSpeed = 0.1)
    {
        _alpha = alpha;
        _smallValueThreshold = smallValueThreshold;
        _convergeSpeed = convergeSpeed;
    }

    public void PushRawValue(int value)
    {
        RawValue = value;
        TargetValue = value;
    }

    public void Tick()
    {
        if (TargetValue < _smallValueThreshold)
        {
            DisplayValue = Lerp(DisplayValue, 0, _convergeSpeed * 3);
        }
        else
        {
            DisplayValue = Lerp(DisplayValue, TargetValue, _alpha);
        }

        if (Math.Abs(DisplayValue) < 0.01)
            DisplayValue = 0;

        OnDisplayValueChanged?.Invoke(DisplayValue);
    }

    public void Reset()
    {
        RawValue = 0;
        TargetValue = 0;
        DisplayValue = 0;
        OnDisplayValueChanged?.Invoke(DisplayValue);
    }

    private static double Lerp(double current, double target, double alpha)
    {
        return current + (target - current) * alpha;
    }
}
