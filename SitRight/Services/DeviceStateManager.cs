using System;
using SitRight.Models;

namespace SitRight.Services;

public class DeviceStateManager
{
    private readonly object _sync = new();
    private readonly int _timeoutMs;
    private DateTime? _lastReceiveTime;

    public event Action<DeviceState>? OnStateChanged;

    public DeviceState State { get; } = new();

    public DeviceStateManager(int timeoutMs = 2000)
    {
        _timeoutMs = timeoutMs;
    }

    public void OnConnecting()
    {
        lock (_sync)
        {
            UpdateState(state => state.ConnectionState = DeviceConnectionState.Connecting);
        }
    }

    public void OnConnected()
    {
        lock (_sync)
        {
            UpdateState(state =>
            {
                state.ConnectionState = DeviceConnectionState.ConnectedIdle;
                state.LastReceiveTime = null;
            });
        }
    }

    public void OnDisconnected()
    {
        Disconnect();
    }

    public void ReceiveRawValue(int value)
    {
        lock (_sync)
        {
            _lastReceiveTime = DateTime.Now;
            UpdateState(state =>
            {
                state.ConnectionState = DeviceConnectionState.Receiving;
                state.RawValue = value;
                state.LastReceiveTime = _lastReceiveTime;
            });
        }
    }

    public void OnFault(string error)
    {
        lock (_sync)
        {
            UpdateState(state =>
            {
                state.ConnectionState = DeviceConnectionState.Fault;
                state.LastError = error;
                state.ErrorCount++;
            });
        }
    }

    public void Disconnect()
    {
        lock (_sync)
        {
            _lastReceiveTime = null;
            UpdateState(state =>
            {
                state.ConnectionState = DeviceConnectionState.Disconnected;
                state.RawValue = 0;
                state.DisplayValue = 0;
                state.LastReceiveTime = null;
            });
        }
    }

    public void CheckTimeout()
    {
        lock (_sync)
        {
            if (!_lastReceiveTime.HasValue)
                return;

            if ((DateTime.Now - _lastReceiveTime.Value).TotalMilliseconds <= _timeoutMs)
                return;

            if (State.ConnectionState is DeviceConnectionState.Receiving or DeviceConnectionState.ConnectedIdle)
            {
                UpdateState(state => state.ConnectionState = DeviceConnectionState.Timeout);
            }
        }
    }

    private void UpdateState(Action<DeviceState> update)
    {
        update(State);
        OnStateChanged?.Invoke(State);
    }
}
