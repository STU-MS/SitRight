using SitRight.Models;
using SitRight.Services;

namespace SitRight.Services;

public class CalibrationServiceTests
{
    [Fact]
    public void Normalize_WhenNoBaseline_ReturnsRawValue()
    {
        var service = new CalibrationService();

        Assert.Equal(55, service.Normalize(rawValue: 55, baseline: 0));
    }

    [Fact]
    public void Normalize_WhenRawBelowBaseline_ReturnsZero()
    {
        var service = new CalibrationService();

        Assert.Equal(0, service.Normalize(rawValue: 40, baseline: 60));
    }

    [Fact]
    public void Normalize_WhenRawAboveBaseline_ReturnsDelta()
    {
        var service = new CalibrationService();

        Assert.Equal(13, service.Normalize(rawValue: 73, baseline: 60));
    }

    [Fact]
    public void ApplyCalibration_WritesBaselineAndTimestamp()
    {
        var service = new CalibrationService();
        var config = new AppConfig();
        var now = new DateTime(2026, 4, 14, 22, 15, 0, DateTimeKind.Local);

        service.ApplyCalibration(config, rawValue: 61, calibratedAt: now);

        Assert.Equal(61, config.CalibrationBaseline);
        Assert.Equal(now, config.CalibratedAt);
    }
}
