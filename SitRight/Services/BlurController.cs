using System;

namespace SitRight.Services;

public sealed class BlurController
{
    private readonly double _alpha;
    private readonly double _snapThreshold;

    public event Action<double>? DisplayValueChanged;

    public double RawValue { get; private set; }
    public double TargetValue { get; private set; }
    public double DisplayValue { get; private set; }

    public BlurController(double alpha = 0.18, double snapThreshold = 0.01)
    {
        _alpha = alpha;
        _snapThreshold = snapThreshold;
    }

    public void PushRawValue(int value)
    {
        RawValue = value;
        TargetValue = value;
    }

    public void Tick()
    {
        DisplayValue += (TargetValue - DisplayValue) * _alpha;

        if (Math.Abs(TargetValue - DisplayValue) < _snapThreshold)
        {
            DisplayValue = TargetValue;
        }

        if (Math.Abs(DisplayValue) < _snapThreshold)
        {
            DisplayValue = 0;
        }

        DisplayValueChanged?.Invoke(DisplayValue);
    }

    public void Reset()
    {
        RawValue = 0;
        TargetValue = 0;
        DisplayValue = 0;
        DisplayValueChanged?.Invoke(DisplayValue);
    }
}
