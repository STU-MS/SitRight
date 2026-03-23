using System;

namespace SitRight.Models;

public enum DeviceConnectionState
{
    Disconnected,
    Connecting,
    ConnectedIdle,
    Receiving,
    Timeout,
    Fault
}

public class DeviceState
{
    public DeviceConnectionState ConnectionState { get; set; } = DeviceConnectionState.Disconnected;
    public int RawValue { get; set; }
    public double DisplayValue { get; set; }
    public DateTime? LastReceiveTime { get; set; }
    public int ErrorCount { get; set; }
    public string? LastError { get; set; }
}
