using Xunit;
using SitRight.Services;
using SitRight.Models;

namespace SitRight.Tests;

public class ConfigServiceTests : IDisposable
{
    private readonly string _testPath;
    private readonly ConfigService _service;

    public ConfigServiceTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"test_config_{Guid.NewGuid()}.json");
        _service = new ConfigService(_testPath);
    }

    [Fact]
    public void Load_WhenNoConfig_CreatesDefault()
    {
        var config = _service.Load();
        Assert.NotNull(config);
        Assert.Equal(115200, config.BaudRate);
        Assert.Equal(2000, config.TimeoutThresholdMs);
    }

    [Fact]
    public void Load_WhenConfigExists_LoadsValues()
    {
        var initialConfig = new AppConfig
        {
            DefaultComPort = "COM5",
            BaudRate = 9600,
            CalibratedNormalAngle = 12.5,
            CalibratedSlouchAngle = 26.5
        };
        _service.Save(initialConfig);

        var newService = new ConfigService(_testPath);
        var loaded = newService.Load();

        Assert.Equal("COM5", loaded.DefaultComPort);
        Assert.Equal(9600, loaded.BaudRate);
        Assert.Equal(12.5, loaded.CalibratedNormalAngle);
        Assert.Equal(26.5, loaded.CalibratedSlouchAngle);
    }

    [Fact]
    public void Save_WritesJsonFile()
    {
        var config = new AppConfig { DefaultComPort = "COM3" };
        _service.Save(config);

        Assert.True(File.Exists(_testPath));
    }

    [Fact]
    public void Update_ModifiesAndSaves()
    {
        _service.Load();
        _service.Update(c => c.DefaultComPort = "COM7");

        var reloaded = _service.Load();
        Assert.Equal("COM7", reloaded.DefaultComPort);
    }

    [Fact]
    public void Load_CachesConfig()
    {
        var config1 = _service.Load();
        var config2 = _service.Load();
        Assert.Same(config1, config2);
    }

    [Fact]
    public void Save_InvalidatesCache()
    {
        var config1 = _service.Load();
        _service.Save(new AppConfig { DefaultComPort = "COM9" });
        var config2 = _service.Load();

        Assert.NotSame(config1, config2);
        Assert.Equal("COM9", config2.DefaultComPort);
    }

    [Fact]
    public void Save_PersistsCalibrationFields()
    {
        var calibratedAt = new DateTime(2026, 4, 15, 9, 30, 0, DateTimeKind.Local);

        _service.Save(new AppConfig
        {
            CalibratedNormalAngle = 11.2,
            CalibratedSlouchAngle = 24.8,
            CalibratedAt = calibratedAt
        });

        var reloaded = new ConfigService(_testPath).Load();

        Assert.Equal(11.2, reloaded.CalibratedNormalAngle);
        Assert.Equal(24.8, reloaded.CalibratedSlouchAngle);
        Assert.Equal(calibratedAt, reloaded.CalibratedAt);
    }

    public void Dispose()
    {
        if (File.Exists(_testPath))
            File.Delete(_testPath);
    }
}
