using System;
using SitRight.Models;

namespace SitRight.Services;

public sealed class CalibrationService
{
    public bool RestoreFromConfig(AppConfig config, CalibrationData calibrationData, ValueMapper mapper)
    {
        calibrationData.Reset();
        calibrationData.LastCalibrated = config.CalibratedAt;

        if (!config.CalibratedNormalAngle.HasValue)
            return false;

        calibrationData.NormalAngle = config.CalibratedNormalAngle.Value;
        calibrationData.State = CalibrationState.NormalSet;

        if (config.CalibratedSlouchAngle.HasValue)
        {
            calibrationData.SlouchAngle = config.CalibratedSlouchAngle.Value;
            calibrationData.State = CalibrationState.FullyCalibrated;
            mapper.SetCalibration(
                (int)Math.Round(config.CalibratedNormalAngle.Value),
                (int)Math.Round(config.CalibratedSlouchAngle.Value));
        }

        return true;
    }

    public void PersistToConfig(AppConfig config, CalibrationData calibrationData)
    {
        if (calibrationData.State == CalibrationState.NotCalibrated)
        {
            ClearPersistedCalibration(config);
            return;
        }

        config.CalibratedNormalAngle = calibrationData.NormalAngle;
        config.CalibratedSlouchAngle = calibrationData.SlouchAngle;
        config.CalibratedAt = calibrationData.LastCalibrated;
    }

    public void ClearPersistedCalibration(AppConfig config)
    {
        config.CalibratedNormalAngle = null;
        config.CalibratedSlouchAngle = null;
        config.CalibratedAt = null;
    }
}
