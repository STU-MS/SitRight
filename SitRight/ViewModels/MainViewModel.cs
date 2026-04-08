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
    // 新增：绑定校准变化事件，同步基准角
    OnCalibrationChanged += OnCalibrationDataChanged;
}

// 新增：校准数据变化时，更新 ValueMapper 的基准角
private void OnCalibrationDataChanged(CalibrationData data)
{
    if (data.State == CalibrationState.FullyCalibrated && 
        data.NormalAngle.HasValue && 
        data.SlouchAngle.HasValue)
    {
        // 将校准的角度（float）转为 int 传入 ValueMapper
        _valueMapper.UpdateCalibration(
            (int)Math.Round(data.NormalAngle.Value),
            (int)Math.Round(data.SlouchAngle.Value)
        );
    }
}
//优化串口数据处理的日志
case ProtocolLineType.RuntimeData:
    _stateManager.ReceiveRawValue(value);
    RawValueText = value.ToString();
    LastReceiveTimeText = DateTime.Now.ToString("HH:mm:ss");
    DisplayValueText = value.ToString();

    if (CalibrationData.State == CalibrationState.FullyCalibrated)
    {
        var overlayState = _valueMapper.Map(value);
        // 新增：打印区间判断日志
        OnLog?.Invoke($"[DIAG-3] 角度={value}°，坐正角={CalibrationData.NormalAngle:F2}°，驼背角={CalibrationData.SlouchAngle:F2}°，遮罩透明度={overlayState.MaskOpacity:F3}");
        OnLog?.Invoke($"[DIAG-4] OnOverlayStateChanged 订阅者数: {OnOverlayStateChanged?.GetInvocationList()?.Length ?? 0}");
        OnOverlayStateChanged?.Invoke(overlayState);
    }
    else
    {
        OnLog?.Invoke($"[DIAG-3] 校准未完成，跳过遮罩渲染");
    }
    break;
