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

    private string _statusText = "Disconnected";
    private string _rawValueText = "--";
    private string _displayValueText = "--";
    private string _lastReceiveTimeText = "--";
    private bool _isConnected;
    private bool _isSimulationMode;
    private string _calibrationStatusText = "未校准";
    private string _normalAngleText = "--";
    private string _slouchAngleText = "--";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<DeviceConnectionState>? OnConnectionStateChanged;
    public event Action<OverlayState>? OnOverlayStateChanged;
    public event Action<bool>? OnSimulationModeChanged;
    public event Action<CalibrationData>? OnCalibrationChanged;

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

    public string NormalAngleText
    {
        get => _normalAngleText;
        private set => SetProperty(ref _normalAngleText, value);
    }

    public string SlouchAngleText
    {
        get => _slouchAngleText;
        private set => SetProperty(ref _slouchAngleText, value);
    }

    public string[] AvailablePorts => _serialService.GetAvailablePorts();

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

        BindEvents();
    }

    public event Action<string>? OnLog;

    private void BindEvents()
    {
        _serialService.OnLineReceived += line =>
        {
            // [诊断1] 串口原始数据
            OnLog?.Invoke($"[DIAG-1] 串口收到: \"{line}\"");

            if (_protocol.TryParseFull(line, out var type, out var value, out var ack, out var err))
            {
                // [诊断2] 协议解析结果
                OnLog?.Invoke($"[DIAG-2] 解析成功: type={type}, value={value}");

                switch (type)
                {
                    case ProtocolLineType.RuntimeData:
                        _stateManager.ReceiveRawValue(value);
                        RawValueText = value.ToString();
                        LastReceiveTimeText = DateTime.Now.ToString("HH:mm:ss");

                        // 只有在完全校准后才触发遮罩渲染
                        if (CalibrationData.State == CalibrationState.FullyCalibrated)
                        {
                            var overlayState = _valueMapper.Map(value);
                            DisplayValueText = value.ToString();
                            // [诊断3] Overlay 状态计算结果
                            OnLog?.Invoke($"[DIAG-3] Overlay: opacity={overlayState.MaskOpacity:F3}, color={overlayState.MaskColor}, edge={overlayState.EdgeOpacity:F3}");
                            // [诊断4] 即将触发 OnOverlayStateChanged
                            OnLog?.Invoke($"[DIAG-4] OnOverlayStateChanged 订阅者数: {OnOverlayStateChanged?.GetInvocationList()?.Length ?? 0}");
                            OnOverlayStateChanged?.Invoke(overlayState);
                        }
                        else
                        {
                            DisplayValueText = value.ToString();
                            // 校准期间不触发遮罩渲染，确保屏幕不会变灰
                            OnLog?.Invoke($"[DIAG-3] 校准未完成，跳过遮罩渲染");
                        }
                        break;

                    case ProtocolLineType.CalibrationAck:
                        CalibrationData.ApplyAck(ack!);
                        UpdateCalibrationUI();
                        OnCalibrationChanged?.Invoke(CalibrationData);
                        break;

                    case ProtocolLineType.CalibrationErr:
                        CalibrationData.ApplyError(err!);
                        UpdateCalibrationUI();
                        OnCalibrationChanged?.Invoke(CalibrationData);
                        break;
                }
            }
            else
            {
                // [诊断2] 协议解析失败 — 关键：数据格式不匹配！
                OnLog?.Invoke($"[DIAG-2] 解析失败，丢弃: \"{line}\"");
            }
        };

        _serialService.OnError += ex =>
        {
            _stateManager.OnFault(ex.Message);
        };

        _serialService.OnConnected += () =>
        {
            _stateManager.OnConnected();
            IsConnected = true;
        };

        _serialService.OnDisconnected += () =>
        {
            _stateManager.OnDisconnected();
            IsConnected = false;
        };

        _stateManager.OnStateChanged += state =>
        {
            StatusText = state.ConnectionState.ToString();
            OnConnectionStateChanged?.Invoke(state.ConnectionState);
        };
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

        NormalAngleText = CalibrationData.NormalAngle.HasValue
            ? $"{CalibrationData.NormalAngle.Value:F2}°"
            : "--";

        SlouchAngleText = CalibrationData.SlouchAngle.HasValue
            ? $"{CalibrationData.SlouchAngle.Value:F2}°"
            : "--";
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
        if (_isSimulationMode)
        {
            RawValueText = value.ToString();
            LastReceiveTimeText = DateTime.Now.ToString("HH:mm:ss");

            // 只有在完全校准后才触发遮罩渲染
            if (CalibrationData.State == CalibrationState.FullyCalibrated)
            {
                var overlayState = _valueMapper.Map(value);
                DisplayValueText = value.ToString();
                OnOverlayStateChanged?.Invoke(overlayState);
            }
            else
            {
                DisplayValueText = value.ToString();
                // 校准期间不触发遮罩渲染，确保屏幕不会变灰
                OnLog?.Invoke($"[DIAG-3] 校准未完成，跳过遮罩渲染");
            }
        }
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
