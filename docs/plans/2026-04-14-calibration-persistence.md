# Calibration & Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 SitRight 补上“姿势校准 + 本地持久化 + 平滑恢复”链路，解决“每次启动都要重新校准”“点击校准后屏幕仍持续模糊”“重启后没有记忆功能”的问题，并让这条链路可以用 mock 数据做白盒验证。

**Architecture:** 保持当前 WPF code-behind 结构，不做额外 MVVM 重构。最小增量引入 `ConfigService` 负责 `config.json` 读写，`CalibrationService` 负责基线记录和原始值归一化，`BlurController` 负责平滑插值与 `Reset()`。主窗口输入链路调整为 `rawValue -> Normalize(raw, baseline) -> BlurController -> OverlayState.FromDisplayLevel(...) -> OverlayWindow`，校准时先持久化基线，再重置平滑状态并重新推入当前值，确保模糊立即恢复。

**Tech Stack:** .NET 8.0 / WPF / C# / System.Text.Json / xUnit

**Root Cause This Plan Addresses:**
- 当前源码没有校准字段，`AppConfig` 无法保存基线与校准时间。
- 当前源码没有配置持久化服务，启动时不会加载、运行中不会保存校准结果。
- 当前源码没有平滑控制器，校准之后旧的高模糊状态无法被立即清空。
- 当前 `OverlayState.FromDisplayLevel()` 丢失了提示文案映射，现有测试套件本身是红灯。

---

## File Structure

| File | Action | Responsibility |
|------|--------|---------------|
| `SitRight/Models/AppConfig.cs` | Modify | 增加校准持久化字段 |
| `SitRight/Services/ConfigService.cs` | Create | 读写 `config.json` |
| `SitRight/Services/CalibrationService.cs` | Create | 记录校准基线，归一化原始值 |
| `SitRight/Services/BlurController.cs` | Create | 平滑插值与 `Reset()` |
| `SitRight/Models/OverlayState.cs` | Modify | 恢复提示文案与透明度映射 |
| `SitRight/MainWindow.xaml` | Modify | 增加校准 UI |
| `SitRight/MainWindow.xaml.cs` | Modify | 串接配置、校准、平滑和 Overlay |
| `SitRight.Tests/AppConfigTests.cs` | Modify | 覆盖校准字段默认值 |
| `SitRight.Tests/ConfigServiceTests.cs` | Create | 持久化往返测试 |
| `SitRight.Tests/CalibrationServiceTests.cs` | Create | 基线归一化白盒测试 |
| `SitRight.Tests/BlurControllerTests.cs` | Create | 平滑与重置白盒测试 |
| `SitRight.Tests/OverlayStateTests.cs` | Modify | 修复 Overlay 契约测试 |

---

## Task 1: Persist Calibration Fields In AppConfig And config.json

**Files:**
- Modify: `SitRight/Models/AppConfig.cs`
- Modify: `SitRight.Tests/AppConfigTests.cs`
- Create: `SitRight/Services/ConfigService.cs`
- Create: `SitRight.Tests/ConfigServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `SitRight.Tests/ConfigServiceTests.cs`:

```csharp
using SitRight.Models;
using SitRight.Services;

namespace SitRight.Services;

public sealed class ConfigServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly ConfigService _service;

    public ConfigServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"sitright-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _service = new ConfigService(_testDir);
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaultsAndCreatesFile()
    {
        var config = _service.Load();

        Assert.Equal("COM1", config.DefaultComPort);
        Assert.Equal(0, config.CalibrationBaseline);
        Assert.Null(config.CalibratedAt);
        Assert.True(File.Exists(Path.Combine(_testDir, "config.json")));
    }

    [Fact]
    public void Save_ThenLoad_PreservesCalibrationFields()
    {
        var now = new DateTime(2026, 4, 14, 21, 30, 0, DateTimeKind.Local);
        _service.Save(new AppConfig
        {
            CalibrationBaseline = 42,
            CalibratedAt = now
        });

        var reloaded = new ConfigService(_testDir).Load();

        Assert.Equal(42, reloaded.CalibrationBaseline);
        Assert.Equal(now, reloaded.CalibratedAt);
    }

    [Fact]
    public void Update_ModifiesPersistedConfig()
    {
        _service.Load();
        _service.Update(config => config.CalibrationBaseline = 77);

        var reloaded = new ConfigService(_testDir).Load();

        Assert.Equal(77, reloaded.CalibrationBaseline);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }
}
```

Modify `SitRight.Tests/AppConfigTests.cs`:

```csharp
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
        Assert.Equal(33, config.DisplayRefreshIntervalMs);
        Assert.Equal(0.18, config.SmoothingAlpha);
        Assert.Equal(0.70, config.MaxMaskOpacity);
        Assert.Equal(30, config.HintStartLevel);
        Assert.Equal(80, config.UrgentLevel);
        Assert.Equal(0, config.CalibrationBaseline);
        Assert.Null(config.CalibratedAt);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test SitRight.Tests/SitRight.Tests.csproj --filter "FullyQualifiedName~ConfigServiceTests|FullyQualifiedName~AppConfigTests" --verbosity normal`

Expected:
- `ConfigService` type not found
- `AppConfig` missing `CalibrationBaseline`

- [ ] **Step 3: Add calibration fields and ConfigService**

Modify `SitRight/Models/AppConfig.cs`:

```csharp
using System;

namespace SitRight.Models;

public class AppConfig
{
    public string DefaultComPort { get; set; } = "COM1";
    public int BaudRate { get; set; } = 115200;
    public int TimeoutThresholdMs { get; set; } = 2000;
    public int DisplayRefreshIntervalMs { get; set; } = 33;
    public double SmoothingAlpha { get; set; } = 0.18;
    public double MaxMaskOpacity { get; set; } = 0.70;
    public int HintStartLevel { get; set; } = 30;
    public int UrgentLevel { get; set; } = 80;
    public int CalibrationBaseline { get; set; } = 0;
    public DateTime? CalibratedAt { get; set; }
}
```

Create `SitRight/Services/ConfigService.cs`:

```csharp
using System;
using System.IO;
using System.Text.Json;
using SitRight.Models;

namespace SitRight.Services;

public sealed class ConfigService
{
    private readonly string _configPath;
    private AppConfig? _cachedConfig;

    public ConfigService(string? configDir = null)
    {
        var baseDir = configDir ?? AppDomain.CurrentDomain.BaseDirectory;
        _configPath = Path.Combine(baseDir, "config.json");
    }

    public AppConfig Load()
    {
        if (_cachedConfig is not null)
        {
            return _cachedConfig;
        }

        if (!File.Exists(_configPath))
        {
            _cachedConfig = new AppConfig();
            Save(_cachedConfig);
            return _cachedConfig;
        }

        var json = File.ReadAllText(_configPath);
        _cachedConfig = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        return _cachedConfig;
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
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

- [ ] **Step 4: Run the targeted tests and verify they pass**

Run: `dotnet test SitRight.Tests/SitRight.Tests.csproj --filter "FullyQualifiedName~ConfigServiceTests|FullyQualifiedName~AppConfigTests" --verbosity normal`

Expected:
- All `ConfigServiceTests` pass
- `AppConfigTests` pass

- [ ] **Step 5: Commit**

```bash
git add SitRight/Models/AppConfig.cs SitRight/Services/ConfigService.cs SitRight.Tests/AppConfigTests.cs SitRight.Tests/ConfigServiceTests.cs
git commit -m "feat: persist calibration baseline in config"
```

---

## Task 2: Add CalibrationService For White-Box Baseline Tests

**Files:**
- Create: `SitRight/Services/CalibrationService.cs`
- Create: `SitRight.Tests/CalibrationServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `SitRight.Tests/CalibrationServiceTests.cs`:

```csharp
using SitRight.Models;
using SitRight.Services;

namespace SitRight.Services;

public class CalibrationServiceTests
{
    [Fact]
    public void Normalize_WhenNoBaseline_ReturnsRawValue()
    {
        var service = new CalibrationService();

        Assert.Equal(55, service.Normalize(rawValue: 55, baseline: 0));
    }

    [Fact]
    public void Normalize_WhenRawBelowBaseline_ReturnsZero()
    {
        var service = new CalibrationService();

        Assert.Equal(0, service.Normalize(rawValue: 40, baseline: 60));
    }

    [Fact]
    public void Normalize_WhenRawAboveBaseline_ReturnsDelta()
    {
        var service = new CalibrationService();

        Assert.Equal(13, service.Normalize(rawValue: 73, baseline: 60));
    }

    [Fact]
    public void ApplyCalibration_WritesBaselineAndTimestamp()
    {
        var service = new CalibrationService();
        var config = new AppConfig();
        var now = new DateTime(2026, 4, 14, 22, 15, 0, DateTimeKind.Local);

        service.ApplyCalibration(config, rawValue: 61, calibratedAt: now);

        Assert.Equal(61, config.CalibrationBaseline);
        Assert.Equal(now, config.CalibratedAt);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test SitRight.Tests/SitRight.Tests.csproj --filter "FullyQualifiedName~CalibrationServiceTests" --verbosity normal`

Expected:
- `CalibrationService` type not found

- [ ] **Step 3: Implement CalibrationService**

Create `SitRight/Services/CalibrationService.cs`:

```csharp
using System;
using SitRight.Models;

namespace SitRight.Services;

public sealed class CalibrationService
{
    public int Normalize(int rawValue, int baseline)
    {
        if (baseline <= 0)
        {
            return Math.Clamp(rawValue, 0, 100);
        }

        return Math.Clamp(rawValue - baseline, 0, 100);
    }

    public void ApplyCalibration(AppConfig config, int rawValue, DateTime calibratedAt)
    {
        config.CalibrationBaseline = Math.Clamp(rawValue, 0, 100);
        config.CalibratedAt = calibratedAt;
    }
}
```

- [ ] **Step 4: Run the targeted tests and verify they pass**

Run: `dotnet test SitRight.Tests/SitRight.Tests.csproj --filter "FullyQualifiedName~CalibrationServiceTests" --verbosity normal`

Expected:
- All `CalibrationServiceTests` pass

- [ ] **Step 5: Commit**

```bash
git add SitRight/Services/CalibrationService.cs SitRight.Tests/CalibrationServiceTests.cs
git commit -m "feat: add calibration normalization service"
```

---

## Task 3: Add BlurController For Smooth Recovery And Reset

**Files:**
- Create: `SitRight/Services/BlurController.cs`
- Create: `SitRight.Tests/BlurControllerTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `SitRight.Tests/BlurControllerTests.cs`:

```csharp
using SitRight.Services;

namespace SitRight.Services;

public class BlurControllerTests
{
    [Fact]
    public void InitialState_IsZero()
    {
        var controller = new BlurController();
        Assert.Equal(0, controller.RawValue);
        Assert.Equal(0, controller.TargetValue);
        Assert.Equal(0, controller.DisplayValue);
    }

    [Fact]
    public void PushRawValue_UpdatesRawAndTarget()
    {
        var controller = new BlurController();

        controller.PushRawValue(80);

        Assert.Equal(80, controller.RawValue);
        Assert.Equal(80, controller.TargetValue);
        Assert.Equal(0, controller.DisplayValue);
    }

    [Fact]
    public void Tick_WithAlphaOne_JumpsToTarget()
    {
        var controller = new BlurController(alpha: 1.0);
        controller.PushRawValue(65);

        controller.Tick();

        Assert.Equal(65, controller.DisplayValue, 3);
    }

    [Fact]
    public void Reset_ClearsResidualBlurImmediately()
    {
        var controller = new BlurController(alpha: 1.0);
        controller.PushRawValue(100);
        controller.Tick();

        controller.Reset();

        Assert.Equal(0, controller.RawValue);
        Assert.Equal(0, controller.TargetValue);
        Assert.Equal(0, controller.DisplayValue);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test SitRight.Tests/SitRight.Tests.csproj --filter "FullyQualifiedName~BlurControllerTests" --verbosity normal`

Expected:
- `BlurController` type not found

- [ ] **Step 3: Implement BlurController**

Create `SitRight/Services/BlurController.cs`:

```csharp
using System;

namespace SitRight.Services;

public sealed class BlurController
{
    private readonly double _alpha;
    private readonly double _snapThreshold;

    public event Action<double>? DisplayValueChanged;

    public double RawValue { get; private set; }
    public double TargetValue { get; private set; }
    public double DisplayValue { get; private set; }

    public BlurController(double alpha = 0.18, double snapThreshold = 0.01)
    {
        _alpha = alpha;
        _snapThreshold = snapThreshold;
    }

    public void PushRawValue(int value)
    {
        RawValue = value;
        TargetValue = value;
    }

    public void Tick()
    {
        DisplayValue += (TargetValue - DisplayValue) * _alpha;

        if (Math.Abs(TargetValue - DisplayValue) < _snapThreshold)
        {
            DisplayValue = TargetValue;
        }

        if (Math.Abs(DisplayValue) < _snapThreshold)
        {
            DisplayValue = 0;
        }

        DisplayValueChanged?.Invoke(DisplayValue);
    }

    public void Reset()
    {
        RawValue = 0;
        TargetValue = 0;
        DisplayValue = 0;
        DisplayValueChanged?.Invoke(DisplayValue);
    }
}
```

- [ ] **Step 4: Run the targeted tests and verify they pass**

Run: `dotnet test SitRight.Tests/SitRight.Tests.csproj --filter "FullyQualifiedName~BlurControllerTests" --verbosity normal`

Expected:
- All `BlurControllerTests` pass

- [ ] **Step 5: Commit**

```bash
git add SitRight/Services/BlurController.cs SitRight.Tests/BlurControllerTests.cs
git commit -m "feat: add blur controller for smooth recovery"
```

---

## Task 4: Restore OverlayState Mapping Contract

**Files:**
- Modify: `SitRight/Models/OverlayState.cs`
- Modify: `SitRight.Tests/OverlayStateTests.cs`

- [ ] **Step 1: Write the updated failing tests**

Modify `SitRight.Tests/OverlayStateTests.cs`:

```csharp
using SitRight.Models;

namespace SitRight.Models;

public class OverlayStateTests
{
    [Fact]
    public void FromDisplayLevel_LevelZero_ReturnsNoMessage()
    {
        var state = OverlayState.FromDisplayLevel(0);

        Assert.Equal(string.Empty, state.MessageText);
        Assert.Equal(0, state.MessageOpacity);
        Assert.Equal(0, state.SeverityLevel);
    }

    [Fact]
    public void FromDisplayLevel_HintThreshold_ShowsReminder()
    {
        var state = OverlayState.FromDisplayLevel(35, hintStart: 30, urgentLevel: 80);

        Assert.Equal("请调整坐姿", state.MessageText);
        Assert.True(state.MessageOpacity > 0);
    }

    [Fact]
    public void FromDisplayLevel_UrgentThreshold_ShowsUrgentReminder()
    {
        var state = OverlayState.FromDisplayLevel(85, hintStart: 30, urgentLevel: 80);

        Assert.Equal("请立即调整坐姿！", state.MessageText);
        Assert.True(state.MessageOpacity > 0.9);
        Assert.Equal(3, state.SeverityLevel);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test SitRight.Tests/SitRight.Tests.csproj --filter "FullyQualifiedName~OverlayStateTests" --verbosity normal`

Expected:
- Existing `OverlayState` implementation cannot satisfy message assertions

- [ ] **Step 3: Restore the mapping implementation**

Modify `SitRight/Models/OverlayState.cs`:

```csharp
using System;

namespace SitRight.Models;

public class OverlayState
{
    public double MaskOpacity { get; set; }
    public string MaskColor { get; set; } = "#FFFFFF";
    public double EdgeOpacity { get; set; }
    public string MessageText { get; set; } = string.Empty;
    public double MessageOpacity { get; set; }
    public int SeverityLevel { get; set; }
    public bool BlockInput { get; set; }

    public static OverlayState FromDisplayLevel(double level, int hintStart = 30, int urgentLevel = 80)
    {
        level = Math.Clamp(level, 0, 100);
        var normalized = level / 100.0;
        var maskOpacity = level > 95 ? 1.0 : 0.02 + Math.Pow(normalized, 2.8) * 0.98;
        var edgeOpacity = Math.Pow(normalized, 1.8) * 0.25;
        var message = level < hintStart ? string.Empty : level < urgentLevel ? "请调整坐姿" : "请立即调整坐姿！";
        var messageOpacity = level <= hintStart ? 0 : Math.Min(1.0, (level - hintStart) / 40.0);
        var severity = level switch { <= 20 => 0, <= 50 => 1, <= 79 => 2, _ => 3 };

        return new OverlayState
        {
            MaskOpacity = Math.Clamp(maskOpacity, 0, 1),
            MaskColor = level < 30 ? "#FFFFFF" : level < 60 ? "#E0E0E0" : level < 80 ? "#BDBDBD" : "#9E9E9E",
            EdgeOpacity = Math.Clamp(edgeOpacity, 0, 1),
            MessageText = message,
            MessageOpacity = Math.Clamp(messageOpacity, 0, 1),
            SeverityLevel = severity,
            BlockInput = false
        };
    }
}
```

- [ ] **Step 4: Run the targeted tests and verify they pass**

Run: `dotnet test SitRight.Tests/SitRight.Tests.csproj --filter "FullyQualifiedName~OverlayStateTests" --verbosity normal`

Expected:
- All `OverlayStateTests` pass

- [ ] **Step 5: Commit**

```bash
git add SitRight/Models/OverlayState.cs SitRight.Tests/OverlayStateTests.cs
git commit -m "fix: restore overlay message mapping contract"
```

---

## Task 5: Integrate Calibration, Persistence, And Smoothing Into MainWindow

**Files:**
- Modify: `SitRight/MainWindow.xaml`
- Modify: `SitRight/MainWindow.xaml.cs`

**Notes:**
- 保持当前 code-behind 结构，不额外引入 MVVM。
- 当前仓库没有串口输入链路，校准按钮默认用“当前模拟值”作为校准基线；未来接串口后复用 `_lastRawValue` 即可。
- 这一步主要用编译 + 手工回归验证，自动化验证由 Task 1-4 的服务测试兜底。

- [ ] **Step 1: Update the layout**

Modify `SitRight/MainWindow.xaml`:

```xml
<Window x:Class="SitRight.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="SitRight - 坐姿矫正仪"
        Width="520" Height="520"
        WindowStartupLocation="CenterScreen">
    <Grid Margin="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- 保留原有串口连接、设备状态、模拟模式 -->

        <GroupBox Grid.Row="3" Header="姿势校准" Margin="0,0,0,12">
            <Grid Margin="8">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <TextBlock Grid.Row="0" Grid.Column="0" Text="当前基线" Margin="0,0,12,4"/>
                <TextBlock Grid.Row="0" Grid.Column="1" x:Name="CalibrationBaselineText" Text="0"/>
                <TextBlock Grid.Row="1" Grid.Column="0" Text="最近校准" Margin="0,0,12,8"/>
                <TextBlock Grid.Row="1" Grid.Column="1" x:Name="CalibrationTimeText" Text="未校准"/>
                <Button Grid.Row="2"
                        Grid.ColumnSpan="2"
                        x:Name="CalibrateButton"
                        Content="校准当前姿势"
                        Height="32"
                        Click="CalibrateButton_Click"/>
            </Grid>
        </GroupBox>
    </Grid>
</Window>
```

- [ ] **Step 2: Wire the services into MainWindow**

Modify `SitRight/MainWindow.xaml.cs`:

```csharp
using System;
using System.Windows;
using System.Windows.Threading;
using SitRight.Models;
using SitRight.Services;

namespace SitRight;

public partial class MainWindow : Window
{
    private bool _isSimulationMode;
    private readonly OverlayWindow _overlay;
    private readonly ConfigService _configService;
    private readonly CalibrationService _calibrationService;
    private readonly BlurController _blurController;
    private readonly DispatcherTimer _displayTimer;

    private AppConfig _config;
    private int _lastRawValue;

    public MainWindow()
    {
        InitializeComponent();

        _configService = new ConfigService();
        _calibrationService = new CalibrationService();
        _config = _configService.Load();
        _blurController = new BlurController(alpha: _config.SmoothingAlpha);
        _blurController.DisplayValueChanged += OnDisplayValueChanged;

        _displayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_config.DisplayRefreshIntervalMs)
        };
        _displayTimer.Tick += (_, _) => _blurController.Tick();
        _displayTimer.Start();

        _overlay = new OverlayWindow();
        _overlay.Show();

        UpdateCalibrationInfo();
        Log("应用程序已启动");
    }

    private void CalibrateButton_Click(object sender, RoutedEventArgs e)
    {
        var baselineSource = _isSimulationMode ? (int)SimulatedValueSlider.Value : _lastRawValue;

        _calibrationService.ApplyCalibration(_config, baselineSource, DateTime.Now);
        _configService.Save(_config);
        UpdateCalibrationInfo();

        // 关键修复：先清空旧的平滑残留，再重新推入当前值。
        _blurController.Reset();
        PushRawValue(baselineSource);
    }

    private void PushRawValue(int rawValue)
    {
        _lastRawValue = rawValue;
        var normalized = _calibrationService.Normalize(rawValue, _config.CalibrationBaseline);
        _blurController.PushRawValue(normalized);
    }

    private void OnDisplayValueChanged(double displayValue)
    {
        Dispatcher.Invoke(() =>
        {
            DisplayValueText.Text = displayValue.ToString("F1");
            _overlay.ApplyState(OverlayState.FromDisplayLevel(
                displayValue,
                _config.HintStartLevel,
                _config.UrgentLevel));
        });
    }

    private void UpdateCalibrationInfo()
    {
        CalibrationBaselineText.Text = _config.CalibrationBaseline.ToString();
        CalibrationTimeText.Text = _config.CalibratedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未校准";
    }
}
```

- [ ] **Step 3: Build and run the full test suite**

Run: `dotnet build SitRight/SitRight.csproj`

Expected:
- BUILD SUCCEEDED

Run: `dotnet test SitRight.Tests/SitRight.Tests.csproj --verbosity normal`

Expected:
- All tests PASS

- [ ] **Step 4: Manual regression verification**

Run: `dotnet run --project SitRight/SitRight.csproj`

Verify:
1. 勾选“启用模拟”，把滑杆拖到 `70`，屏幕出现明显遮罩。
2. 点击“校准当前姿势”，遮罩应立即清空或快速归零。
3. 保持基线不变，把滑杆从 `70` 调到 `75`，模糊应只反映差值 `5`。
4. 关闭应用，再次启动，基线值和最近校准时间仍显示上次结果。
5. 重启后把滑杆重新调回已保存基线值，遮罩应保持接近 `0`。

- [ ] **Step 5: Commit**

```bash
git add SitRight/MainWindow.xaml SitRight/MainWindow.xaml.cs
git commit -m "feat: integrate calibration persistence into main window"
```

---

## Task 6: Final Verification

**Files:**
- Read: `SitRight/Models/AppConfig.cs`
- Read: `SitRight/Services/ConfigService.cs`
- Read: `SitRight/Services/CalibrationService.cs`
- Read: `SitRight/Services/BlurController.cs`
- Read: `SitRight/Models/OverlayState.cs`
- Read: `SitRight/MainWindow.xaml`
- Read: `SitRight/MainWindow.xaml.cs`

- [ ] **Step 1: Run the full automated suite**

Run: `dotnet test SitRight.Tests/SitRight.Tests.csproj --verbosity normal`

Expected:
- All tests PASS

- [ ] **Step 2: Check generated config**

Inspect: `SitRight/bin/Debug/net8.0-windows/config.json`

Expected example:

```json
{
  "defaultComPort": "COM1",
  "baudRate": 115200,
  "timeoutThresholdMs": 2000,
  "displayRefreshIntervalMs": 33,
  "smoothingAlpha": 0.18,
  "maxMaskOpacity": 0.7,
  "hintStartLevel": 30,
  "urgentLevel": 80,
  "calibrationBaseline": 70,
  "calibratedAt": "2026-04-14T22:15:00+08:00"
}
```

- [ ] **Step 3: Commit the final documentation checkpoint**

```bash
git add docs/plans/2026-04-14-calibration-persistence.md
git commit -m "docs: finalize calibration persistence plan"
```

---

## Self-Review Checklist

- 是否覆盖“持久化、校准后立即恢复、启动加载记忆”三条主诉求：是。
- 是否覆盖当前已存在的测试红灯：是，Task 4 先修复 `OverlayState` 契约。
- 是否引入了不必要的新框架：否。
- 是否给出了白盒测试、运行命令、手工回归路径：是。
