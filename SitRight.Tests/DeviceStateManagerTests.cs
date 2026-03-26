using SitRight.Models;
using SitRight.Services;

namespace SitRight.Services;

public class DeviceStateManagerTests
{
    private readonly DeviceStateManager _manager = new(timeoutMs: 50);

    [Fact]
    public void InitialState_IsDisconnected()
    {
        Assert.Equal(DeviceConnectionState.Disconnected, _manager.State.ConnectionState);
    }

    [Fact]
    public void OnConnecting_TransitionsToConnecting()
    {
        _manager.OnConnecting();

        Assert.Equal(DeviceConnectionState.Connecting, _manager.State.ConnectionState);
    }

    [Fact]
    public void OnConnected_FromConnecting_TransitionsToConnectedIdle()
    {
        _manager.OnConnecting();
        _manager.OnConnected();

        Assert.Equal(DeviceConnectionState.ConnectedIdle, _manager.State.ConnectionState);
    }

    [Fact]
    public void ReceiveRawValue_FromConnectedIdle_TransitionsToReceiving()
    {
        _manager.OnConnecting();
        _manager.OnConnected();
        _manager.ReceiveRawValue(50);

        Assert.Equal(DeviceConnectionState.Receiving, _manager.State.ConnectionState);
        Assert.Equal(50, _manager.State.RawValue);
    }

    [Fact]
    public void ReceiveRawValue_UpdatesLastReceiveTime()
    {
        _manager.ReceiveRawValue(50);

        Assert.NotNull(_manager.State.LastReceiveTime);
    }

    [Fact]
    public void Disconnect_TransitionsToDisconnectedAndClearsState()
    {
        _manager.OnConnecting();
        _manager.OnConnected();
        _manager.ReceiveRawValue(50);
        _manager.Disconnect();

        Assert.Equal(DeviceConnectionState.Disconnected, _manager.State.ConnectionState);
        Assert.Equal(0, _manager.State.RawValue);
        Assert.Null(_manager.State.LastReceiveTime);
    }

    [Fact]
    public void OnFault_TransitionsToFault()
    {
        _manager.OnConnecting();
        _manager.OnConnected();
        _manager.OnFault("Test error");

        Assert.Equal(DeviceConnectionState.Fault, _manager.State.ConnectionState);
        Assert.Equal("Test error", _manager.State.LastError);
        Assert.Equal(1, _manager.State.ErrorCount);
    }

    [Fact]
    public void CheckTimeout_TransitionsToTimeout()
    {
        _manager.OnConnecting();
        _manager.OnConnected();
        _manager.ReceiveRawValue(50);
        Thread.Sleep(80);

        _manager.CheckTimeout();

        Assert.Equal(DeviceConnectionState.Timeout, _manager.State.ConnectionState);
    }

    [Fact]
    public void OnStateChanged_EventIsRaised()
    {
        var changedCount = 0;
        _manager.OnStateChanged += _ => changedCount++;

        _manager.ReceiveRawValue(50);

        Assert.Equal(1, changedCount);
    }
}
