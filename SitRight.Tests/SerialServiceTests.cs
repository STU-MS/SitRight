using SitRight.Services;

namespace SitRight.Services;

public class SerialServiceTests
{
    [Fact]
    public void GetAvailablePorts_ReturnsArray()
    {
        using var service = new SerialService();

        var ports = service.GetAvailablePorts();

        Assert.NotNull(ports);
    }

    [Fact]
    public void IsConnected_WhenNotConnected_ReturnsFalse()
    {
        using var service = new SerialService();

        Assert.False(service.IsConnected);
    }

    [Fact]
    public void CurrentPort_WhenNotConnected_ReturnsNull()
    {
        using var service = new SerialService();

        Assert.Null(service.CurrentPort);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var service = new SerialService();
        service.Dispose();

        var exception = Record.Exception(service.Dispose);

        Assert.Null(exception);
    }

    [Fact]
    public void SendLine_WhenNotConnected_DoesNotThrow()
    {
        using var service = new SerialService();

        var exception = Record.Exception(() => service.SendLine("CMD:SET_NORMAL"));

        Assert.Null(exception);
    }
}
