using SitRight.Services;

namespace SitRight.Services;

public class ISerialServiceTests
{
    [Fact]
    public void ISerialService_DefinesRequiredMembers()
    {
        var type = typeof(ISerialService);

        Assert.True(type.IsInterface);
        Assert.NotNull(type.GetProperty(nameof(ISerialService.IsConnected)));
        Assert.NotNull(type.GetProperty(nameof(ISerialService.CurrentPort)));
        Assert.NotNull(type.GetEvent(nameof(ISerialService.OnLineReceived)));
        Assert.NotNull(type.GetEvent(nameof(ISerialService.OnError)));
        Assert.NotNull(type.GetEvent(nameof(ISerialService.OnConnected)));
        Assert.NotNull(type.GetEvent(nameof(ISerialService.OnDisconnected)));
    }

    [Fact]
    public void SerialService_ImplementsISerialService()
    {
        Assert.True(typeof(ISerialService).IsAssignableFrom(typeof(SerialService)));
    }
}
