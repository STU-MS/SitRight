using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SitRight.Models;
using SitRight.Services;

namespace SitRight.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ISerialService _serialService;
    private readonly DeviceProtocol _protocol;
    private readonly DeviceStateManager _stateManager;
    private readonly ValueMapper _valueMapper;
    private readonly ConfigService _configService;
    private readonly AppConfig _config;

    private string _statusText = "Disconnected";
    private string _rawValueText = "--";
    private string _displayValueText = "--";
    private string _lastReceiveTimeText = "--";
    private bool _isConnected;
    private bool _isSimulationMode;
    private string _calibrationStatusText = "未校准";

    public MainViewModel(
        ISerialService serialService,
        DeviceProtocol protocol,
        DeviceStateManager stateManager,
        ValueMapper valueMapper,
        ConfigService configService)
    {
        _serialService = serialService;
        _protocol = protocol;
        _stateManager = stateManager;
        _valueMapper = valueMapper;
        _configService = configService;
        _config = _configService.Load();

        BindEvents();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<DeviceConnectionState>? OnConnectionStateChanged;
    public event Action<OverlayState>? OnOverlayStateChanged;
    public event Action<bool>? OnSimulationModeChanged;
    public event Action<CalibrationData>? OnCalibrationChanged;
    public event Action<string>? OnLog;

    public CalibrationData CalibrationData { get; } = new();

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string RawValueText
    {
        get => _rawValueText;
        private set => SetProperty(ref _rawValueText, value);
    }

    public string DisplayValueText
    {
        get => _displayValueText;
        private set => SetProperty(ref _displayValueText, value);
    }

    public string LastReceiveTimeText
    {
        get => _lastReceiveTimeText;
        private set => SetProperty(ref _lastReceiveTimeText, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }

    public bool IsSimulationMode
    {
        get => _isSimulationMode;
        set
        {
            if (SetProperty(ref _isSimulationMode, value))
            {
                OnSimulationModeChanged?.Invoke(value);
            }
        }
    }

    public string CalibrationStatusText
    {
        get => _calibrationStatusText;
        private set => SetProperty(ref _calibrationStatusText, value);
    }

    public string[] AvailablePorts => _serialService.GetAvailablePorts();

    private void BindEvents()
    {
        _serialService.OnLineReceived += line =>
        {
            OnLog?.Invoke($"[DIAG-1] serial: \"{line}\"");

            if (!_protocol.TryParseFull(line, out var type, out var value, out var ack, out var err))
            {
                OnLog?.Invoke($"[DIAG-2] parse failed: \"{line}\"");
                return;
            }

            switch (type)
            {
                case ProtocolLineType.RuntimeData:
                    HandleInputValue(value, updateDeviceState: true);
                    break;

                case ProtocolLineType.CalibrationAck:
                    HandleCalibrationAck(ack!);
                    break;

                case ProtocolLineType.CalibrationErr:
                    CalibrationData.ApplyError(err!);
                    UpdateCalibrationUI();
                    OnCalibrationChanged?.Invoke(CalibrationData);
                    break;
            }
        };

        _serialService.OnError += ex => _stateManager.OnFault(ex.Message);

        _serialService.OnConnected += () =>
        {
            _stateManager.OnConnected();
            IsConnected = true;
        };

        _serialService.OnDisconnected += () =>
        {
            _stateManager.OnDisconnected();
            _stateManager.Disconnect();
            IsConnected = false;
        };

        _stateManager.OnStateChanged += state =>
        {
            StatusText = state.ConnectionState.ToString();
            OnConnectionStateChanged?.Invoke(state.ConnectionState);
        };
    }

    private void HandleInputValue(int value, bool updateDeviceState)
    {
        if (updateDeviceState)
            _stateManager.ReceiveRawValue(value);

        RawValueText = value.ToString();
        LastReceiveTimeText = DateTime.Now.ToString("HH:mm:ss");
        DisplayValueText = value.ToString();

        // 收到 RuntimeData 意味着固件已校准（未校准固件不发数据）
        if (CalibrationData.State != CalibrationState.FullyCalibrated)
        {
            CalibrationData.State = CalibrationState.FullyCalibrated;
            UpdateCalibrationUI();
            OnCalibrationChanged?.Invoke(CalibrationData);
        }

        var overlayState = _valueMapper.Map(value);
        OnOverlayStateChanged?.Invoke(overlayState);
    }

    private void HandleCalibrationAck(CalibrationAckData ack)
    {
        CalibrationData.ApplyAck(ack);
        UpdateCalibrationUI();
        OnCalibrationChanged?.Invoke(CalibrationData);
        ClearOverlayState();
    }

    private void ClearOverlayState()
    {
        DisplayValueText = "0";
        OnOverlayStateChanged?.Invoke(new OverlayState());
    }

    private void UpdateCalibrationUI()
    {
        CalibrationStatusText = CalibrationData.State switch
        {
            CalibrationState.NotCalibrated => "未校准",
            CalibrationState.NormalSet => "已校准坐正",
            CalibrationState.FullyCalibrated => "完全校准",
            CalibrationState.Error => $"错误: {CalibrationData.LastError}",
            _ => "--"
        };
    }

    public void Connect(string portName, int baudRate)
    {
        _stateManager.OnConnecting();
        _serialService.Connect(portName, baudRate);
    }

    public void Disconnect()
    {
        _serialService.Disconnect();
        _stateManager.Disconnect();
        IsConnected = false;
    }

    public void SimulateValue(int value)
    {
        if (!_isSimulationMode)
            return;

        HandleInputValue(value, updateDeviceState: false);
    }

    public void SendCalibrationCommand(string command)
    {
        if (_isConnected)
            _serialService.SendLine($"CMD:{command}");
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
