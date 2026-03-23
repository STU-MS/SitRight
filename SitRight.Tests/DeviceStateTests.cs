using Xunit;
using SitRight.Models;

namespace SitRight.Models;

public class DeviceStateTests
{
    [Fact]
    public void NewInstance_HasDefaultValues()
    {
        var state = new DeviceState();
        Assert.Equal(DeviceConnectionState.Disconnected, state.ConnectionState);
        Assert.Equal(0, state.RawValue);
        Assert.Equal(0, state.DisplayValue);
        Assert.Null(state.LastReceiveTime);
    }

    [Fact]
    public void DeviceConnectionState_HasAllExpectedValues()
    {
        var states = Enum.GetValues<DeviceConnectionState>();
        Assert.Contains(DeviceConnectionState.Disconnected, states);
        Assert.Contains(DeviceConnectionState.Connecting, states);
        Assert.Contains(DeviceConnectionState.ConnectedIdle, states);
        Assert.Contains(DeviceConnectionState.Receiving, states);
        Assert.Contains(DeviceConnectionState.Timeout, states);
        Assert.Contains(DeviceConnectionState.Fault, states);
    }
}
