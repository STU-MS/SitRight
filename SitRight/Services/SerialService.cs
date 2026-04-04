using System;
using System.IO.Ports;
using System.Text;

namespace SitRight.Services;

public class SerialService : ISerialService
{
    private SerialPort? _serialPort;
    private readonly StringBuilder _buffer = new();
    private bool _disposed;

    public event Action<string>? OnLineReceived;
    public event Action<Exception>? OnError;
    public event Action? OnConnected;
    public event Action? OnDisconnected;

    public bool IsConnected => _serialPort?.IsOpen ?? false;
    public string? CurrentPort => _serialPort?.PortName;

    public string[] GetAvailablePorts() => SerialPort.GetPortNames();

    public void Connect(string portName, int baudRate)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("Port name is required.", nameof(portName));

        Disconnect();

        _buffer.Clear();
        _serialPort = new SerialPort(portName, baudRate)
        {
            NewLine = "\n",
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };

        _serialPort.DataReceived += HandleDataReceived;
        _serialPort.ErrorReceived += HandleErrorReceived;
        _serialPort.Open();

        OnConnected?.Invoke();
    }

    public void Disconnect()
    {
        if (_serialPort is null)
            return;

        _serialPort.DataReceived -= HandleDataReceived;
        _serialPort.ErrorReceived -= HandleErrorReceived;

        try
        {
            if (_serialPort.IsOpen)
                _serialPort.Close();
        }
        finally
        {
            _serialPort.Dispose();
            _serialPort = null;
            _buffer.Clear();
            OnDisconnected?.Invoke();
        }
    }

    public void SendLine(string line)
    {
        if (_serialPort?.IsOpen == true)
            _serialPort.WriteLine(line);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Disconnect();
        _disposed = true;
    }

    private void HandleDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (sender is not SerialPort port)
                return;

            _buffer.Append(port.ReadExisting());

            while (true)
            {
                var current = _buffer.ToString();
                var newLineIndex = current.IndexOf('\n');
                if (newLineIndex < 0)
                    break;

                var line = current[..newLineIndex].TrimEnd('\r');
                _buffer.Remove(0, newLineIndex + 1);

                if (!string.IsNullOrEmpty(line))
                    OnLineReceived?.Invoke(line);
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
        }
    }

    private void HandleErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        OnError?.Invoke(new InvalidOperationException($"Serial error: {e.EventType}"));
    }
}
