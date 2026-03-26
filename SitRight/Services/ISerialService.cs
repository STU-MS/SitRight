using System;

namespace SitRight.Services;

public interface ISerialService : IDisposable
{
    event Action<string>? OnLineReceived;
    event Action<Exception>? OnError;
    event Action? OnConnected;
    event Action? OnDisconnected;

    bool IsConnected { get; }
    string? CurrentPort { get; }

    void Connect(string portName, int baudRate);
    void Disconnect();
    string[] GetAvailablePorts();
}
