using SitRight.Models;
using SitRight.Services;

namespace SitRight.Services;

public sealed class ConfigServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly ConfigService _service;

    public ConfigServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"sitright-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _service = new ConfigService(_testDir);
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaultsAndCreatesFile()
    {
        var config = _service.Load();

        Assert.Equal("COM1", config.DefaultComPort);
        Assert.Equal(0, config.CalibrationBaseline);
        Assert.Null(config.CalibratedAt);
        Assert.True(File.Exists(Path.Combine(_testDir, "config.json")));
    }

    [Fact]
    public void Save_ThenLoad_PreservesCalibrationFields()
    {
        var now = new DateTime(2026, 4, 14, 21, 30, 0, DateTimeKind.Local);
        _service.Save(new AppConfig
        {
            CalibrationBaseline = 42,
            CalibratedAt = now
        });

        var reloaded = new ConfigService(_testDir).Load();

        Assert.Equal(42, reloaded.CalibrationBaseline);
        Assert.Equal(now, reloaded.CalibratedAt);
    }

    [Fact]
    public void Update_ModifiesPersistedConfig()
    {
        _service.Load();
        _service.Update(config => config.CalibrationBaseline = 77);

        var reloaded = new ConfigService(_testDir).Load();

        Assert.Equal(77, reloaded.CalibrationBaseline);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }
}
