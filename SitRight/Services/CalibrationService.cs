using System;
using SitRight.Models;

namespace SitRight.Services;

public sealed class CalibrationService
{
    public int Normalize(int rawValue, int baseline)
    {
        if (baseline <= 0)
        {
            return Math.Clamp(rawValue, 0, 100);
        }

        return Math.Clamp(rawValue - baseline, 0, 100);
    }

    public void ApplyCalibration(AppConfig config, int rawValue, DateTime calibratedAt)
    {
        config.CalibrationBaseline = Math.Clamp(rawValue, 0, 100);
        config.CalibratedAt = calibratedAt;
    }
}
