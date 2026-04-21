# 阶段 1-2 实施计划：业务逻辑 + MVVM 重构

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.
>
> **TDD Required:** 所有实现必须遵循 Red-Green-Refactor 循环：
> 1. 先写测试，运行验证 FAIL
> 2. 再写实现，运行验证 PASS
> 3. 重构代码

**Goal:** 完成业务逻辑层（ValueMapper、ConfigService）和 MVVM 重构（OverlayViewModel、MainViewModel），使应用具备完整的核心数据流。

**Architecture:** MCU 发送平滑后的 blurLevel → SerialService → DeviceProtocol → ValueMapper → OverlayViewModel → OverlayWindow。MainViewModel 编排所有服务，MainWindow 通过 MVVM 绑定驱动 UI。ConfigService 管理 JSON 配置持久化。

**Tech Stack:** .NET 8.0 / WPF / xUnit / Moq 4.20.70 / System.Text.Json

---

## Task 1: 清理 AppConfig 废弃字段

**Files:**
- Modify: `SitRight/Models/AppConfig.cs:8-9`
- Modify: `SitRight.Tests/AppConfigTests.cs:15-16`

**Step 1: 更新 AppConfigTests（RED）**

编辑 `SitRight.Tests/AppConfigTests.cs`，移除对 `DisplayRefreshIntervalMs` 和 `SmoothingAlpha` 的断言：

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

**Step 2: 运行测试验证 FAIL**

Run: `dotnet test SitRight.Tests --filter "FullyQualifiedName~AppConfigTests"`
Expected: FAIL — `AppConfig` does not contain a definition for `DisplayRefreshIntervalMs` (因为还没删，测试实际上此时还是 PASS 的。直接到 Step 3 改源码)

**Step 3: 更新 AppConfig（GREEN）**

编辑 `SitRight/Models/AppConfig.cs`，移除第 8-9 行（`DisplayRefreshIntervalMs` 和 `SmoothingAlpha`）：

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
}
```

**Step 4: 运行测试验证 PASS**

Run: `dotnet test SitRight.Tests --filter "FullyQualifiedName~AppConfigTests"`
Expected: PASS

**Step 5: 运行全量测试确认无回归**

Run: `dotnet test SitRight.Tests`
Expected: ALL PASS

**Step 6: 提交**

```bash
git add SitRight/Models/AppConfig.cs SitRight.Tests/AppConfigTests.cs
git commit -m "chore: 移除 AppConfig 废弃字段 SmoothingAlpha 和 DisplayRefreshIntervalMs"
```

---

## Task 2: 安装 Moq 测试框架

**Files:**
- Modify: `SitRight.Tests/SitRight.Tests.csproj`

**Step 1: 添加 Moq 包**

Run: `dotnet add SitRight.Tests/SitRight.Tests.csproj package Moq --version 4.20.70`

**Step 2: 验证还原成功**

Run: `dotnet restore SitRight.Tests/SitRight.Tests.csproj`
Expected: 成功

**Step 3: 运行全量测试确认无回归**

Run: `dotnet test SitRight.Tests`
Expected: ALL PASS

**Step 4: 提交**

```bash
git add SitRight.Tests/SitRight.Tests.csproj
git commit -m "chore: 添加 Moq 4.20.70 测试框架"
```

---

## Task 3: ValueMapper 数值映射器

**Files:**
- Create: `SitRight/Services/ValueMapper.cs`
- Create: `SitRight.Tests/ValueMapperTests.cs`

**Step 1: 编写测试（RED）**

创建 `SitRight.Tests/ValueMapperTests.cs`：

```csharp
using Xunit;
using SitRight.Models;
using SitRight.Services;

namespace SitRight.Tests;

public class ValueMapperTests
{
    private readonly ValueMapper _mapper;

    public ValueMapperTests()
    {
        _mapper = new ValueMapper(hintStartLevel: 30, urgentLevel: 80);
    }

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
        Assert.True(state.MaskOpacity > 0.2);
        Assert.True(state.MaskOpacity < 0.5);
    }

    [Fact]
    public void Map_LevelBelowHintStart_NoBlock()
    {
        var state = _mapper.Map(20);
        Assert.False(state.BlockInput);
    }

    [Fact]
    public void Map_LevelAboveUrgent_BlocksInput()
    {
        var state = _mapper.Map(90);
        Assert.True(state.BlockInput);
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

**Step 2: 运行测试验证 FAIL**

Run: `dotnet test SitRight.Tests --filter "FullyQualifiedName~ValueMapperTests"`
Expected: FAIL — `ValueMapper` not found

**Step 3: 实现 ValueMapper（GREEN）**

创建 `SitRight/Services/ValueMapper.cs`：

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

    public OverlayState Map(int blurLevel)
    {
        return OverlayState.FromDisplayLevel(blurLevel, _hintStartLevel, _urgentLevel);
    }
}
```

**Step 4: 运行测试验证 PASS**

Run: `dotnet test SitRight.Tests --filter "FullyQualifiedName~ValueMapperTests"`
Expected: PASS

**Step 5: 运行全量测试确认无回归**

Run: `dotnet test SitRight.Tests`
Expected: ALL PASS

**Step 6: 提交**

```bash
git add SitRight/Services/ValueMapper.cs SitRight.Tests/ValueMapperTests.cs
git commit -m "feat: 实现 ValueMapper 数值映射器 (TDD)"
```

---

## Task 4: ConfigService 配置服务

**Files:**
- Create: `SitRight/Services/ConfigService.cs`
- Create: `SitRight.Tests/ConfigServiceTests.cs`

**Step 1: 编写测试（RED）**

创建 `SitRight.Tests/ConfigServiceTests.cs`：

```csharp
using Xunit;
using SitRight.Services;
using SitRight.Models;

namespace SitRight.Tests;

public class ConfigServiceTests : IDisposable
{
    private readonly string _testPath;
    private readonly ConfigService _service;

    public ConfigServiceTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"test_config_{Guid.NewGuid()}.json");
        _service = new ConfigService(_testPath);
    }

    [Fact]
    public void Load_WhenNoConfig_CreatesDefault()
    {
        var config = _service.Load();
        Assert.NotNull(config);
        Assert.Equal(115200, config.BaudRate);
        Assert.Equal(2000, config.TimeoutThresholdMs);
    }

    [Fact]
    public void Load_WhenConfigExists_LoadsValues()
    {
        var initialConfig = new AppConfig
        {
            DefaultComPort = "COM5",
            BaudRate = 9600
        };
        _service.Save(initialConfig);

        var newService = new ConfigService(_testPath);
        var loaded = newService.Load();

        Assert.Equal("COM5", loaded.DefaultComPort);
        Assert.Equal(9600, loaded.BaudRate);
    }

    [Fact]
    public void Save_WritesJsonFile()
    {
        var config = new AppConfig { DefaultComPort = "COM3" };
        _service.Save(config);

        Assert.True(File.Exists(_testPath));
    }

    [Fact]
    public void Update_ModifiesAndSaves()
    {
        _service.Load();
        _service.Update(c => c.DefaultComPort = "COM7");

        var reloaded = _service.Load();
        Assert.Equal("COM7", reloaded.DefaultComPort);
    }

    [Fact]
    public void Load_CachesConfig()
    {
        var config1 = _service.Load();
        var config2 = _service.Load();
        Assert.Same(config1, config2);
    }

    [Fact]
    public void Save_InvalidatesCache()
    {
        var config1 = _service.Load();
        _service.Save(new AppConfig { DefaultComPort = "COM9" });
        var config2 = _service.Load();

        Assert.NotSame(config1, config2);
        Assert.Equal("COM9", config2.DefaultComPort);
    }

    public void Dispose()
    {
        if (File.Exists(_testPath))
            File.Delete(_testPath);
    }
}
```

**Step 2: 运行测试验证 FAIL**

Run: `dotnet test SitRight.Tests --filter "FullyQualifiedName~ConfigServiceTests"`
Expected: FAIL — `ConfigService` not found

**Step 3: 实现 ConfigService（GREEN）**

创建 `SitRight/Services/ConfigService.cs`：

```csharp
using System.IO;
using System.Text.Json;
using SitRight.Models;

namespace SitRight.Services;

public class ConfigService
{
    private readonly string _configPath;
    private AppConfig? _cachedConfig;

    public ConfigService(string? configPath = null)
    {
        _configPath = configPath ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config.json");
    }

    public AppConfig Load()
    {
        if (_cachedConfig != null)
            return _cachedConfig;

        if (File.Exists(_configPath))
        {
            var json = File.ReadAllText(_configPath);
            _cachedConfig = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        else
        {
            _cachedConfig = new AppConfig();
            Save(_cachedConfig);
        }

        return _cachedConfig;
    }

    public void Save(AppConfig config)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(_configPath, json);
        _cachedConfig = config;
    }

    public void Update(Action<AppConfig> updateAction)
    {
        var config = Load();
        updateAction(config);
        Save(config);
    }
}
```

**Step 4: 运行测试验证 PASS**

Run: `dotnet test SitRight.Tests --filter "FullyQualifiedName~ConfigServiceTests"`
Expected: PASS

**Step 5: 运行全量测试确认无回归**

Run: `dotnet test SitRight.Tests`
Expected: ALL PASS

**Step 6: 提交**

```bash
git add SitRight/Services/ConfigService.cs SitRight.Tests/ConfigServiceTests.cs
git commit -m "feat: 实现 ConfigService 配置服务 (TDD)"
```

---

## Task 5: OverlayViewModel 视图模型

**Files:**
- Create: `SitRight/ViewModels/OverlayViewModel.cs`
- Create: `SitRight.Tests/OverlayViewModelTests.cs`

**Step 1: 编写测试（RED）**

创建 `SitRight.Tests/OverlayViewModelTests.cs`：

```csharp
using Xunit;
using SitRight.Models;
using SitRight.ViewModels;

namespace SitRight.Tests;

public class OverlayViewModelTests
{
    [Fact]
    public void InitialState_IsInvisible()
    {
        var vm = new OverlayViewModel();
        Assert.False(vm.IsVisible);
        Assert.Equal(0, vm.MaskOpacity);
        Assert.Equal(0, vm.EdgeOpacity);
    }

    [Fact]
    public void InitialState_HasDefaultColor()
    {
        var vm = new OverlayViewModel();
        Assert.Equal("#FFFFFF", vm.MaskColor);
    }

    [Fact]
    public void UpdateFrom_SetsAllProperties()
    {
        var vm = new OverlayViewModel();
        var state = new OverlayState
        {
            MaskOpacity = 0.5,
            MaskColor = "#E0E0E0",
            EdgeOpacity = 0.2,
            SeverityLevel = 2
        };

        vm.UpdateFrom(state);

        Assert.Equal(0.5, vm.MaskOpacity);
        Assert.Equal("#E0E0E0", vm.MaskColor);
        Assert.Equal(0.2, vm.EdgeOpacity);
        Assert.Equal(2, vm.SeverityLevel);
    }

    [Fact]
    public void UpdateFrom_ZeroOpacity_SetsInvisible()
    {
        var vm = new OverlayViewModel();
        vm.UpdateFrom(new OverlayState { MaskOpacity = 0 });
        Assert.False(vm.IsVisible);
    }

    [Fact]
    public void UpdateFrom_NonZeroOpacity_SetsVisible()
    {
        var vm = new OverlayViewModel();
        vm.UpdateFrom(new OverlayState { MaskOpacity = 0.3 });
        Assert.True(vm.IsVisible);
    }

    [Fact]
    public void PropertyChanged_IsRaised()
    {
        var vm = new OverlayViewModel();
        var changedProperties = new List<string>();

        vm.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        vm.UpdateFrom(new OverlayState { MaskOpacity = 0.5 });

        Assert.Contains("MaskOpacity", changedProperties);
        Assert.Contains("IsVisible", changedProperties);
    }

    [Theory]
    [InlineData(0, "#FFFFFF")]
    [InlineData(100, "#9E9E9E")]
    public void UpdateFrom_RespectsSeverityColor(int level, string expectedColor)
    {
        var vm = new OverlayViewModel();
        var state = OverlayState.FromDisplayLevel(level);
        vm.UpdateFrom(state);

        Assert.Equal(expectedColor, vm.MaskColor);
    }
}
```

**Step 2: 运行测试验证 FAIL**

Run: `dotnet test SitRight.Tests --filter "FullyQualifiedName~OverlayViewModelTests"`
Expected: FAIL — `OverlayViewModel` not found

**Step 3: 实现 OverlayViewModel（GREEN）**

创建 `SitRight/ViewModels/OverlayViewModel.cs`：

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SitRight.Models;

namespace SitRight.ViewModels;

public class OverlayViewModel : INotifyPropertyChanged
{
    private double _maskOpacity;
    private string _maskColor = "#FFFFFF";
    private double _edgeOpacity;
    private int _severityLevel;
    private bool _isVisible;

    public event PropertyChangedEventHandler? PropertyChanged;

    public double MaskOpacity
    {
        get => _maskOpacity;
        set => SetProperty(ref _maskOpacity, value);
    }

    public string MaskColor
    {
        get => _maskColor;
        set => SetProperty(ref _maskColor, value);
    }

    public double EdgeOpacity
    {
        get => _edgeOpacity;
        set => SetProperty(ref _edgeOpacity, value);
    }

    public int SeverityLevel
    {
        get => _severityLevel;
        set => SetProperty(ref _severityLevel, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public void UpdateFrom(OverlayState state)
    {
        MaskOpacity = state.MaskOpacity;
        MaskColor = state.MaskColor;
        EdgeOpacity = state.EdgeOpacity;
        SeverityLevel = state.SeverityLevel;
        IsVisible = state.MaskOpacity > 0.01;
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

**Step 4: 运行测试验证 PASS**

Run: `dotnet test SitRight.Tests --filter "FullyQualifiedName~OverlayViewModelTests"`
Expected: PASS

**Step 5: 运行全量测试确认无回归**

Run: `dotnet test SitRight.Tests`
Expected: ALL PASS

**Step 6: 提交**

```bash
git add SitRight/ViewModels/OverlayViewModel.cs SitRight.Tests/OverlayViewModelTests.cs
git commit -m "feat: 实现 OverlayViewModel (TDD)"
```

---

## Task 6: MainViewModel 主视图模型

**Files:**
- Create: `SitRight/ViewModels/MainViewModel.cs`
- Create: `SitRight.Tests/MainViewModelTests.cs`

**前置条件:** Task 2 (Moq) 和 Task 3 (ValueMapper) 已完成。

**Step 1: 编写测试（RED）**

创建 `SitRight.Tests/MainViewModelTests.cs`：

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
    private readonly ConfigService _configService;
    private readonly MainViewModel _viewModel;

    public MainViewModelTests()
    {
        _mockSerial = new Mock<ISerialService>();
        _mockSerial.Setup(s => s.GetAvailablePorts()).Returns(new[] { "COM1", "COM2" });

        _protocol = new DeviceProtocol();
        _stateManager = new DeviceStateManager();
        _valueMapper = new ValueMapper();

        var testPath = Path.Combine(Path.GetTempPath(), $"test_vm_{Guid.NewGuid()}.json");
        _configService = new ConfigService(testPath);

        _viewModel = new MainViewModel(
            _mockSerial.Object,
            _protocol,
            _stateManager,
            _valueMapper,
            _configService);
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
}
```

**Step 2: 运行测试验证 FAIL**

Run: `dotnet test SitRight.Tests --filter "FullyQualifiedName~MainViewModelTests"`
Expected: FAIL — `MainViewModel` not found

**Step 3: 实现 MainViewModel（GREEN）**

创建 `SitRight/ViewModels/MainViewModel.cs`：

```csharp
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

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<DeviceConnectionState>? OnConnectionStateChanged;
    public event Action<OverlayState>? OnOverlayStateChanged;
    public event Action<bool>? OnSimulationModeChanged;

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

    private void BindEvents()
    {
        _serialService.OnLineReceived += line =>
        {
            if (_protocol.TryParse(line, out var value))
            {
                _stateManager.ReceiveRawValue(value);
                RawValueText = value.ToString();
                LastReceiveTimeText = DateTime.Now.ToString("HH:mm:ss");

                var overlayState = _valueMapper.Map(value);
                DisplayValueText = value.ToString();
                OnOverlayStateChanged?.Invoke(overlayState);
            }
        };

        _serialService.OnError += ex =>
        {
            _stateManager.OnFault(ex.Message);
        };

        _stateManager.OnStateChanged += state =>
        {
            StatusText = state.ConnectionState.ToString();
            OnConnectionStateChanged?.Invoke(state.ConnectionState);
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

            var overlayState = _valueMapper.Map(value);
            DisplayValueText = value.ToString();
            OnOverlayStateChanged?.Invoke(overlayState);
        }
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

**Step 4: 运行测试验证 PASS**

Run: `dotnet test SitRight.Tests --filter "FullyQualifiedName~MainViewModelTests"`
Expected: PASS

**Step 5: 运行全量测试确认无回归**

Run: `dotnet test SitRight.Tests`
Expected: ALL PASS

**Step 6: 提交**

```bash
git add SitRight/ViewModels/MainViewModel.cs SitRight.Tests/MainViewModelTests.cs
git commit -m "feat: 实现 MainViewModel (TDD)"
```

---

## Task 7: MainWindow 迁移至 MVVM 绑定

**Files:**
- Modify: `SitRight/MainWindow.xaml`
- Modify: `SitRight/MainWindow.xaml.cs`
- Modify: `SitRight/OverlayWindow.xaml.cs`

**前置条件:** Task 3-6 全部完成。

**Step 1: 重构 MainWindow.xaml.cs 使用 MainViewModel**

完全重写 `SitRight/MainWindow.xaml.cs`：

```csharp
using System;
using System.Windows;
using System.Windows.Threading;
using SitRight.Models;
using SitRight.Services;
using SitRight.ViewModels;

namespace SitRight;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly OverlayWindow _overlay;
    private readonly DispatcherTimer _timeoutTimer;

    public MainWindow()
    {
        InitializeComponent();

        var configService = new ConfigService();
        var config = configService.Load();

        var serialService = new SerialService();
        var protocol = new DeviceProtocol();
        var stateManager = new DeviceStateManager();
        var valueMapper = new ValueMapper(config.HintStartLevel, config.UrgentLevel);

        _viewModel = new MainViewModel(
            serialService,
            protocol,
            stateManager,
            valueMapper,
            configService);

        _overlay = new OverlayWindow();
        _overlay.Show();

        _viewModel.OnOverlayStateChanged += state => _overlay.ApplyState(state);

        _timeoutTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timeoutTimer.Tick += (_, _) => stateManager.CheckTimeout();
        _timeoutTimer.Start();

        BindUIEvents();
        RefreshPorts();
        UpdateStatus(stateManager.State);

        Log("应用程序已启动");
    }

    private void BindUIEvents()
    {
        RefreshButton.Click += (_, _) => RefreshPorts();
        ConnectButton.Click += ConnectButtonClicked;

        SimulationModeCheckBox.Checked += (_, _) =>
        {
            _viewModel.IsSimulationMode = true;
            SimulatedValueSlider.IsEnabled = true;
        };

        SimulationModeCheckBox.Unchecked += (_, _) =>
        {
            _viewModel.IsSimulationMode = false;
            SimulatedValueSlider.IsEnabled = false;
            _overlay.ApplyState(new OverlayState());
        };

        SimulatedValueSlider.ValueChanged += (_, e) =>
        {
            var value = (int)e.NewValue;
            SimulatedValueText.Text = value.ToString();
            _viewModel.SimulateValue(value);
        };

        _viewModel.OnConnectionStateChanged += _ => Dispatcher.Invoke(() =>
        {
            ConnectButton.Content = _viewModel.IsConnected ? "断开" : "连接";
        });
    }

    private void ConnectButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsConnected)
        {
            _viewModel.Disconnect();
            return;
        }

        var portName = ComPortComboBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(portName))
        {
            Log("请先选择 COM 口");
            return;
        }

        try
        {
            var baudRate = GetSelectedBaudRate();
            _viewModel.Connect(portName, baudRate);
        }
        catch (Exception ex)
        {
            Log($"连接失败: {ex.Message}");
        }
    }

    private int GetSelectedBaudRate()
    {
        if (BaudRateComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem comboBoxItem)
        {
            var contentText = comboBoxItem.Content?.ToString();
            if (int.TryParse(contentText, out var comboValue))
                return comboValue;
        }

        if (int.TryParse(BaudRateComboBox.Text, out var textValue))
            return textValue;

        return 115200;
    }

    private void RefreshPorts()
    {
        var ports = _viewModel.AvailablePorts;
        ComPortComboBox.ItemsSource = ports;

        if (ports.Length > 0 && ComPortComboBox.SelectedItem is null)
            ComPortComboBox.SelectedIndex = 0;

        Log($"已刷新串口列表: {ports.Length} 个端口");
    }

    private void UpdateStatus(DeviceState state)
    {
        StatusText.Text = state.ConnectionState.ToString();
        StatusText.Foreground = state.ConnectionState switch
        {
            DeviceConnectionState.Receiving or DeviceConnectionState.ConnectedIdle => System.Windows.Media.Brushes.Green,
            DeviceConnectionState.Connecting => System.Windows.Media.Brushes.DodgerBlue,
            DeviceConnectionState.Timeout => System.Windows.Media.Brushes.Orange,
            DeviceConnectionState.Fault => System.Windows.Media.Brushes.Red,
            _ => System.Windows.Media.Brushes.Gray
        };

        if (!state.LastReceiveTime.HasValue && state.ConnectionState == DeviceConnectionState.Disconnected)
        {
            RawValueText.Text = "--";
            DisplayValueText.Text = "--";
            LastReceiveTimeText.Text = "--";
        }
    }

    protected void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        Dispatcher.Invoke(() =>
        {
            LogTextBox.AppendText($"[{timestamp}] {message}\n");
            LogTextBox.ScrollToEnd();
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        _timeoutTimer.Stop();
        _overlay.Close();
        base.OnClosed(e);
    }
}
```

**Step 2: 更新 MainWindow.xaml**

移除 SimulationModeCheckBox 和 SimulatedValueSlider 的 `Checked`/`Unchecked`/`ValueChanged` 事件属性（改由 code-behind 绑定）。修改第 57-59 行：

```xml
<CheckBox x:Name="SimulationModeCheckBox" Content="启用模拟" VerticalAlignment="Center"/>
<Slider x:Name="SimulatedValueSlider" Minimum="0" Maximum="100" Value="0"
        Width="200" Margin="16,0,0,0" IsEnabled="False"/>
```

**Step 3: 验证编译**

Run: `dotnet build SitRight/SitRight.csproj`
Expected: BUILD SUCCEEDED

**Step 4: 运行全量测试确认无回归**

Run: `dotnet test SitRight.Tests`
Expected: ALL PASS

**Step 5: 提交**

```bash
git add SitRight/MainWindow.xaml SitRight/MainWindow.xaml.cs
git commit -m "feat: MainWindow 迁移至 MVVM 绑定"
```

---

## 交付验证

所有 Task 完成后，运行：

```bash
dotnet test SitRight.Tests
dotnet build SitRight/SitRight.csproj
```

两项均通过即为成功。

### 最终文件清单

| 文件 | 操作 |
|------|------|
| `SitRight/Models/AppConfig.cs` | 修改（移除 2 个字段） |
| `SitRight.Tests/AppConfigTests.cs` | 修改（移除 2 个断言） |
| `SitRight.Tests/SitRight.Tests.csproj` | 修改（添加 Moq） |
| `SitRight/Services/ValueMapper.cs` | 新建 |
| `SitRight.Tests/ValueMapperTests.cs` | 新建 |
| `SitRight/Services/ConfigService.cs` | 新建 |
| `SitRight.Tests/ConfigServiceTests.cs` | 新建 |
| `SitRight/ViewModels/OverlayViewModel.cs` | 新建 |
| `SitRight.Tests/OverlayViewModelTests.cs` | 新建 |
| `SitRight/ViewModels/MainViewModel.cs` | 新建 |
| `SitRight.Tests/MainViewModelTests.cs` | 新建 |
| `SitRight/MainWindow.xaml` | 修改 |
| `SitRight/MainWindow.xaml.cs` | 修改 |
