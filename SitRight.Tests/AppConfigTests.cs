using Xunit;
using SitRight.Models;

namespace SitRight.Models;

public class AppConfigTests
{
    [Fact]
    public void NewInstance_HasRecommendedDefaults()
    {
        var config = new AppConfig();
        Assert.Equal("COM1", config.DefaultComPort);
        Assert.Equal(115200, config.BaudRate);
        Assert.Equal(2000, config.TimeoutThresholdMs);
        Assert.Equal(0.70, config.MaxMaskOpacity);
        Assert.Equal(30, config.HintStartLevel);
        Assert.Equal(80, config.UrgentLevel);
    }
}
