# BlurController 业务逻辑层实现计划

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.
>
> **TDD Required:** 所有实现必须遵循 Red-Green-Refactor 循环：
> 1. 先写测试，运行验证 FAIL
> 2. 再写实现，运行验证 PASS
> 3. 重构代码

**Goal:** 实现 BlurController 平滑控制器、ValueMapper 数值映射器、ConfigService 配置服务

**Architecture:**
- BlurController 维护 RawValue、TargetValue、DisplayValue 三级状态，通过定时器平滑插值（第5章核心数据流、第11章 BlurController 说明）
- ValueMapper 将 DisplayValue 映射为 OverlayState 视觉参数（第8章 Overlay设计、第11章 ValueMapper说明）
- ConfigService 处理 JSON 配置文件的读写（第16章 配置与可扩展性）

**Tech Stack:** .NET 8.0 / WPF / System.Text.Json / xUnit

**对应完整计划章节:** 第5章 核心数据流、第8章 Overlay设计、第11章 关键模块说明、第12章 推荐参数默认值、第16章 配置与可扩展性

---

## 任务0: 验证 Model 类存在

**Files:**
- Read: `PostureOverlayApp/Models/OverlayState.cs`
- Read: `PostureOverlayApp/Models/AppConfig.cs`

**Step 1: 验证项目结构**

```bash
cd PostureOverlayApp
dotnet build
```
Expected: BUILD SUCCEEDED

**Step 2: 提交**

```bash
git commit -m "chore: 验证 Model 类可用"
```

---

## 任务1: BlurController 平滑控制器

**Files:**
- Create: `PostureOverlayApp/Services/BlurController.cs`
- Create: `PostureOverlayApp/Services/BlurControllerTests.cs`

**对应完整计划章节:** 第5章 核心数据流、第11章 BlurController 说明、第12章 推荐参数默认值

**TDD Step 1: 编写测试（RED）**

```csharp
using Xunit;
using PostureOverlayApp.Services;

namespace PostureOverlayApp.Services;

public class BlurControllerTests
{
    [Fact]
    public void InitialDisplayValue_IsZero()
    {
        var controller = new BlurController();
        Assert.Equal(0, controller.DisplayValue);
    }

    [Fact]
    public void InitialTargetValue_IsZero()
    {
        var controller = new BlurController();
        Assert.Equal(0, controller.TargetValue);
    }

    [Fact]
    public void PushRawValue_UpdatesTargetValue()
    {
        var controller = new BlurController();
        controller.PushRawValue(50);
        Assert.Equal(50, controller.TargetValue);
    }

    [Fact]
    public void PushRawValue_UpdatesRawValue()
    {
        var controller = new BlurController();
        controller.PushRawValue(37);
        Assert.Equal(37, controller.RawValue);
    }

    [Fact]
    public void PushRawValue_DoesNotImmediatelyUpdateDisplayValue()
    {
        var controller = new BlurController();
        controller.PushRawValue(100);
        // Display value should still be 0 until Tick() is called
        Assert.Equal(0, controller.DisplayValue);
    }

    [Fact]
    public void Tick_MovesDisplayValueTowardTarget()
    {
        var controller = new BlurController(alpha: 1.0); // alpha=1 for immediate
        controller.PushRawValue(100);
        controller.Tick();
        Assert.Equal(100, controller.DisplayValue);
    }

    [Fact]
    public void Tick_WithPartialAlpha_MovesPartially()
    {
        var controller = new BlurController(alpha: 0.5);
        controller.PushRawValue(100);
        controller.Tick();
        // With alpha=0.5, display should move halfway: 0 + (100-0)*0.5 = 50
        Assert.Equal(50, controller.DisplayValue);
    }

    [Fact]
    public void Tick_MultipleCalls_ConvergesToTarget()
    {
        var controller = new BlurController(alpha: 0.18); // Recommended default
        controller.PushRawValue(100);

        // Simulate multiple ticks
        for (int i = 0; i < 10; i++)
        {
            controller.Tick();
        }

        // Should be very close to 100
        Assert.True(controller.DisplayValue > 90);
    }

    [Fact]
    public void Tick_SmallValueBelowThreshold_ConvergesToZero()
    {
        var controller = new BlurController(smallValueThreshold: 5);
        controller.PushRawValue(3);

        // After ticks, should converge to 0
        controller.Tick();

        // Small values should quickly converge to zero
        Assert.True(controller.DisplayValue < 1);
    }

    [Fact]
    public void Reset_SetsAllValuesToZero()
    {
        var controller = new BlurController();
        controller.PushRawValue(50);
        controller.Tick(); // Move toward target

        controller.Reset();

        Assert.Equal(0, controller.RawValue);
        Assert.Equal(0, controller.TargetValue);
        Assert.Equal(0, controller.DisplayValue);
    }

    [Fact]
    public void OnDisplayValueChanged_EventIsRaisedOnTick()
    {
        var controller = new BlurController(alpha: 1.0);
        var changedValues = new List<double>();

        controller.OnDisplayValueChanged += value => changedValues.Add(value);
        controller.PushRawValue(50);
        controller.Tick();

        Assert.Contains(50, changedValues);
    }

    [Fact]
    public void Tick_DoesNotThrow()
    {
        var controller = new BlurController();
        controller.PushRawValue(50);
        var exception = Record.Exception(() => controller.Tick());
        Assert.Null(exception);
    }
}
```

**Step 2: 运行测试（RED）**

```bash
cd PostureOverlayApp
dotnet test --filter "FullyQualifiedName~BlurControllerTests"
```
Expected: FAIL - BlurController not found

**TDD Step 3: 实现 BlurController（GREEN）**

```csharp
namespace PostureOverlayApp.Services;

/// <summary>
/// BlurController：第5章核心数据流、第11章关键模块说明
/// 维护 RawValue、TargetValue、DisplayValue 三级状态
/// 使用指数滑动平均实现平滑过渡
/// </summary>
public class BlurController
{
    private readonly double _alpha;
    private readonly double _smallValueThreshold;
    private readonly double _convergeSpeed;

    public event Action<double>? OnDisplayValueChanged;

    public double RawValue { get; private set; }
    public double TargetValue { get; private set; }
    public double DisplayValue { get; private set; }

    /// <summary>
    /// 创建 BlurController
    /// </summary>
    /// <param name="alpha">平滑系数，推荐 0.12~0.25，参见第12章推荐参数默认值</param>
    /// <param name="smallValueThreshold">低于此值视为接近零，默认 5</param>
    /// <param name="convergeSpeed">小值收敛速度</param>
    public BlurController(
        double alpha = 0.18,
        double smallValueThreshold = 5,
        double convergeSpeed = 0.1)
    {
        _alpha = alpha;
        _smallValueThreshold = smallValueThreshold;
        _convergeSpeed = convergeSpeed;
    }

    /// <summary>
    /// 接收原始值，更新目标值
    /// </summary>
    public void PushRawValue(int value)
    {
        RawValue = value;
        TargetValue = value;
    }

    /// <summary>
    /// 执行一次平滑插值 - 应由定时器驱动，每 16~33ms 调用一次
    /// </summary>
    public void Tick()
    {
        if (TargetValue < _smallValueThreshold)
        {
            // 小值快速收敛到零，防止尾巴拖太长（参见第11章说明）
            DisplayValue = Lerp(DisplayValue, 0, _convergeSpeed * 3);
        }
        else
        {
            DisplayValue = Lerp(DisplayValue, TargetValue, _alpha);
        }

        // 非常接近零时直接归零
        if (Math.Abs(DisplayValue) < 0.01)
            DisplayValue = 0;

        OnDisplayValueChanged?.Invoke(DisplayValue);
    }

    public void Reset()
    {
        RawValue = 0;
        TargetValue = 0;
        DisplayValue = 0;
        OnDisplayValueChanged?.Invoke(DisplayValue);
    }

    private double Lerp(double current, double target, double alpha)
    {
        return current + (target - current) * alpha;
    }
}
```

**Step 4: 运行测试（GREEN）**

```bash
dotnet test --filter "FullyQualifiedName~BlurControllerTests"
```
Expected: PASS

**Step 5: 提交**

```bash
git add PostureOverlayApp/Services/BlurController.cs PostureOverlayApp/Services/BlurControllerTests.cs
git commit -m "feat: 实现 BlurController 平滑控制器 (TDD)"
```

---

## 任务2: ValueMapper 数值映射器

**Files:**
- Create: `PostureOverlayApp/Services/ValueMapper.cs`
- Create: `PostureOverlayApp/Services/ValueMapperTests.cs`

**对应完整计划章节:** 第8章 Overlay设计、第11章 ValueMapper说明

**TDD Step 1: 编写测试（RED）**

```csharp
using Xunit;
using PostureOverlayApp.Models;
using PostureOverlayApp.Services;

namespace PostureOverlayApp.Services;

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
        Assert.Equal(string.Empty, state.MessageText);
    }

    [Fact]
    public void Map_Level100_ReturnsMaxMask()
    {
        var state = _mapper.Map(100);
        Assert.True(state.MaskOpacity > 0.6);
        Assert.NotEmpty(state.MessageText);
    }

    [Fact]
    public void Map_Level50_ReturnsModerateMask()
    {
        var state = _mapper.Map(50);
        Assert.True(state.MaskOpacity > 0.2);
        Assert.True(state.MaskOpacity < 0.5);
        Assert.Equal("请调整坐姿", state.MessageText);
    }

    [Fact]
    public void Map_LevelBelowHintStart_NoMessage()
    {
        var state = _mapper.Map(20);
        Assert.Equal(string.Empty, state.MessageText);
        Assert.Equal(0, state.MessageOpacity);
    }

    [Fact]
    public void Map_LevelAtHintStart_BeginsShowingMessage()
    {
        var state = _mapper.Map(30);
        Assert.NotEmpty(state.MessageText);
        Assert.True(state.MessageOpacity > 0);
    }

    [Fact]
    public void Map_LevelBelowUrgent_ShowsNormalMessage()
    {
        var state = _mapper.Map(60);
        Assert.Equal("请调整坐姿", state.MessageText);
        Assert.Equal(2, state.SeverityLevel);
    }

    [Fact]
    public void Map_LevelAboveUrgent_ShowsUrgentMessage()
    {
        var state = _mapper.Map(90);
        Assert.Equal("请立即调整坐姿！", state.MessageText);
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

        // Edge opacity should increase non-linearly
        Assert.True(state2.EdgeOpacity > state1.EdgeOpacity);
        // The ratio should be more than linear (pow > 1)
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

**Step 2: 运行测试（RED）**

```bash
dotnet test --filter "FullyQualifiedName~ValueMapperTests"
```
Expected: FAIL - ValueMapper not found

**TDD Step 3: 实现 ValueMapper（GREEN）**

```csharp
using PostureOverlayApp.Models;

namespace PostureOverlayApp.Services;

/// <summary>
/// ValueMapper：将 DisplayLevel 映射为 OverlayState 视觉参数
/// 对应第8章 Overlay设计、第11章 ValueMapper说明
/// </summary>
public class ValueMapper
{
    private readonly int _hintStartLevel;
    private readonly int _urgentLevel;

    public ValueMapper(int hintStartLevel = 30, int urgentLevel = 80)
    {
        _hintStartLevel = hintStartLevel;
        _urgentLevel = urgentLevel;
    }

    /// <summary>
    /// 将显示值映射为 Overlay 视觉状态
    /// </summary>
    /// <param name="displayLevel">0~100 的显示值</param>
    /// <returns>OverlayState 视觉参数</returns>
    public OverlayState Map(double displayLevel)
    {
        return OverlayState.FromDisplayLevel(displayLevel, _hintStartLevel, _urgentLevel);
    }
}
```

**Step 4: 运行测试（GREEN）**

```bash
dotnet test --filter "FullyQualifiedName~ValueMapperTests"
```
Expected: PASS

**Step 5: 提交**

```bash
git add PostureOverlayApp/Services/ValueMapper.cs PostureOverlayApp/Services/ValueMapperTests.cs
git commit -m "feat: 实现 ValueMapper 数值映射器 (TDD)"
```

---

## 任务3: ConfigService 配置服务

**Files:**
- Create: `PostureOverlayApp/Services/ConfigService.cs`
- Create: `PostureOverlayApp/Services/ConfigServiceTests.cs`
- Create: `PostureOverlayApp/config.json`

**对应完整计划章节:** 第16章 配置与可扩展性

**TDD Step 1: 编写测试（RED）**

```csharp
using Xunit;
using PostureOverlayApp.Services;
using PostureOverlayApp.Models;
using System.IO;

namespace PostureOverlayApp.Services;

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
            BaudRate = 9600,
            SmoothingAlpha = 0.25
        };
        _service.Save(initialConfig);

        // Create new service instance to load from disk
        var newService = new ConfigService(_testPath);
        var loaded = newService.Load();

        Assert.Equal("COM5", loaded.DefaultComPort);
        Assert.Equal(9600, loaded.BaudRate);
        Assert.Equal(0.25, loaded.SmoothingAlpha);
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
        _service.Update(c => c.SmoothingAlpha = 0.5);

        var reloaded = _service.Load();
        Assert.Equal(0.5, reloaded.SmoothingAlpha);
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
        _service.Save(new AppConfig { SmoothingAlpha = 0.9 });
        var config2 = _service.Load();

        Assert.NotSame(config1, config2);
        Assert.Equal(0.9, config2.SmoothingAlpha);
    }

    public void Dispose()
    {
        if (File.Exists(_testPath))
            File.Delete(_testPath);
    }
}
```

**Step 2: 运行测试（RED）**

```bash
dotnet test --filter "FullyQualifiedName~ConfigServiceTests"
```
Expected: FAIL - ConfigService not found

**TDD Step 3: 实现 ConfigService（GREEN）**

```csharp
using System.IO;
using System.Text.Json;
using PostureOverlayApp.Models;

namespace PostureOverlayApp.Services;

/// <summary>
/// 配置服务：负责 JSON 配置文件的读写
/// 对应第16章 配置与可扩展性
/// </summary>
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

    /// <summary>
    /// 加载配置，若不存在则创建默认配置
    /// </summary>
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

    /// <summary>
    /// 保存配置到文件
    /// </summary>
    public void Save(AppConfig config)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(_configPath, json);
        _cachedConfig = config;
    }

    /// <summary>
    /// 更新配置
    /// </summary>
    public void Update(Action<AppConfig> updateAction)
    {
        var config = Load();
        updateAction(config);
        Save(config);
    }
}
```

**Step 4: 创建默认配置文件**

```json
{
  "defaultComPort": "COM1",
  "baudRate": 115200,
  "timeoutThresholdMs": 2000,
  "displayRefreshIntervalMs": 33,
  "smoothingAlpha": 0.18,
  "maxMaskOpacity": 0.70,
  "hintStartLevel": 30,
  "urgentLevel": 80
}
```

**Step 5: 运行测试（GREEN）**

```bash
dotnet test --filter "FullyQualifiedName~ConfigServiceTests"
```
Expected: PASS

**Step 6: 提交**

```bash
git add PostureOverlayApp/Services/ConfigService.cs PostureOverlayApp/Services/ConfigServiceTests.cs PostureOverlayApp/config.json
git commit -m "feat: 实现 ConfigService 配置服务 (TDD)"
```

---

## 任务4: 集成到 MainWindow

**Files:**
- Modify: `PostureOverlayApp/MainWindow.xaml.cs`

**Step 1: 添加 BlurController 和 ValueMapper 集成（GREEN）**

```csharp
using System.Windows;
using System.Windows.Threading;
using PostureOverlayApp.Services;
using PostureOverlayApp.Models;

namespace PostureOverlayApp;

public partial class MainWindow : Window
{
    // 来自任务B的服务
    public SerialService SerialService { get; } = new();
    public DeviceProtocol Protocol { get; } = new();
    public DeviceStateManager StateManager { get; } = new();

    // 任务C新增的服务
    private readonly BlurController _blurController;
    private readonly ValueMapper _valueMapper;
    private readonly ConfigService _configService;

    private DispatcherTimer? _timeoutTimer;
    private DispatcherTimer? _displayTimer;

    public MainWindow()
    {
        InitializeComponent();

        // 初始化任务C的服务
        _configService = new ConfigService();
        var config = _configService.Load();

        _blurController = new BlurController(alpha: config.SmoothingAlpha);
        _valueMapper = new ValueMapper(
            hintStartLevel: config.HintStartLevel,
            urgentLevel: config.UrgentLevel);

        InitializeTimeoutTimer(config.TimeoutThresholdMs);
        InitializeDisplayTimer(config.DisplayRefreshIntervalMs);
        BindEvents();

        Log("应用程序已启动 - 业务逻辑层已加载");
    }

    private void InitializeTimeoutTimer(int intervalMs)
    {
        _timeoutTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timeoutTimer.Tick += (s, e) => StateManager.CheckTimeout();
        _timeoutTimer.Start();
    }

    private void InitializeDisplayTimer(int intervalMs)
    {
        _displayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(intervalMs)
        };
        _displayTimer.Tick += (s, e) =>
        {
            _blurController.Tick();
            UpdateOverlayState();
        };
        _displayTimer.Start();
    }

    private void BindEvents()
    {
        // SerialService 事件处理
        SerialService.OnLineReceived += line =>
        {
            if (Protocol.TryParse(line, out var value))
            {
                StateManager.ReceiveRawValue(value);
                _blurController.PushRawValue(value);
                Dispatcher.Invoke(() =>
                {
                    RawValueText.Text = value.ToString();
                    LastReceiveTimeText.Text = DateTime.Now.ToString("HH:mm:ss");
                });
            }
            else
            {
                Log($"解析失败: {line}");
            }
        };

        SerialService.OnError += ex =>
        {
            StateManager.OnFault(ex.Message);
            Log($"串口错误: {ex.Message}");
        };

        // BlurController 事件处理
        _blurController.OnDisplayValueChanged += level =>
        {
            Dispatcher.Invoke(() =>
            {
                DisplayValueText.Text = level.ToString("F1");
            });
        };

        // DeviceStateManager 事件处理
        StateManager.OnStateChanged += state =>
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = state.ConnectionState.ToString();
                UpdateStatusColor(state.ConnectionState);
            });
        };
    }

    private void UpdateOverlayState()
    {
        var state = _blurController.DisplayValue;
        var overlayState = _valueMapper.Map(state);
        OnOverlayStateChanged?.Invoke(overlayState);
    }

    // 供 OverlayWindow 订阅的事件
    public event Action<OverlayState>? OnOverlayStateChanged;

    private void UpdateStatusColor(DeviceConnectionState state)
    {
        StatusText.Foreground = state switch
        {
            DeviceConnectionState.ConnectedIdle or DeviceConnectionState.Receiving =>
                System.Windows.Media.Brushes.Green,
            DeviceConnectionState.Timeout =>
                System.Windows.Media.Brushes.Orange,
            DeviceConnectionState.Fault =>
                System.Windows.Media.Brushes.Red,
            _ =>
                System.Windows.Media.Brushes.Gray
        };
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
        _timeoutTimer?.Stop();
        _displayTimer?.Stop();
        SerialService.Dispose();
        base.OnClosed(e);
    }
}
```

**Step 2: 验证编译**

```bash
cd PostureOverlayApp
dotnet build
```
Expected: BUILD SUCCEEDED

**Step 3: 提交**

```bash
git add PostureOverlayApp/MainWindow.xaml.cs
git commit -m "feat: 集成 BlurController 和 ValueMapper 到 MainWindow"
```

---

## 交付清单

本任务完成后的完整交付物：

| 文件 | 描述 | 对应完整计划章节 |
|------|------|------------------|
| `Services/BlurController.cs` | 平滑控制器 | 第5章、第11章、第12章 |
| `Services/BlurControllerTests.cs` | BlurController 测试 | - |
| `Services/ValueMapper.cs` | 数值映射器 | 第8章、第11章 |
| `Services/ValueMapperTests.cs` | ValueMapper 测试 | - |
| `Services/ConfigService.cs` | 配置服务 | 第16章 |
| `Services/ConfigServiceTests.cs` | ConfigService 测试 | - |
| `config.json` | 默认配置文件 | 第12章 |

**关键接口：**
```csharp
// BlurController
event OnDisplayValueChanged(double displayLevel)
void PushRawValue(int value)
void Tick()  // 由定时器驱动

// ValueMapper
OverlayState Map(double displayLevel)

// ConfigService
AppConfig Load()
void Save(AppConfig config)
void Update(Action<AppConfig> updateAction)
```

**数据流（对应第5章）：**
```
MCU -> SerialService -> DeviceProtocol -> BlurController.PushRawValue()
                                                    |
                                                    v
                                          BlurController.Tick() (定时器驱动)
                                                    |
                                                    v
                                          ValueMapper.Map(displayLevel)
                                                    |
                                                    v
                                          OnOverlayStateChanged(overlayState)
```

**下一步依赖:**
- 任务D 将使用 `OnOverlayStateChanged` 事件来更新 OverlayWindow 显示
