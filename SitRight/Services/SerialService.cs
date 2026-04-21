using System;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace SitRight.Services;

public class SerialService : ISerialService
{
    private SerialPort? _serialPort;
    private readonly StringBuilder _buffer = new();
    private bool _disposed;
    private readonly object _bufferLock = new();
    private readonly SynchronizationContext _uiContext = SynchronizationContext.Current ?? new();

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

        lock (_bufferLock)
        {
            _buffer.Clear();
        }

        _serialPort = new SerialPort(portName, baudRate)
        {
            NewLine = "\n",
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };

        _serialPort.DataReceived += HandleDataReceived;
        _serialPort.ErrorReceived += HandleErrorReceived;

        try
        {
            _serialPort.Open();
            _uiContext.Post(_ => OnConnected?.Invoke(), null);
        }
        catch (Exception ex)
        {
            _uiContext.Post(_ => OnError?.Invoke(ex), null);
            Disconnect();
        }
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
        catch (Exception ex)
        {
            _uiContext.Post(_ => OnError?.Invoke(ex), null);
        }
        finally
        {
            _serialPort.Dispose();
            _serialPort = null;
            lock (_bufferLock)
            {
                _buffer.Clear();
            }
            _uiContext.Post(_ => OnDisconnected?.Invoke(), null);
        }
    }

    public void SendLine(string line)
    {
        try
        {
            if (_serialPort?.IsOpen == true && !string.IsNullOrEmpty(line))
                _serialPort.WriteLine(line);
        }
        catch (Exception ex)
        {
            _uiContext.Post(_ => OnError?.Invoke(ex), null);
        }
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
            if (sender is not SerialPort port || !port.IsOpen)
                return;

            string data = port.ReadExisting();
            if (string.IsNullOrEmpty(data))
                return;

            //加锁操作StringBuilder，保证线程安全
            lock (_bufferLock)
            {
                _buffer.Append(data);

                while (true)
                {
                    var current = _buffer.ToString();
                    var newLineIndex = current.IndexOf('\n');
                    if (newLineIndex < 0)
                        break;

                    var line = current[..newLineIndex].TrimEnd('\r');
                    _buffer.Remove(0, newLineIndex + 1);

                    if (!string.IsNullOrEmpty(line))
                    {
                        _uiContext.Post(_ => OnLineReceived?.Invoke(line), null);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _uiContext.Post(_ => OnError?.Invoke(ex), null);
        }
    }

    private void HandleErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        _uiContext.Post(_ => OnError?.Invoke(new InvalidOperationException($"Serial error: {e.EventType}")), null);
    }
}
