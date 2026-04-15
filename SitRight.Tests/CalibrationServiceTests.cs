using SitRight.Models;
using SitRight.Services;

namespace SitRight.Tests;

public class CalibrationServiceTests
{
    private readonly CalibrationService _service = new();

    [Fact]
    public void RestoreFromConfig_WhenEmptyConfig_KeepsNotCalibrated()
    {
        var config = new AppConfig();
        var calibrationData = new CalibrationData();
        var mapper = new ValueMapper();

        var restored = _service.RestoreFromConfig(config, calibrationData, mapper);

        Assert.False(restored);
        Assert.Equal(CalibrationState.NotCalibrated, calibrationData.State);
    }

    [Fact]
    public void RestoreFromConfig_WhenOnlyNormalAngleExists_RestoresPartialCalibration()
    {
        var calibratedAt = new DateTime(2026, 4, 15, 8, 0, 0, DateTimeKind.Local);
        var config = new AppConfig
        {
            CalibratedNormalAngle = 10.5,
            CalibratedAt = calibratedAt
        };
        var calibrationData = new CalibrationData();
        var mapper = new ValueMapper();

        var restored = _service.RestoreFromConfig(config, calibrationData, mapper);

        Assert.True(restored);
        Assert.Equal(CalibrationState.NormalSet, calibrationData.State);
        Assert.Equal(10.5, calibrationData.NormalAngle);
        Assert.Null(calibrationData.SlouchAngle);
        Assert.Equal(calibratedAt, calibrationData.LastCalibrated);
    }

    [Fact]
    public void RestoreFromConfig_WhenFullCalibrationExists_ConfiguresMapper()
    {
        var config = new AppConfig
        {
            CalibratedNormalAngle = 10,
            CalibratedSlouchAngle = 30,
            CalibratedAt = new DateTime(2026, 4, 15, 8, 30, 0, DateTimeKind.Local)
        };
        var calibrationData = new CalibrationData();
        var mapper = new ValueMapper();

        var restored = _service.RestoreFromConfig(config, calibrationData, mapper);
        var state = mapper.Map(30);

        Assert.True(restored);
        Assert.Equal(CalibrationState.FullyCalibrated, calibrationData.State);
        Assert.Equal(10, calibrationData.NormalAngle);
        Assert.Equal(30, calibrationData.SlouchAngle);
        Assert.True(state.MaskOpacity > 0.6);
    }

    [Fact]
    public void PersistToConfig_CopiesCalibrationData()
    {
        var calibratedAt = new DateTime(2026, 4, 15, 10, 0, 0, DateTimeKind.Local);
        var config = new AppConfig();
        var calibrationData = new CalibrationData
        {
            State = CalibrationState.FullyCalibrated,
            NormalAngle = 11,
            SlouchAngle = 25,
            LastCalibrated = calibratedAt
        };

        _service.PersistToConfig(config, calibrationData);

        Assert.Equal(11, config.CalibratedNormalAngle);
        Assert.Equal(25, config.CalibratedSlouchAngle);
        Assert.Equal(calibratedAt, config.CalibratedAt);
    }
}
