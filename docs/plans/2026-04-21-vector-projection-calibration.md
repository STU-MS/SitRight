# 向量投影校准方案 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将单轴角度校准替换为三轴向量投影，实现安装无关的坐姿检测，同时简化 PC 端架构。

**Architecture:** 固件存储校准姿态的完整重力向量（而非单轴角度），运行时通过向量投影计算 0-100 比值。PC 端不再做角度映射和校准持久化，固件 EEPROM 是唯一真相来源。

**Tech Stack:** Arduino C++（固件）、C# / WPF / .NET 8（PC 端）、xUnit + Moq（测试）

**Spec:** `docs/superpowers/specs/2026-04-21-vector-projection-calibration-design.md`

---

## Phase 1: PC 端简化（C#）

### Task 1: 简化 AppConfig

**Files:**
- Modify: `SitRight/Models/AppConfig.cs`
- Modify: `SitRight.Tests/AppConfigTests.cs`

- [ ] **Step 1: 删除 AppConfig 中的校准角度字段**

编辑 `SitRight/Models/AppConfig.cs`，删除三行：

```csharp
// 删除这三行:
public double? CalibratedNormalAngle { get; set; }
public double? CalibratedSlouchAngle { get; set; }
public DateTime? CalibratedAt { get; set; }
```

最终文件：

```csharp
namespace SitRight.Models;

public class AppConfig
{
    public string DefaultComPort { get; set; } = "COM1";
    public int BaudRate { get; set; } = 115200;
    public int TimeoutThresholdMs { get; set; } = 2000;
    public double MaxMaskOpacity { get; set; } = 0.70;
    public int HintStartLevel { get; set; } = 30;
    public int UrgentLevel { get; set; } = 80;
    public int TargetMonitorIndex { get; set; } = 0;
}
```

- [ ] **Step 2: 更新 AppConfigTests**

编辑 `SitRight.Tests/AppConfigTests.cs`，删除校准角度断言：

```csharp
using Xunit;
using SitRight.Models;

namespace SitRight.Models;

public class AppConfigTests
{
    [Fact]
    public void NewInstance_HasRecommendedDefaults()
    {
        var config = new AppConfig();
        Assert.Equal("COM1", config.DefaultComPort);
        Assert.Equal(115200, config.BaudRate);
        Assert.Equal(2000, config.TimeoutThresholdMs);
        Assert.Equal(0.70, config.MaxMaskOpacity);
        Assert.Equal(30, config.HintStartLevel);
        Assert.Equal(80, config.UrgentLevel);
    }
}
```

- [ ] **Step 3: 运行测试确认通过**

Run: `cd SitRight.Tests && dotnet test --filter "FullyQualifiedName~AppConfigTests"`
Expected: PASS

---

### Task 2: 简化 CalibrationData

**Files:**
- Modify: `SitRight/Models/CalibrationData.cs`
- Modify: `SitRight.Tests/CalibrationDataTests.cs`

- [ ] **Step 1: 重写 CalibrationData**

删除 `NormalAngle`、`SlouchAngle`、`LastCalibrated` 属性，简化 `ApplyAck` 不再解析 ANGLE 字段：

```csharp
using SitRight.Services;

namespace SitRight.Models;

public enum CalibrationState
{
    NotCalibrated,
    NormalSet,
    FullyCalibrated,
    Error
}

public class CalibrationData
{
    public CalibrationState State { get; set; } = CalibrationState.NotCalibrated;
    public string? LastError { get; set; }

    public void ApplyAck(CalibrationAckData ack)
    {
        LastError = null;

        switch (ack.Command)
        {
            case "SET_NORMAL":
                State = CalibrationState.NormalSet;
                break;
            case "SET_SLOUCH":
                State = CalibrationState.FullyCalibrated;
                break;
            case "RESET":
                State = CalibrationState.NotCalibrated;
                break;
        }
    }

    public void ApplyError(CalibrationErrData err)
    {
        State = CalibrationState.Error;
        LastError = err.ErrorCode;
    }

    public void Reset()
    {
        State = CalibrationState.NotCalibrated;
        LastError = null;
    }
}
```

- [ ] **Step 2: 重写 CalibrationDataTests**

删除所有涉及角度和 LastCalibrated 的测试：

```csharp
using SitRight.Services;
using SitRight.Models;

namespace SitRight.Tests;

public class CalibrationDataTests
{
    private readonly CalibrationData _data = new();

    [Fact]
    public void Initial_State_IsNotCalibrated()
    {
        Assert.Equal(CalibrationState.NotCalibrated, _data.State);
        Assert.Null(_data.LastError);
    }

    [Fact]
    public void ApplyAck_SetNormal_StateIsNormalSet()
    {
        var ack = new CalibrationAckData("SET_NORMAL", new Dictionary<string, string>());
        _data.ApplyAck(ack);
        Assert.Equal(CalibrationState.NormalSet, _data.State);
    }

    [Fact]
    public void ApplyAck_SetSlouch_StateIsFullyCalibrated()
    {
        var ack = new CalibrationAckData("SET_SLOUCH", new Dictionary<string, string>());
        _data.ApplyAck(ack);
        Assert.Equal(CalibrationState.FullyCalibrated, _data.State);
    }

    [Fact]
    public void ApplyAck_SetNormalThenSlouch_StateIsFullyCalibrated()
    {
        _data.ApplyAck(new CalibrationAckData("SET_NORMAL", new Dictionary<string, string>()));
        _data.ApplyAck(new CalibrationAckData("SET_SLOUCH", new Dictionary<string, string>()));
        Assert.Equal(CalibrationState.FullyCalibrated, _data.State);
    }

    [Fact]
    public void ApplyAck_Reset_ReturnsToNotCalibrated()
    {
        _data.ApplyAck(new CalibrationAckData("SET_NORMAL", new Dictionary<string, string>()));
        _data.ApplyAck(new CalibrationAckData("RESET", new Dictionary<string, string>()));
        Assert.Equal(CalibrationState.NotCalibrated, _data.State);
    }

    [Fact]
    public void ApplyAck_ReNormal_ReturnsToNormalSet()
    {
        _data.ApplyAck(new CalibrationAckData("SET_NORMAL", new Dictionary<string, string>()));
        _data.ApplyAck(new CalibrationAckData("SET_SLOUCH", new Dictionary<string, string>()));
        _data.ApplyAck(new CalibrationAckData("SET_NORMAL", new Dictionary<string, string>()));
        Assert.Equal(CalibrationState.NormalSet, _data.State);
    }

    [Fact]
    public void ApplyError_SetsErrorState()
    {
        _data.ApplyError(new CalibrationErrData("BUSY"));
        Assert.Equal(CalibrationState.Error, _data.State);
        Assert.Equal("BUSY", _data.LastError);
    }

    [Fact]
    public void Reset_ReturnsToNotCalibrated()
    {
        _data.ApplyAck(new CalibrationAckData("SET_NORMAL", new Dictionary<string, string>()));
        _data.Reset();
        Assert.Equal(CalibrationState.NotCalibrated, _data.State);
        Assert.Null(_data.LastError);
    }

    [Fact]
    public void ApplyAck_ClearsPreviousError()
    {
        _data.ApplyError(new CalibrationErrData("BUSY"));
        _data.ApplyAck(new CalibrationAckData("SET_NORMAL", new Dictionary<string, string>()));
        Assert.Null(_data.LastError);
    }
}
```

- [ ] **Step 3: 运行测试确认通过**

Run: `cd SitRight.Tests && dotnet test --filter "FullyQualifiedName~CalibrationDataTests"`
Expected: PASS

---

### Task 3: 简化 ValueMapper

**Files:**
- Modify: `SitRight/Services/ValueMapper.cs`
- Modify: `SitRight.Tests/ValueMapperTests.cs`

- [ ] **Step 1: 重写 ValueMapper**

删除 `_normalAngle`、`_slouchAngle`、`SetCalibration()`，`Map` 变为直接透传：

```csharp
using SitRight.Models;

namespace SitRight.Services;

public class ValueMapper
{
    private readonly int _hintStartLevel;
    private readonly int _urgentLevel;

    public ValueMapper(int hintStartLevel = 30, int urgentLevel = 80)
    {
        _hintStartLevel = hintStartLevel;
        _urgentLevel = urgentLevel;
    }

    public OverlayState Map(int level)
    {
        return OverlayState.FromDisplayLevel(level, _hintStartLevel, _urgentLevel);
    }
}
```

- [ ] **Step 2: 重写 ValueMapperTests**

删除 `SetCalibration` 调用，所有测试直接用 0-100 值：

```csharp
using Xunit;
using SitRight.Models;
using SitRight.Services;

namespace SitRight.Tests;

public class ValueMapperTests
{
    private readonly ValueMapper _mapper = new(hintStartLevel: 30, urgentLevel: 80);

    [Fact]
    public void Map_LevelZero_ReturnsMinimalMask()
    {
        var state = _mapper.Map(0);
        Assert.True(state.MaskOpacity < 0.1);
    }

    [Fact]
    public void Map_Level100_ReturnsMaxMask()
    {
        var state = _mapper.Map(100);
        Assert.True(state.MaskOpacity > 0.6);
    }

    [Fact]
    public void Map_Level50_ReturnsModerateMask()
    {
        var state = _mapper.Map(50);
        Assert.True(state.MaskOpacity > 0.1);
        Assert.True(state.MaskOpacity < 0.3);
    }

    [Fact]
    public void Map_LevelBelowHintStart_NoBlock()
    {
        var state = _mapper.Map(20);
        Assert.False(state.BlockInput);
    }

    [Fact]
    public void Map_LevelAboveUrgent_NeverBlocksInput()
    {
        var state = _mapper.Map(90);
        Assert.False(state.BlockInput);
        Assert.Equal(3, state.SeverityLevel);
    }

    [Theory]
    [InlineData(0, "#FFFFFF")]
    [InlineData(50, "#E0E0E0")]
    [InlineData(70, "#BDBDBD")]
    [InlineData(100, "#9E9E9E")]
    public void Map_Level_ReturnsCorrectColor(int level, string expectedColor)
    {
        var state = _mapper.Map(level);
        Assert.Equal(expectedColor, state.MaskColor);
    }

    [Fact]
    public void Map_EdgeOpacity_IsNonLinear()
    {
        var state1 = _mapper.Map(50);
        var state2 = _mapper.Map(100);

        Assert.True(state2.EdgeOpacity > state1.EdgeOpacity);
        Assert.True(state2.EdgeOpacity / state1.EdgeOpacity > 2);
    }

    [Fact]
    public void NewMapper_HasDefaultLevels()
    {
        var mapper = new ValueMapper();
        var state = mapper.Map(0);
        Assert.NotNull(state);
    }
}
```

- [ ] **Step 3: 运行测试确认通过**

Run: `cd SitRight.Tests && dotnet test --filter "FullyQualifiedName~ValueMapperTests"`
Expected: PASS

---

### Task 4: 删除 CalibrationService

**Files:**
- Delete: `SitRight/Services/CalibrationService.cs`
- Delete: `SitRight.Tests/CalibrationServiceTests.cs`

- [ ] **Step 1: 删除 CalibrationService.cs**

```bash
rm SitRight/Services/CalibrationService.cs
```

- [ ] **Step 2: 删除 CalibrationServiceTests.cs**

```bash
rm SitRight.Tests/CalibrationServiceTests.cs
```

- [ ] **Step 3: 暂不运行测试**（MainViewModel 还引用 CalibrationService，下一 task 修复）

---

### Task 5: 简化 MainViewModel

**Files:**
- Modify: `SitRight/ViewModels/MainViewModel.cs`
- Modify: `SitRight.Tests/MainViewModelTests.cs`

- [ ] **Step 1: 重写 MainViewModel**

主要变更：
- 删除 `_calibrationService` 字段及相关方法
- 删除 `NormalAngleText` / `SlouchAngleText` 属性
- 删除 `SyncCalibrationToValueMapper`、`SubscribeCalibrationChanges`、`RestoreCalibrationFromConfig`、`PersistCalibration`
- `HandleInputValue`：收到 RuntimeData 时自动推断校准状态为 FullyCalibrated
- `HandleCalibrationAck`：只更新状态和 UI
- `ClearOverlayState`：简化

```csharp
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
```

- [ ] **Step 2: 重写 MainViewModelTests**

删除所有涉及角度持久化、CalibrationService、NormalAngle/SlouchAngle 的测试：

```csharp
using Xunit;
using Moq;
using SitRight.Services;
using SitRight.Models;
using SitRight.ViewModels;

namespace SitRight.Tests;

public class MainViewModelTests
{
    private readonly Mock<ISerialService> _mockSerial;
    private readonly DeviceProtocol _protocol;
    private readonly DeviceStateManager _stateManager;
    private readonly ValueMapper _valueMapper;
    private readonly string _testPath;
    private readonly ConfigService _configService;
    private MainViewModel _viewModel;

    public MainViewModelTests()
    {
        _mockSerial = new Mock<ISerialService>();
        _mockSerial.Setup(s => s.GetAvailablePorts()).Returns(new[] { "COM1", "COM2" });

        _protocol = new DeviceProtocol();
        _stateManager = new DeviceStateManager();
        _valueMapper = new ValueMapper();

        _testPath = Path.Combine(Path.GetTempPath(), $"test_vm_{Guid.NewGuid()}.json");
        _configService = new ConfigService(_testPath);

        _viewModel = CreateViewModel();
    }

    [Fact]
    public void InitialStatus_IsDisconnected()
    {
        Assert.Equal("Disconnected", _viewModel.StatusText);
    }

    [Fact]
    public void AvailablePorts_ReturnsFromSerialService()
    {
        var ports = _viewModel.AvailablePorts;
        Assert.Contains("COM1", ports);
        Assert.Contains("COM2", ports);
    }

    [Fact]
    public void InitialIsConnected_IsFalse()
    {
        Assert.False(_viewModel.IsConnected);
    }

    [Fact]
    public void InitialRawValue_IsDash()
    {
        Assert.Equal("--", _viewModel.RawValueText);
    }

    [Fact]
    public void InitialDisplayValue_IsDash()
    {
        Assert.Equal("--", _viewModel.DisplayValueText);
    }

    [Fact]
    public void SetSimulationMode_RaisesEvent()
    {
        var eventRaised = false;
        _viewModel.OnSimulationModeChanged += _ => eventRaised = true;

        _viewModel.IsSimulationMode = true;

        Assert.True(eventRaised);
    }

    [Fact]
    public void SimulateValue_WhenSimulationMode_MapsDirectly()
    {
        _viewModel.IsSimulationMode = true;
        _viewModel.SimulateValue(50);

        Assert.Equal("50", _viewModel.RawValueText);
    }

    [Fact]
    public void SimulateValue_WhenNotSimulationMode_DoesNothing()
    {
        _viewModel.IsSimulationMode = false;
        _viewModel.SimulateValue(50);

        Assert.Equal("--", _viewModel.RawValueText);
    }

    [Fact]
    public void Disconnect_CallsSerialDisconnect()
    {
        _viewModel.Disconnect();
        _mockSerial.Verify(s => s.Disconnect(), Times.Once);
    }

    [Fact]
    public void OnOverlayStateChanged_FiredOnSimulate()
    {
        OverlayState? receivedState = null;
        _viewModel.OnOverlayStateChanged += state => receivedState = state;

        _viewModel.IsSimulationMode = true;
        _viewModel.SimulateValue(50);

        Assert.NotNull(receivedState);
    }

    [Fact]
    public void SimulateValue_AutoSetsFullyCalibrated()
    {
        _viewModel.IsSimulationMode = true;
        _viewModel.SimulateValue(50);

        Assert.Equal(CalibrationState.FullyCalibrated, _viewModel.CalibrationData.State);
        Assert.Equal("完全校准", _viewModel.CalibrationStatusText);
    }

    [Fact]
    public void RuntimeData_AutoSetsFullyCalibrated()
    {
        _mockSerial.Raise(s => s.OnLineReceived += null, "42");

        Assert.Equal(CalibrationState.FullyCalibrated, _viewModel.CalibrationData.State);
    }

    [Fact]
    public void CalibrationAck_UpdatesCalibrationState()
    {
        _mockSerial.Raise(s => s.OnLineReceived += null, "ACK:SET_NORMAL");

        Assert.Equal(CalibrationState.NormalSet, _viewModel.CalibrationData.State);
        Assert.Equal("已校准坐正", _viewModel.CalibrationStatusText);

        _mockSerial.Raise(s => s.OnLineReceived += null, "ACK:SET_SLOUCH");

        Assert.Equal(CalibrationState.FullyCalibrated, _viewModel.CalibrationData.State);
        Assert.Equal("完全校准", _viewModel.CalibrationStatusText);
    }

    [Fact]
    public void CalibrationAck_ClearsOverlayState()
    {
        OverlayState? receivedState = null;
        _viewModel.OnOverlayStateChanged += state => receivedState = state;

        _viewModel.IsSimulationMode = true;
        _viewModel.SimulateValue(50);
        Assert.NotNull(receivedState);

        _mockSerial.Raise(s => s.OnLineReceived += null, "ACK:SET_NORMAL");

        Assert.Equal(0, receivedState!.MaskOpacity);
        Assert.Equal(string.Empty, receivedState.MessageText);
    }

    [Fact]
    public void CalibrationErr_SetsErrorState()
    {
        _mockSerial.Raise(s => s.OnLineReceived += null, "ERR:BUSY");

        Assert.Equal(CalibrationState.Error, _viewModel.CalibrationData.State);
        Assert.Contains("BUSY", _viewModel.CalibrationStatusText);
    }

    private MainViewModel CreateViewModel()
    {
        return new MainViewModel(
            _mockSerial.Object,
            _protocol,
            _stateManager,
            _valueMapper,
            _configService);
    }
}
```

- [ ] **Step 3: 运行测试确认通过**

Run: `cd SitRight.Tests && dotnet test --filter "FullyQualifiedName~MainViewModelTests"`
Expected: PASS

---

### Task 6: 更新 MainWindow UI

**Files:**
- Modify: `SitRight/MainWindow.xaml`
- Modify: `SitRight/MainWindow.xaml.cs`

- [ ] **Step 1: 更新 MainWindow.xaml 校准区域**

删除校准区域的第二行（坐正角度/驼背角度）。将校准控制区替换为：

```xml
        <!-- 校准控制区 -->
        <GroupBox Grid.Row="3" Header="校准控制" Margin="0,0,0,12">
            <StackPanel Orientation="Horizontal" Margin="8">
                <Button x:Name="CalibrateNormalButton" Content="校准坐正" Width="80" Margin="0,0,12,0"/>
                <Button x:Name="CalibrateSlouchButton" Content="校准驼背" Width="80" Margin="0,0,12,0"/>
                <TextBlock Text="状态:" FontWeight="Bold" VerticalAlignment="Center" Margin="0,0,8,0"/>
                <TextBlock x:Name="CalibrationStatusText" Text="未校准" VerticalAlignment="Center"/>
            </StackPanel>
        </GroupBox>
```

- [ ] **Step 2: 更新 MainWindow.xaml.cs 的 OnCalibrationChanged 处理**

替换 `BindUIEvents` 中 `_viewModel.OnCalibrationChanged` 的处理 lambda（约第 130-141 行）：

```csharp
        _viewModel.OnCalibrationChanged += _ => Dispatcher.Invoke(() =>
        {
            CalibrationStatusText.Text = _viewModel.CalibrationStatusText;

            var data = _viewModel.CalibrationData;
            if (data.State == CalibrationState.Error)
                Log($"校准错误: {data.LastError}");
            else
                Log($"校准状态: {_viewModel.CalibrationStatusText}");
        });
```

- [ ] **Step 3: 运行全部 PC 端测试确认通过**

Run: `cd SitRight.Tests && dotnet test`
Expected: ALL PASS

---

## Phase 2: 固件重写（Arduino C++）

### Task 7: 更新 EEPROM 结构和校准数据类型

**Files:**
- Modify: `hardware/sitRight_firmware.ino`

- [ ] **Step 1: 替换 EEPROM 结构和全局变量**

将 EEPROM 结构部分（约第 46-71 行）替换为：

```cpp
// ==================== EEPROM 存储结构 ====================
struct CalibData {
  uint16_t magic;       // 魔数 0xA5A5
  uint8_t  version;     // 版本号 0x02
  float    normalX, normalY, normalZ;   // 坐正姿态重力向量
  float    slouchX, slouchY, slouchZ;   // 驼背姿态重力向量
  uint8_t  checksum;   // 简单校验和
};

const uint16_t EEPROM_MAGIC = 0xA5A5;
const uint8_t EEPROM_VERSION = 0x02;

// ==================== 全局变量 ====================
// MPU6050 原始数据
int16_t AcX, AcY, AcZ, Tmp, GyX, GyY, GyZ;
// 加速度
float ax, ay, az;

// 校准参数: 重力方向单位向量
float calibNormalX = 0, calibNormalY = 0, calibNormalZ = 0;
float calibSlouchX = 0, calibSlouchY = 0, calibSlouchZ = 0;
bool hasNormal = false;
bool hasSlouch = false;

// blur 输出状态
float blurSmoothed = 0.0;
bool blurSmootherInitialized = false;

// 系统状态
volatile bool isCalibrating = false;

// 串口接收缓冲区
const int MAX_CMD_LEN = 32;
char cmdBuffer[MAX_CMD_LEN];
int cmdIndex = 0;
```

- [ ] **Step 2: 删除不再需要的全局变量和常量**

删除：
- `float gx, gy, gz;`
- `float angleX, angleY;`
- `const float DEFAULT_NORMAL = 0.0;`
- `const float DEFAULT_SLOUCH = 15.0;`
- `#define OUTPUT_MODE_BLUR` 和 `#define OUTPUT_MODE_ANGLE` 编译开关及相关条件编译

---

### Task 8: 重写 EEPROM 读写和校准流程

**Files:**
- Modify: `hardware/sitRight_firmware.ino`

- [ ] **Step 1: 重写 eepromReadCalib**

```cpp
void eepromReadCalib() {
  CalibData data;
  EEPROM.get(0, data);

  // 校验魔数和版本
  if (data.magic != EEPROM_MAGIC || data.version != EEPROM_VERSION) {
    hasNormal = false;
    hasSlouch = false;
    return;
  }

  // 校验校验和
  uint8_t storedChecksum = data.checksum;
  data.checksum = 0;
  uint8_t calcChecksum = calculateChecksum(data);

  if (storedChecksum != calcChecksum) {
    hasNormal = false;
    hasSlouch = false;
    return;
  }

  // 加载校准向量
  calibNormalX = data.normalX;
  calibNormalY = data.normalY;
  calibNormalZ = data.normalZ;
  hasNormal = true;

  calibSlouchX = data.slouchX;
  calibSlouchY = data.slouchY;
  calibSlouchZ = data.slouchZ;
  hasSlouch = true;
}
```

- [ ] **Step 2: 重写 eepromWriteCalib**

```cpp
void eepromWriteCalib() {
  CalibData data;
  data.magic = EEPROM_MAGIC;
  data.version = EEPROM_VERSION;
  data.normalX = calibNormalX;
  data.normalY = calibNormalY;
  data.normalZ = calibNormalZ;
  data.slouchX = calibSlouchX;
  data.slouchY = calibSlouchY;
  data.slouchZ = calibSlouchZ;
  data.checksum = 0;
  data.checksum = calculateChecksum(data);

  EEPROM.put(0, data);
}
```

- [ ] **Step 3: 重写 performCalibration — 返回归一化向量**

```cpp
// 执行校准采样：500ms内采样10次，返回归一化重力向量
// 成功返回true，结果写入 outX/outY/outZ
bool performCalibration(float &outX, float &outY, float &outZ) {
  float sumX = 0, sumY = 0, sumZ = 0;
  int validSamples = 0;

  for (int i = 0; i < CALIBRATE_SAMPLES; i++) {
    Wire.beginTransmission(MPU_addr);
    Wire.write(0x3B);
    Wire.endTransmission(false);
    Wire.requestFrom(MPU_addr, 14, true);

    AcX = Wire.read() << 8 | Wire.read();
    AcY = Wire.read() << 8 | Wire.read();
    AcZ = Wire.read() << 8 | Wire.read();

    float rawX = AcX / 16384.0;
    float rawY = AcY / 16384.0;
    float rawZ = AcZ / 16384.0;

    // 归一化
    float mag = sqrt(rawX * rawX + rawY * rawY + rawZ * rawZ);
    if (mag < 0.1) continue;  // 无效读数

    sumX += rawX / mag;
    sumY += rawY / mag;
    sumZ += rawZ / mag;
    validSamples++;

    delay(SAMPLE_INTERVAL_MS);
  }

  if (validSamples < CALIBRATE_SAMPLES / 2)
    return false;

  // 平均后再次归一化
  outX = sumX / validSamples;
  outY = sumY / validSamples;
  outZ = sumZ / validSamples;
  float mag = sqrt(outX * outX + outY * outY + outZ * outZ);
  outX /= mag;
  outY /= mag;
  outZ /= mag;

  return true;
}
```

- [ ] **Step 4: 重写 processCommand 中的校准命令处理**

```cpp
void processCommand(const char* cmd) {
  if (isCalibrating) {
    sendErr("BUSY");
    return;
  }

  if (strcmp(cmd, "CMD:SET_NORMAL") == 0) {
    isCalibrating = true;
    float nx, ny, nz;
    if (!performCalibration(nx, ny, nz)) {
      sendErr("CALIBRATE_TIMEOUT");
      isCalibrating = false;
      return;
    }

    calibNormalX = nx;
    calibNormalY = ny;
    calibNormalZ = nz;
    hasNormal = true;

    eepromWriteCalib();

    // 验证写入
    CalibData verify;
    EEPROM.get(0, verify);
    if (verify.magic != EEPROM_MAGIC) {
      hasNormal = false;
      sendErr("EEPROM_WRITE_FAIL");
      isCalibrating = false;
      return;
    }

    sendAck("SET_NORMAL");
    isCalibrating = false;

  } else if (strcmp(cmd, "CMD:SET_SLOUCH") == 0) {
    isCalibrating = true;
    float sx, sy, sz;
    if (!performCalibration(sx, sy, sz)) {
      sendErr("CALIBRATE_TIMEOUT");
      isCalibrating = false;
      return;
    }

    calibSlouchX = sx;
    calibSlouchY = sy;
    calibSlouchZ = sz;
    hasSlouch = true;

    eepromWriteCalib();

    CalibData verify;
    EEPROM.get(0, verify);
    if (verify.magic != EEPROM_MAGIC) {
      hasSlouch = false;
      sendErr("EEPROM_WRITE_FAIL");
      isCalibrating = false;
      return;
    }

    sendAck("SET_SLOUCH");
    isCalibrating = false;

  } else {
    sendErr("UNKNOWN_CMD");
  }
}
```

- [ ] **Step 5: 简化 sendAck**

```cpp
void sendAck(const char* cmdType) {
  Serial.print("ACK:");
  Serial.println(cmdType);
}
```

---

### Task 9: 重写运行时算法

**Files:**
- Modify: `hardware/sitRight_firmware.ino`

- [ ] **Step 1: 重写 computeBlurLevel 为向量投影**

```cpp
// 使用向量投影计算 blurLevel
int computeBlurLevel(float curX, float curY, float curZ) {
  // deviation = current - normal
  float devX = curX - calibNormalX;
  float devY = curY - calibNormalY;
  float devZ = curZ - calibNormalZ;

  // axis = slouch - normal
  float axisX = calibSlouchX - calibNormalX;
  float axisY = calibSlouchY - calibNormalY;
  float axisZ = calibSlouchZ - calibNormalZ;

  // projection = dot(deviation, axis) / dot(axis, axis)
  float dotDevAxis = devX * axisX + devY * axisY + devZ * axisZ;
  float dotAxisAxis = axisX * axisX + axisY * axisY + axisZ * axisZ;

  // 防止除零（校准两点过于接近时）
  if (dotAxisAxis < 0.001) return 0;

  float projection = dotDevAxis / dotAxisAxis;

  // 截断负值（后仰不触发）
  float x = clampFloat(projection, 0.0, 1.2);

  // 三段非线性映射（保持现有曲线参数）
  float b = 0.0;
  if (x <= 0.3) {
    b = 30.0 * pow(x / 0.3, 1.6);
  } else if (x <= 0.7) {
    b = 30.0 + 40.0 * pow((x - 0.3) / 0.4, 1.2);
  } else {
    b = 70.0 + 30.0 * pow((x - 0.7) / 0.5, 0.8);
  }

  b = clampFloat(b, 0.0, 100.0);
  return (int)round(b);
}
```

- [ ] **Step 2: 重写 loop 主循环**

```cpp
void loop() {
  // ========== 串口命令处理 ==========
  while (Serial.available() > 0) {
    char c = Serial.read();

    if (c == '\n') {
      cmdBuffer[cmdIndex] = '\0';
      if (cmdIndex > 0) {
        trimWhitespace(cmdBuffer);

        if (strlen(cmdBuffer) > 0) {
          processCommand(cmdBuffer);
        }
      }

      cmdIndex = 0;
    } else if (cmdIndex < MAX_CMD_LEN - 1) {
      cmdBuffer[cmdIndex++] = c;
    }
  }

  // ========== 姿态采集与上报 ==========
  // 只有完全校准后才开始采集
  if (hasNormal && hasSlouch && !isCalibrating) {
    Wire.beginTransmission(MPU_addr);
    Wire.write(0x3B);
    Wire.endTransmission(false);
    Wire.requestFrom(MPU_addr, 14, true);

    AcX = Wire.read() << 8 | Wire.read();
    AcY = Wire.read() << 8 | Wire.read();
    AcZ = Wire.read() << 8 | Wire.read();

    // 归一化当前重力向量
    ax = AcX / 16384.0;
    ay = AcY / 16384.0;
    az = AcZ / 16384.0;
    float mag = sqrt(ax * ax + ay * ay + az * az);

    if (mag > 0.1) {
      ax /= mag;
      ay /= mag;
      az /= mag;

      int blurRaw = computeBlurLevel(ax, ay, az);
      int blurStable = smoothBlurLevel(blurRaw);
      Serial.println(blurStable);
    }

    delay(DELAY_MS);
  }
}
```

- [ ] **Step 3: 更新 setup 中的调试输出**

```cpp
void setup() {
  Wire.begin();
  Serial.begin(BAUD_RATE);

  // 初始化MPU6050
  Wire.beginTransmission(MPU_addr);
  Wire.write(0x6B);
  Wire.write(0);
  Wire.endTransmission(true);

  // 从EEPROM加载校准参数
  eepromReadCalib();

  DBG_PRINTLN("SitRight Firmware v2.0");
  DBG_PRINT("Calibration: ");
  DBG_PRINT(hasNormal ? "normal OK" : "normal MISSING");
  DBG_PRINT(", ");
  DBG_PRINTLN(hasSlouch ? "slouch OK" : "slouch MISSING");
  DBG_PRINTLN("Ready. Commands: CMD:SET_NORMAL, CMD:SET_SLOUCH");
}
```

---

### Task 10: 固件集成测试

**Files:**
- Modify: `hardware/sitRight_firmware.ino`（临时开启 DEBUG_SERIAL）

- [ ] **Step 1: 编译并上传固件到 Arduino**

使用 Arduino IDE 或 platformio 编译上传。

- [ ] **Step 2: 串口监视器验证启动信息**

预期看到（DEBUG_SERIAL=1 时）：
```
SitRight Firmware v2.0
Calibration: normal MISSING, slouch MISSING
Ready. Commands: CMD:SET_NORMAL, CMD:SET_SLOUCH
```

固件此时不发运行数据。

- [ ] **Step 3: 发送校准命令验证**

发送 `CMD:SET_NORMAL`，预期回复：`ACK:SET_NORMAL`

发送 `CMD:SET_SLOUCH`，预期回复：`ACK:SET_SLOUCH`

校准完成后，预期开始看到每 200ms 输出 0-100 整数。

- [ ] **Step 4: 验证姿态响应**

- 坐正 → 输出接近 0
- 逐渐驼背 → 输出逐渐增大
- 后仰 → 输出保持 0
- 超过驼背校准位置 → 输出接近 100

- [ ] **Step 5: 验证 EEPROM 持久化**

断电重启后，预期看到：
```
Calibration: normal OK, slouch OK
```
并立即开始发送数据，无需重新校准。

- [ ] **Step 6: PC 端联调**

启动 PC 应用，连接串口，验证：
- 收到数据后 UI 自动显示"完全校准"
- 校准按钮正常工作
- 遮罩响应正确
- 角度文本不再显示（已删除）

- [ ] **Step 7: 测试完毕后关闭 DEBUG_SERIAL**

将 `#define DEBUG_SERIAL 1` 改回 `#define DEBUG_SERIAL 0`。

---

## 自检

**Spec 覆盖：**
- EEPROM version 0x02（6 个 float）→ Task 7
- 校准记录归一化向量 → Task 8
- 未校准不发数据 → Task 9 (loop 中 `hasNormal && hasSlouch` 检查)
- 向量投影算法 → Task 9
- 后仰不触发（投影截断负值）→ Task 9
- ACK 不带 ANGLE → Task 8
- ValueMapper 简化 → Task 3
- CalibrationData 简化 → Task 2
- CalibrationService 删除 → Task 4
- AppConfig 简化 → Task 1
- MainViewModel 简化 → Task 5
- MainWindow UI 简化 → Task 6
- 校准状态自动恢复（收到数据即推断校准）→ Task 5

**Placeholder 扫描：** 无 TBD/TODO。

**类型一致性：** 所有方法签名和属性名在 Task 间保持一致。
