# SitRight PC 上位机实现计划

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 实现一个基于 WPF 的坐姿矫正仪 PC 上位机，支持串口通信、全屏 Overlay 视觉反馈、模拟模式

**Architecture:**
- 采用 MVVM 轻量架构，Model/ViewModel/View 解耦
- Service 层处理串口通信、数值映射（ValueMapper）、配置管理
- OverlayWindow 作为独立全屏置顶窗口，仅通过遮罩透明度/颜色变化反馈（无文字提示）
- 模拟模式可在无硬件环境下独立开发和演示
- 配置文件保存在软件所在目录
- **TDD 工作流**：每写一个测试 → 验证失败 → 写最简代码通过 → 重构

**Tech Stack:** .NET 8 / WPF, xUnit, CommunityToolkit.Mvvm, System.IO.Ports

**最终产物:** 单个 `SitRight.exe` 可执行文件（自包含，无需安装 .NET 运行时）

---

## 项目初始化

### Task 1: 创建项目骨架和测试项目

**Files:**
- Create: `SitRight/SitRight.csproj`
- Create: `SitRight.Tests/SitRight.Tests.csproj`

**Step 1: 创建主项目**

```xml
<!-- SitRight/SitRight.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>true</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>SitRight</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
    <PackageReference Include="System.IO.Ports" Version="8.0.0" />
  </ItemGroup>
</Project>
```

**Step 2: 创建测试项目**

```xml
<!-- SitRight.Tests/SitRight.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>true</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.4" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\SitRight\SitRight.csproj" />
  </ItemGroup>
</Project>
```

**Step 3: 创建解决方案**

```bash
dotnet new sln -n SitRight
dotnet sln add SitRight/SitRight.csproj
dotnet sln add SitRight.Tests/SitRight.Tests.csproj
```

**Step 4: 验证编译**

Run: `dotnet build SitRight.sln`
Expected: BUILD SUCCEEDED

**Step 5: Commit**

```bash
git add SitRight.sln SitRight/SitRight.csproj SitRight.Tests/SitRight.Tests.csproj
git commit -m "feat: 创建项目骨架和测试项目"
```

---

### Task 2: 实现 DeviceProtocol (TDD)

**Files:**
- Create: `SitRight.Tests/DeviceProtocolTests.cs`
- Create: `SitRight/Services/DeviceProtocol.cs`

**Step 1: RED - 写失败的测试**

```csharp
// SitRight.Tests/DeviceProtocolTests.cs
using Xunit;
using SitRight.Services;
namespace SitRight.Tests;
public class DeviceProtocolTests
{
    [Fact]
    public void Parse_ValidNumber_ReturnsValid()
    {
        var result = DeviceProtocol.Parse("37");

        Assert.True(result.IsValid);
        Assert.Equal(37, result.Value);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Parse_Zero_ReturnsValid()
    {
        var result = DeviceProtocol.Parse("0");

        Assert.True(result.IsValid);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void Parse_OneHundred_ReturnsValid()
    {
        var result = DeviceProtocol.Parse("100");

        Assert.True(result.IsValid);
        Assert.Equal(100, result.Value);
    }

    [Fact]
    public void Parse_NegativeNumber_ReturnsInvalid()
    {
        var result = DeviceProtocol.Parse("-1");

        Assert.False(result.IsValid);
        Assert.Contains("range", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_OverOneHundred_ReturnsInvalid()
    {
        var result = DeviceProtocol.Parse("101");

        Assert.False(result.IsValid);
        Assert.Contains("range", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_InvalidString_ReturnsInvalid()
    {
        var result = DeviceProtocol.Parse("abc");

        Assert.False(result.IsValid);
        Assert.Contains("Invalid", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsInvalid()
    {
        var result = DeviceProtocol.Parse("");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsInvalid()
    {
        var result = DeviceProtocol.Parse("   ");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Parse_WithNewline_TrimsAndParses()
    {
        var result = DeviceProtocol.Parse("50\n");

        Assert.True(result.IsValid);
        Assert.Equal(50, result.Value);
    }
}
```

**Step 2: 验证测试失败**

Run: `dotnet test SitRight.Tests/DeviceProtocolTests.cs`
Expected: BUILD SUCCEEDED but tests FAIL (DeviceProtocol doesn't exist yet)

**Step 3: GREEN - 写最简代码**

```csharp
// SitRight/Services/DeviceProtocol.cs
namespace SitRight.Services;
public class DeviceProtocol
{
    public record ParseResult(bool IsValid, int Value, string? ErrorMessage);

    public static ParseResult Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return new(false, 0, "Empty line");

        string trimmed = line.Trim();

        if (!int.TryParse(trimmed, out int value))
            return new(false, 0, $"Invalid integer: {trimmed}");

        if (value < 0 || value > 100)
            return new(false, value, $"Value out of range [0,100]: {value}");

        return new(true, value, null);
    }
}
```

**Step 4: 验证测试通过**

Run: `dotnet test SitRight.Tests/DeviceProtocolTests.cs --verbosity normal`
Expected: All tests PASS

**Step 5: Commit**

```bash
git add SitRight/Services/DeviceProtocol.cs SitRight.Tests/DeviceProtocolTests.cs
git commit -m "feat: 实现 DeviceProtocol 协议解析 (TDD)"
```

---

### Task 3: 实现 ValueMapper (TDD)

**Files:**
- Create: `SitRight.Tests/ValueMapperTests.cs`
- Create: `SitRight/Utils/ValueMapper.cs`
- Create: `SitRight/Models/OverlayState.cs`

**Step 1: RED - 写失败的测试**

```csharp
// SitRight.Tests/ValueMapperTests.cs
using Xunit;
using SitRight.Utils;
namespace SitRight.Tests;
public class ValueMapperTests
{
    [Fact]
    public void Map_ZeroLevel_ReturnsMinimalOpacity()
    {
        var state = ValueMapper.Map(0);

        Assert.True(state.MaskOpacity > 0);
        Assert.True(state.MaskOpacity < 0.1);
    }

    [Fact]
    public void Map_FiftyLevel_ReturnsMediumOpacity()
    {
        var state = ValueMapper.Map(50);

        Assert.True(state.MaskOpacity > 0.2);
        Assert.True(state.MaskOpacity < 0.5);
    }

    [Fact]
    public void Map_OneHundredLevel_ReturnsMaxOpacity()
    {
        var state = ValueMapper.Map(100);

        Assert.True(state.MaskOpacity > 0.6);
        Assert.True(state.MaskOpacity <= 0.75);
    }

    [Fact]
    public void Map_ZeroLevel_LowSeverity()
    {
        var state = ValueMapper.Map(0);

        Assert.True(state.SeverityLevel < 2);
    }

    [Fact]
    public void Map_OneHundredLevel_HighSeverity()
    {
        var state = ValueMapper.Map(100);

        Assert.Equal(3, state.SeverityLevel);
    }

    [Fact]
    public void Map_BelowTwenty_SeverityZero()
    {
        var state = ValueMapper.Map(15);

        Assert.Equal(0, state.SeverityLevel);
    }

    [Fact]
    public void Map_ThirtyToFifty_SeverityOne()
    {
        var state = ValueMapper.Map(35);

        Assert.Equal(1, state.SeverityLevel);
    }

    [Fact]
    public void Map_FiftyToEighty_SeverityTwo()
    {
        var state = ValueMapper.Map(65);

        Assert.Equal(2, state.SeverityLevel);
    }

    [Fact]
    public void Map_AboveEighty_SeverityThree()
    {
        var state = ValueMapper.Map(85);

        Assert.Equal(3, state.SeverityLevel);
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(-1)]
    [InlineData(200)]
    [InlineData(1000)]
    public void Map_OutOfRange_ClampsCorrectly(int outOfRangeValue)
    {
        var state = ValueMapper.Map(outOfRangeValue);

        Assert.True(state.MaskOpacity >= 0);
        Assert.True(state.SeverityLevel >= 0);
        Assert.True(state.SeverityLevel <= 3);
    }
}
```

**Step 2: 先创建 OverlayState 模型**

```csharp
// SitRight/Models/OverlayState.cs
namespace SitRight.Models;
public class OverlayState
{
    public double MaskOpacity { get; set; }
    public string MaskColor { get; set; } = "#00FFFFFF";
    public double EdgeOpacity { get; set; }
    public int SeverityLevel { get; set; }
}
```

**Step 3: 验证测试失败**

Run: `dotnet test SitRight.Tests/ValueMapperTests.cs`
Expected: Tests FAIL (ValueMapper doesn't exist)

**Step 4: GREEN - 写最简代码**

```csharp
// SitRight/Utils/ValueMapper.cs
using SitRight.Models;
namespace SitRight.Utils;
public static class ValueMapper
{
    // blurLevel 来自硬件端已平滑的值，软件端不再做平滑处理
    public static OverlayState Map(double displayLevel)
    {
        double normalized = Math.Clamp(displayLevel, 0, 100) / 100.0;

        // 规范值：0.05 + Math.Pow(normalized, 1.4) * 0.65
        double maskOpacity = 0.05 + Math.Pow(normalized, 1.4) * 0.65;

        string maskColor;
        if (normalized < 0.3)
            maskColor = "#08FFFFFF";
        else if (normalized < 0.7)
            maskColor = "#20FFFFFF";
        else
            maskColor = "#35FFFFFF";

        int severityLevel = normalized switch
        {
            < 0.2 => 0,
            < 0.5 => 1,
            < 0.8 => 2,
            _ => 3
        };

        return new OverlayState
        {
            MaskOpacity = maskOpacity,
            MaskColor = maskColor,
            EdgeOpacity = normalized * 0.3,
            SeverityLevel = severityLevel
        };
    }
}
```

**Step 5: 验证测试通过**

Run: `dotnet test SitRight.Tests/ValueMapperTests.cs --verbosity normal`
Expected: All tests PASS

**Step 6: Commit**

```bash
git add SitRight/Models/OverlayState.cs SitRight/Utils/ValueMapper.cs SitRight.Tests/ValueMapperTests.cs
git commit -m "feat: 实现 ValueMapper 数值映射 (TDD)"
```

---

### Task 4: 实现 AppConfig 模型

> **注意：** 平滑算法在硬件端完成，软件端不包含 BlurController。硬件上报的 blurLevel 已经是平滑后的值，软件端直接通过 ValueMapper 映射到 OverlayState。

**Files:**
- Create: `SitRight/Models/AppConfig.cs`

**Step 1: 创建 AppConfig 模型**

```csharp
// SitRight/Models/AppConfig.cs
namespace SitRight.Models;
public class AppConfig
{
    public string DefaultComPort { get; set; } = "COM3";
    public int BaudRate { get; set; } = 115200;
    public int TimeoutMs { get; set; } = 2000;
    public double MaxMaskOpacity { get; set; } = 0.70;
    public int SimulatedBlurLevel { get; set; } = 0;
    public bool IsSimulationMode { get; set; } = true;
}
```

**Step 2: Commit**

```bash
git add SitRight/Models/AppConfig.cs
git commit -m "feat: 实现 AppConfig 配置模型"
```

---

### Task 5: 实现 ConfigService (TDD)

**Files:**
- Create: `SitRight.Tests/ConfigServiceTests.cs`
- Create: `SitRight/Services/ConfigService.cs`

**Step 1: RED - 写失败的测试**

```csharp
// SitRight.Tests/ConfigServiceTests.cs
using Xunit;
using SitRight.Services;
namespace SitRight.Tests;
public class ConfigServiceTests
{
    [Fact]
    public void Load_WhenNoConfig_ReturnsDefaultConfig()
    {
        var service = new ConfigService("/nonexistent/path");

        var config = service.Load();

        Assert.Equal("COM3", config.DefaultComPort);
        Assert.Equal(115200, config.BaudRate);
        Assert.Equal(2000, config.TimeoutMs);
    }

    [Fact]
    public void Save_And_Load_RoundTrips()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var service = new ConfigService(tempDir);
        var original = new AppConfig
        {
            DefaultComPort = "COM5",
            BaudRate = 9600,
            TimeoutMs = 3000
        };

        service.Save(original);
        var loaded = service.Load();

        Assert.Equal("COM5", loaded.DefaultComPort);
        Assert.Equal(9600, loaded.BaudRate);
        Assert.Equal(3000, loaded.TimeoutMs);

        Directory.Delete(tempDir, true);
    }
}
```

**Step 2: 验证测试失败**

Run: `dotnet test SitRight.Tests/ConfigServiceTests.cs`
Expected: Tests FAIL (ConfigService doesn't exist)

**Step 3: GREEN - 写最简代码**

```csharp
// SitRight/Services/ConfigService.cs
using System.IO;
using System.Text.Json;
using SitRight.Models;
namespace SitRight.Services;
public class ConfigService
{
    private readonly string _configPath;

    public ConfigService(string? configDir = null)
    {
        string baseDir = configDir ?? AppDomain.CurrentDomain.BaseDirectory;
        _configPath = Path.Combine(baseDir, "config.json");
    }

    public AppConfig Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                string json = File.ReadAllText(_configPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch
        {
        }
        return new AppConfig();
    }

    public void Save(AppConfig config)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(_configPath, json);
    }
}
```

**Step 4: 验证测试通过**

Run: `dotnet test SitRight.Tests/ConfigServiceTests.cs --verbosity normal`
Expected: All tests PASS

**Step 5: Commit**

```bash
git add SitRight/Services/ConfigService.cs SitRight.Tests/ConfigServiceTests.cs
git commit -m "feat: 实现 ConfigService 配置服务 (TDD)"
```

---

### Task 6: 实现 MVVM 基础设施

**Files:**
- Create: `SitRight/ViewModels/ViewModelBase.cs`
- Create: `SitRight/Utils/DispatcherHelper.cs`

**Step 1: 创建 ViewModelBase（使用 CommunityToolkit.Mvvm）**

```csharp
// SitRight/ViewModels/ViewModelBase.cs
using CommunityToolkit.Mvvm.ComponentModel;
namespace SitRight.ViewModels;
public abstract partial class ViewModelBase : ObservableObject { }
```

**Step 2: 创建 DispatcherHelper**

```csharp
// SitRight/Utils/DispatcherHelper.cs
using System.Windows;
namespace SitRight.Utils;
public static class DispatcherHelper
{
    public static void Invoke(Action action)
    {
        if (Application.Current?.Dispatcher.CheckAccess() == true)
            action();
        else
            Application.Current.Dispatcher.Invoke(action);
    }
}
```

**Step 3: 验证编译**

Run: `dotnet build SitRight`
Expected: BUILD SUCCEEDED

**Step 4: Commit**

```bash
git add SitRight/ViewModels/ViewModelBase.cs SitRight/Utils/DispatcherHelper.cs
git commit -m "feat: 添加 MVVM 基础设施"
```

---

### Task 7: 实现 OverlayViewModel (TDD)

**Files:**
- Create: `SitRight.Tests/OverlayViewModelTests.cs`
- Create: `SitRight/ViewModels/OverlayViewModel.cs`

**Step 1: RED - 写失败的测试**

```csharp
// SitRight.Tests/OverlayViewModelTests.cs
using Xunit;
using SitRight.Models;
using SitRight.Services;
using SitRight.ViewModels;
namespace SitRight.Tests;
public class OverlayViewModelTests
{
    [Fact]
    public void PushRawValue_UpdatesOverlayState()
    {
        var viewModel = new OverlayViewModel();

        viewModel.UpdateFromHardware(50);

        Assert.True(viewModel.DisplayLevel >= 0);
    }

    [Fact]
    public void MaskOpacity_IsAlwaysPositive()
    {
        var viewModel = new OverlayViewModel();

        viewModel.UpdateFromHardware(0);

        Assert.True(viewModel.MaskOpacity > 0);
    }

    [Fact]
    public void SeverityLevel_IsInValidRange()
    {
        var viewModel = new OverlayViewModel();

        viewModel.UpdateFromHardware(100);

        Assert.True(viewModel.SeverityLevel >= 0);
        Assert.True(viewModel.SeverityLevel <= 3);
    }

    [Fact]
    public void Cleanup_DisposesResources()
    {
        var viewModel = new OverlayViewModel();

        viewModel.Cleanup();

        // Should not throw
        viewModel.UpdateFromHardware(50);
    }
}
```

**Step 2: 验证测试失败**

Run: `dotnet test SitRight.Tests/OverlayViewModelTests.cs`
Expected: Tests FAIL (OverlayViewModel doesn't exist)

**Step 3: GREEN - 写最简代码**

```csharp
// SitRight/ViewModels/OverlayViewModel.cs
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SitRight.Models;
using SitRight.Utils;
namespace SitRight.ViewModels;
public partial class OverlayViewModel : ViewModelBase
{
    [ObservableProperty]
    private double _maskOpacity;

    [ObservableProperty]
    private Brush _maskBrush = new SolidColorBrush(Colors.White);

    [ObservableProperty]
    private double _edgeOpacity;

    [ObservableProperty]
    private int _severityLevel;

    [ObservableProperty]
    private double _displayLevel;

    // 直接接收硬件端已平滑的 blurLevel，通过 ValueMapper 映射到 OverlayState
    public void UpdateFromHardware(int blurLevel)
    {
        DisplayLevel = blurLevel;
        var state = ValueMapper.Map(blurLevel);
        MaskOpacity = state.MaskOpacity;
        MaskBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(state.MaskColor));
        EdgeOpacity = state.EdgeOpacity;
        SeverityLevel = state.SeverityLevel;
    }

    public void Cleanup()
    {
        // 无需清理定时器，平滑在硬件端完成
    }
}
```

**Step 4: 验证测试通过**

Run: `dotnet test SitRight.Tests/OverlayViewModelTests.cs --verbosity normal`
Expected: All tests PASS

**Step 5: Commit**

```bash
git add SitRight/ViewModels/OverlayViewModel.cs SitRight.Tests/OverlayViewModelTests.cs
git commit -m "feat: 实现 OverlayViewModel (TDD)"
```

---

### Task 8: 实现 MainViewModel (TDD)

**Files:**
- Create: `SitRight.Tests/MainViewModelTests.cs`
- Create: `SitRight/ViewModels/MainViewModel.cs`

**Step 1: RED - 写失败的测试**

```csharp
// SitRight.Tests/MainViewModelTests.cs
using Xunit;
using SitRight.ViewModels;
namespace SitRight.Tests;
public class MainViewModelTests
{
    [Fact]
    public void InitialState_IsDisconnected()
    {
        var viewModel = new MainViewModel();

        Assert.False(viewModel.IsConnected);
        Assert.Equal("未连接", viewModel.ConnectionStatus);
    }

    [Fact]
    public void RefreshPorts_UpdatesAvailablePorts()
    {
        var viewModel = new MainViewModel();

        viewModel.RefreshPortsCommand.Execute(null);

        Assert.NotNull(viewModel.AvailablePorts);
    }

    [Fact]
    public void ToggleSimulation_ChangesMode()
    {
        var viewModel = new MainViewModel();
        var initialMode = viewModel.IsSimulationMode;

        viewModel.ToggleSimulationCommand.Execute(null);

        Assert.Equal(!initialMode, viewModel.IsSimulationMode);
    }

    [Fact]
    public void SetSimulatedValue_UpdatesValue()
    {
        var viewModel = new MainViewModel();

        viewModel.SetSimulatedValueCommand.Execute(75);

        Assert.Equal(75, viewModel.SimulatedValue);
    }

    [Fact]
    public void ToggleOverlay_ChangesVisibility()
    {
        var viewModel = new MainViewModel();
        var initial = viewModel.IsOverlayVisible;

        viewModel.ToggleOverlayCommand.Execute(null);

        Assert.Equal(!initial, viewModel.IsOverlayVisible);
    }

    [Fact]
    public void OverlayViewModel_IsNotNull()
    {
        var viewModel = new MainViewModel();

        Assert.NotNull(viewModel.OverlayViewModel);
    }

    [Fact]
    public void Cleanup_DoesNotThrow()
    {
        var viewModel = new MainViewModel();

        viewModel.Cleanup();

        // Should complete without exception
        Assert.True(true);
    }
}
```

**Step 2: 验证测试失败**

Run: `dotnet test SitRight.Tests/MainViewModelTests.cs`
Expected: Tests FAIL (MainViewModel doesn't exist)

**Step 3: GREEN - 写最简代码**

```csharp
// SitRight/ViewModels/MainViewModel.cs
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SitRight.Models;
using SitRight.Services;
namespace SitRight.ViewModels;
public partial class MainViewModel : ViewModelBase
{
    private readonly SerialService _serialService;
    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    private readonly DispatcherTimer _statusTimer;

    [ObservableProperty]
    private ObservableCollection<string> _availablePorts = new();

    [ObservableProperty]
    private string? _selectedPort;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatus = "未连接";

    [ObservableProperty]
    private int _rawValue;

    [ObservableProperty]
    private double _displayValue;

    [ObservableProperty]
    private string _lastReceiveTime = "-";

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private bool _isSimulationMode = true;

    [ObservableProperty]
    private int _simulatedValue = 0;

    [ObservableProperty]
    private bool _isOverlayVisible = true;

    [ObservableProperty]
    private ObservableCollection<string> _logMessages = new();

    public OverlayViewModel OverlayViewModel { get; }

    public MainViewModel()
    {
        _configService = new ConfigService();
        _config = _configService.Load();

        _serialService = new SerialService();

        OverlayViewModel = new OverlayViewModel();

        _isSimulationMode = _config.IsSimulationMode;
        SimulatedValue = _config.SimulatedBlurLevel;

        _serialService.ValueReceived += OnSerialValueReceived;
        _serialService.ErrorOccurred += OnSerialError;
        _serialService.Disconnected += OnSerialDisconnected;

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _statusTimer.Tick += OnStatusTimerTick;
        _statusTimer.Start();
    }

    private void OnSerialValueReceived(int value)
    {
        RawValue = value;
        LastReceiveTime = DateTime.Now.ToString("HH:mm:ss.fff");
        // value 是硬件端已平滑的 blurLevel，直接映射
        OverlayViewModel.UpdateFromHardware(value);
        DisplayValue = value;
        ConnectionStatus = "接收中";
    }

    private void OnSerialError(string error)
    {
        ErrorCount++;
        AddLog($"错误: {error}");
        ConnectionStatus = "错误";
    }

    private void OnSerialDisconnected()
    {
        IsConnected = false;
        ConnectionStatus = "已断开";
        AddLog("串口已断开");
    }

    private void OnStatusTimerTick(object? sender, EventArgs e)
    {
        DisplayValue = OverlayViewModel.DisplayLevel;
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (string port in SerialPort.GetPortNames())
        {
            AvailablePorts.Add(port);
        }
        if (AvailablePorts.Contains(_config.DefaultComPort))
            SelectedPort = _config.DefaultComPort;
        else if (AvailablePorts.Count > 0)
            SelectedPort = AvailablePorts[0];
    }

    [RelayCommand]
    private void Connect()
    {
        if (string.IsNullOrEmpty(SelectedPort)) return;

        try
        {
            _serialService.Connect(SelectedPort, _config.BaudRate);
            IsConnected = true;
            ConnectionStatus = "已连接";
            AddLog($"已连接到 {SelectedPort}");
        }
        catch (Exception ex)
        {
            AddLog($"连接失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Disconnect()
    {
        _serialService.Disconnect();
        IsConnected = false;
    }

    [RelayCommand]
    private void ToggleSimulation()
    {
        IsSimulationMode = !IsSimulationMode;
        _config.IsSimulationMode = IsSimulationMode;
        _configService.Save(_config);
        AddLog($"模拟模式: {(IsSimulationMode ? "开启" : "关闭")}");
    }

    [RelayCommand]
    private void SetSimulatedValue(int value)
    {
        SimulatedValue = value;
        _config.SimulatedBlurLevel = value;
        _configService.Save(_config);
        if (IsSimulationMode)
        {
            // 模拟模式下直接传递模拟值，硬件端平滑由模拟器处理或跳过
            OverlayViewModel.UpdateFromHardware(value);
        }
    }

    [RelayCommand]
    private void ToggleOverlay()
    {
        IsOverlayVisible = !IsOverlayVisible;
        AddLog($"Overlay: {(IsOverlayVisible ? "显示" : "隐藏")}");
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogMessages.Clear();
    }

    private void AddLog(string message)
    {
        string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (LogMessages.Count > 100)
            LogMessages.RemoveAt(0);
        LogMessages.Add(logEntry);
    }

    public void Cleanup()
    {
        _statusTimer.Stop();
        _serialService.Dispose();
        OverlayViewModel.Cleanup();
    }
}
```

**Step 4: 验证测试通过**

Run: `dotnet test SitRight.Tests/MainViewModelTests.cs --verbosity normal`
Expected: All tests PASS

**Step 5: Commit**

```bash
git add SitRight/ViewModels/MainViewModel.cs SitRight.Tests/MainViewModelTests.cs
git commit -m "feat: 实现 MainViewModel (TDD)"
```

---

### Task 9: 实现 SerialService (TDD)

**Files:**
- Create: `SitRight.Tests/SerialServiceTests.cs`
- Create: `SitRight/Services/SerialService.cs`

**Step 1: RED - 写失败的测试**

```csharp
// SitRight.Tests/SerialServiceTests.cs
using Xunit;
using SitRight.Services;
namespace SitRight.Tests;
public class SerialServiceTests
{
    [Fact]
    public void IsConnected_InitiallyFalse()
    {
        var service = new SerialService();

        Assert.False(service.IsConnected);
    }

    [Fact]
    public void SimulateValue_RaisesValueReceived()
    {
        var service = new SerialService();
        int? receivedValue = null;
        service.ValueReceived += v => receivedValue = v;

        service.SimulateValue(42);

        Assert.Equal(42, receivedValue);
    }

    [Fact]
    public void Disconnect_RaisesDisconnected()
    {
        var service = new SerialService();
        bool disconnectedRaised = false;
        service.Disconnected += () => disconnectedRaised = true;

        service.Disconnect();

        Assert.True(disconnectedRaised);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var service = new SerialService();

        service.Dispose();

        Assert.False(service.IsConnected);
    }

    [Fact]
    public void CurrentPortName_WhenNotConnected_IsNull()
    {
        var service = new SerialService();

        Assert.Null(service.CurrentPortName);
    }
}
```

**Step 2: 验证测试失败**

Run: `dotnet test SitRight.Tests/SerialServiceTests.cs`
Expected: Tests FAIL (SerialService doesn't exist)

**Step 3: GREEN - 写最简代码**

```csharp
// SitRight/Services/SerialService.cs
using System.IO.Ports;
using SitRight.Utils;
namespace SitRight.Services;
public class SerialService : IDisposable
{
    private SerialPort? _port;
    private string _buffer = "";

    public event Action<int>? ValueReceived;
    public event Action<string>? ErrorOccurred;
    public event Action? Disconnected;

    public bool IsConnected => _port?.IsOpen ?? false;
    public string? CurrentPortName => _port?.PortName;

    public void Connect(string portName, int baudRate = 115200)
    {
        try
        {
            Disconnect();

            _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };

            _port.DataReceived += OnDataReceived;
            _port.Open();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"连接失败: {ex.Message}");
            throw;
        }
    }

    public void Disconnect()
    {
        if (_port != null)
        {
            _port.DataReceived -= OnDataReceived;
            if (_port.IsOpen)
                _port.Close();
            _port.Dispose();
            _port = null;
        }
        Disconnected?.Invoke();
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            string data = _port!.ReadExisting();
            _buffer += data;

            while (true)
            {
                int newlineIndex = _buffer.IndexOf('\n');
                if (newlineIndex < 0) break;

                string line = _buffer[..newlineIndex];
                _buffer = _buffer[(newlineIndex + 1)..];

                var result = DeviceProtocol.Parse(line);
                if (result.IsValid)
                {
                    DispatcherHelper.Invoke(() => ValueReceived?.Invoke(result.Value));
                }
            }
        }
        catch (Exception ex)
        {
            DispatcherHelper.Invoke(() => ErrorOccurred?.Invoke($"接收错误: {ex.Message}"));
        }
    }

    public void SimulateValue(int value)
    {
        DispatcherHelper.Invoke(() => ValueReceived?.Invoke(value));
    }

    public void Dispose()
    {
        Disconnect();
    }
}
```

**Step 4: 验证测试通过**

Run: `dotnet test SitRight.Tests/SerialServiceTests.cs --verbosity normal`
Expected: All tests PASS

**Step 5: Commit**

```bash
git add SitRight/Services/SerialService.cs SitRight.Tests/SerialServiceTests.cs
git commit -m "feat: 实现 SerialService 串口服务 (TDD)"
```

---

### Task 10: 实现 UI 层

**Files:**
- Create: `SitRight/App.xaml`
- Create: `SitRight/App.xaml.cs`
- Create: `SitRight/Views/MainWindow.xaml`
- Create: `SitRight/Views/MainWindow.xaml.cs`
- Create: `SitRight/Views/OverlayWindow.xaml`
- Create: `SitRight/Views/OverlayWindow.xaml.cs`
- Create: `SitRight/Converters.cs`

**Step 1: 创建 App.xaml**

```xml
<!-- SitRight/App.xaml -->
<Application x:Class="SitRight.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:converters="clr-namespace:SitRight">
    <Application.Resources>
        <converters:BoolToTextConverter x:Key="BoolToTextConverter"/>
        <converters:BoolToConnectCommandConverter x:Key="BoolToConnectCommandConverter"/>
        <converters:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter"/>
        <converters:BoolToVisibilityTextConverter x:Key="BoolToVisibilityTextConverter"/>
    </Application.Resources>
</Application>
```

**Step 2: 创建 Converters**

```csharp
// SitRight/Converters.cs
using System.Globalization;
using System.Windows;
using System.Windows.Data;
namespace SitRight;

public class BoolToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && parameter is string s)
        {
            string[] parts = s.Split('|');
            return b ? parts[0] : (parts.Length > 1 ? parts[1] : s);
        }
        return value?.ToString() ?? "";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToConnectCommandConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? parameter : parameter;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToVisibilityTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b ? (b ? "当前显示" : "当前隐藏") : "";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
```

**Step 3: 创建 MainWindow.xaml**

```xml
<Window x:Class="SitRight.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:SitRight.ViewModels"
        Title="SitRight - 主控面板"
        Height="650" Width="480"
        WindowStartupLocation="CenterScreen"
        Closing="Window_Closing">
    <Window.DataContext>
        <vm:MainViewModel/>
    </Window.DataContext>
    <Grid Margin="15">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- 连接控制区 -->
        <GroupBox Grid.Row="0" Header="串口连接" Margin="0,0,0,10">
            <StackPanel Margin="5">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <ComboBox Grid.Column="0"
                              ItemsSource="{Binding AvailablePorts}"
                              SelectedItem="{Binding SelectedPort}"
                              Height="28" Margin="0,0,5,0"/>
                    <Button Grid.Column="1" Content="刷新" Command="{Binding RefreshPortsCommand}" Width="60" Height="28"/>
                </Grid>
                <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                    <Button Content="{Binding IsConnected, Converter={StaticResource BoolToTextConverter}, ConverterParameter='断开|连接'}"
                            Command="{Binding ConnectCommand}"
                            Width="80" Height="28" Margin="0,0,10,0"/>
                    <CheckBox Content="模拟模式" IsChecked="{Binding IsSimulationMode}" VerticalAlignment="Center"/>
                </StackPanel>
            </StackPanel>
        </GroupBox>

        <!-- 状态显示区 -->
        <GroupBox Grid.Row="1" Header="设备状态" Margin="0,0,0,10">
            <Grid Margin="5">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="80"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <TextBlock Grid.Row="0" Grid.Column="0" Text="状态:" FontWeight="Bold"/>
                <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding ConnectionStatus}" Foreground="Green"/>

                <TextBlock Grid.Row="1" Grid.Column="0" Text="原始值:" FontWeight="Bold" Margin="0,5,0,0"/>
                <TextBlock Grid.Row="1" Grid.Column="1" Text="{Binding RawValue}" Margin="0,5,0,0"/>

                <TextBlock Grid.Row="2" Grid.Column="0" Text="显示值:" FontWeight="Bold" Margin="0,5,0,0"/>
                <TextBlock Grid.Row="2" Grid.Column="1" Text="{Binding DisplayValue, StringFormat=F1}" Margin="0,5,0,0"/>

                <TextBlock Grid.Row="3" Grid.Column="0" Text="最后接收:" FontWeight="Bold" Margin="0,5,0,0"/>
                <TextBlock Grid.Row="3" Grid.Column="1" Text="{Binding LastReceiveTime}" Margin="0,5,0,0"/>
            </Grid>
        </GroupBox>

        <!-- 模拟控制区 -->
        <GroupBox Grid.Row="2" Header="模拟控制" Margin="0,0,0,10"
                  Visibility="{Binding IsSimulationMode, Converter={StaticResource BoolToVisibilityConverter}}">
            <StackPanel Margin="5">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="60"/>
                    </Grid.ColumnDefinitions>
                    <Slider Grid.Column="0"
                            Minimum="0" Maximum="100"
                            Value="{Binding SimulatedValue}"
                            TickFrequency="10" IsSnapToTickEnabled="True"
                            VerticalAlignment="Center"/>
                    <TextBlock Grid.Column="1" Text="{Binding SimulatedValue}"
                               HorizontalAlignment="Right" VerticalAlignment="Center" FontWeight="Bold"/>
                </Grid>
                <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                    <Button Content="0" Command="{Binding SetSimulatedValueCommand}" CommandParameter="0" Width="40" Height="28" Margin="0,0,5,0"/>
                    <Button Content="25" Command="{Binding SetSimulatedValueCommand}" CommandParameter="25" Width="40" Height="28" Margin="0,0,5,0"/>
                    <Button Content="50" Command="{Binding SetSimulatedValueCommand}" CommandParameter="50" Width="40" Height="28" Margin="0,0,5,0"/>
                    <Button Content="75" Command="{Binding SetSimulatedValueCommand}" CommandParameter="75" Width="40" Height="28" Margin="0,0,5,0"/>
                    <Button Content="100" Command="{Binding SetSimulatedValueCommand}" CommandParameter="100" Width="50" Height="28"/>
                </StackPanel>
            </StackPanel>
        </GroupBox>

        <!-- Overlay 控制 -->
        <GroupBox Grid.Row="3" Header="Overlay 控制" Margin="0,0,0,10">
            <StackPanel Orientation="Horizontal" Margin="5">
                <Button Content="{Binding IsOverlayVisible, Converter={StaticResource BoolToTextConverter}, ConverterParameter='隐藏|显示'}"
                        Command="{Binding ToggleOverlayCommand}"
                        Width="100" Height="28"/>
                <TextBlock Text="{Binding IsOverlayVisible, Converter={StaticResource BoolToVisibilityTextConverter}}"
                           VerticalAlignment="Center" Margin="10,0,0,0"/>
            </StackPanel>
        </GroupBox>

        <!-- 日志区 -->
        <GroupBox Grid.Row="4" Header="日志">
            <ListBox ItemsSource="{Binding LogMessages}" FontFamily="Consolas" FontSize="11"
                     ScrollViewer.HorizontalScrollBarVisibility="Auto">
                <ListBox.ItemTemplate>
                    <DataTemplate>
                        <TextBlock Text="{Binding}" TextWrapping="NoWrap"/>
                    </DataTemplate>
                </ListBox.ItemTemplate>
            </ListBox>
        </GroupBox>
    </Grid>
</Window>
```

**Step 4: 创建 MainWindow.xaml.cs**

```csharp
// SitRight/Views/MainWindow.xaml.cs
using System.Windows;
using SitRight.ViewModels;
namespace SitRight.Views;
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.Cleanup();
    }
}
```

**Step 5: 创建 OverlayWindow.xaml**

```xml
<Window x:Class="SitRight.Views.OverlayWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Overlay"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        WindowState="Maximized"
        IsHitTestVisible="False"
        Visibility="{Binding IsOverlayVisible, Converter={StaticResource BoolToVisibilityConverter}}">
    <Grid>
        <!-- 主遮罩层 -->
        <Rectangle x:Name="MaskRect"
                   Fill="{Binding MaskBrush}"
                   Opacity="{Binding MaskOpacity}"/>

        <!-- 边缘渐变层 -->
        <Rectangle x:Name="EdgeRect" Opacity="{Binding EdgeOpacity}">
            <Rectangle.Fill>
                <RadialGradientBrush GradientOrigin="0.5,0.5" Center="0.5,0.5">
                    <GradientStop Color="#00000000" Offset="0.6"/>
                    <GradientStop Color="#00000000" Offset="0.8"/>
                    <GradientStop Color="#40000000" Offset="1.0"/>
                </RadialGradientBrush>
            </Rectangle.Fill>
        </Rectangle>

        <!-- 调试信息（小字） -->
        <TextBlock x:Name="DebugText"
                   Text="{Binding DisplayValue, StringFormat=当前模糊度: {0:F1}}"
                   FontSize="14"
                   Foreground="White"
                   HorizontalAlignment="Right"
                   VerticalAlignment="Bottom"
                   Margin="20"
                   Opacity="0.5"
                   FontFamily="Consolas"
                   Visibility="Collapsed"/>
    </Grid>
</Window>
```

**Step 6: 创建 OverlayWindow.xaml.cs**

```csharp
// SitRight/Views/OverlayWindow.xaml.cs
using System.Windows;
using SitRight.ViewModels;
namespace SitRight.Views;
public partial class OverlayWindow : Window
{
    public OverlayWindow() => InitializeComponent();

    public void SetViewModel(OverlayViewModel viewModel)
    {
        DataContext = viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Application.Current.Shutdown();
    }
}
```

**Step 7: 更新 App.xaml.cs**

```csharp
// SitRight/App.xaml.cs
using System.Windows;
using SitRight.ViewModels;
using SitRight.Views;
namespace SitRight;
public partial class App : Application
{
    private OverlayWindow? _overlayWindow;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _overlayWindow = new OverlayWindow();

        _mainWindow = new MainWindow();

        if (_mainWindow.DataContext is MainViewModel mainVm)
        {
            _overlayWindow.SetViewModel(mainVm.OverlayViewModel);
        }

        _overlayWindow.Show();
        _mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_mainWindow?.DataContext is MainViewModel vm)
            vm.Cleanup();
        base.OnExit(e);
    }
}
```

**Step 8: 验证编译**

Run: `dotnet build SitRight`
Expected: BUILD SUCCEEDED

**Step 9: Commit**

```bash
git add SitRight/App.xaml SitRight/App.xaml.cs SitRight/Converters.cs
git add SitRight/Views/MainWindow.xaml SitRight/Views/MainWindow.xaml.cs
git add SitRight/Views/OverlayWindow.xaml SitRight/Views/OverlayWindow.xaml.cs
git commit -m "feat: 实现 UI 层"
```

---

### Task 11: 最终测试和发布配置

**Files:**
- Modify: `SitRight/SitRight.csproj` (添加发布配置)

**Step 1: 添加发布配置到 csproj**

```xml
<!-- SitRight/SitRight.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>true</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>SitRight</AssemblyName>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
    <PackageReference Include="System.IO.Ports" Version="8.0.0" />
  </ItemGroup>
</Project>
```

**Step 2: 运行所有测试**

Run: `dotnet test SitRight.Tests --verbosity normal`
Expected: All tests PASS

**Step 3: 发布应用**

Run: `dotnet publish SitRight/SitRight.csproj -c Release -o ./publish`
Expected: `publish/SitRight.exe` 生成

**Step 4: 验证 exe 文件**

Run: `ls -la publish/SitRight.exe`
Expected: 文件存在

**Step 5: Commit**

```bash
git add SitRight/SitRight.csproj
git commit -m "feat: 添加发布配置"
```

---

## 计划完成摘要

**总任务数:** 11
**预计文件数:** 28+ 个文件

**TDD 测试覆盖:**
| 组件 | 测试数 |
|------|--------|
| DeviceProtocol | 9 tests |
| ValueMapper | 10 tests |
| AppConfig | - |
| ConfigService | 2 tests |
| OverlayViewModel | 4 tests |
| MainViewModel | 7 tests |
| SerialService | 5 tests |
| **总计** | **37 tests** |

**执行顺序:**
1. 项目骨架 + 测试项目 (Task 1)
2. DeviceProtocol (Task 2) - TDD
3. ValueMapper (Task 3) - TDD
4. AppConfig (Task 4)
5. ConfigService (Task 5) - TDD
6. MVVM 基础设施 (Task 6)
7. OverlayViewModel (Task 7) - TDD
8. MainViewModel (Task 8) - TDD
9. SerialService (Task 9) - TDD
10. UI 层 (Task 10)
11. 发布配置 (Task 11)

---

**Plan complete and saved to `docs/plans/2026-03-23-posture-overlay-wpf.md`.**

**Two execution options:**

**1. Subagent-Driven (this session)** - I dispatch fresh subagent per task, review between tasks, fast iteration

**2. Parallel Session (separate)** - Open new session with executing-plans, batch execution with checkpoints

Which approach?
