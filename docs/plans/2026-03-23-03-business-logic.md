# 业务逻辑层实现计划

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.
>
> **TDD Required:** 所有实现必须遵循 Red-Green-Refactor 循环：
> 1. 先写测试，运行验证 FAIL
> 2. 再写实现，运行验证 PASS
> 3. 重构代码

**Goal:** 实现 ValueMapper 数值映射器、ConfigService 配置服务

**Architecture:**
- **平滑算法在硬件端完成**，PC 端不包含 BlurController，收到的 blurLevel 已经是平滑后的值
- ValueMapper 将硬件端发来的 blurLevel 直接映射为 OverlayState 视觉参数（第8章 Overlay设计、第11章 ValueMapper说明）
- ConfigService 处理 JSON 配置文件的读写（第16章 配置与可扩展性）

**Tech Stack:** .NET 8.0 / WPF / System.Text.Json / xUnit

**对应完整计划章节:** 第5章 核心数据流、第8章 Overlay设计、第11章 关键模块说明、第12章 推荐参数默认值、第16章 配置与可扩展性

---

## 任务0: 验证 Model 类存在

**Files:**
- Read: `SitRight/Models/OverlayState.cs`
- Read: `SitRight/Models/AppConfig.cs`

**Step 1: 验证项目结构**

```bash
cd SitRight
dotnet build
```
Expected: BUILD SUCCEEDED

**Step 2: 提交**

```bash
git commit -m "chore: 验证 Model 类可用"
```

---

## 任务1: ~~BlurController 平滑控制器~~ (已移除)

> **说明：** 平滑算法已移至硬件端完成，PC 端不再需要 BlurController。
> 硬件端输出的 blurLevel 已经是经过指数滑动平均处理后的平滑值，
> PC 端收到后直接通过 ValueMapper 映射为 OverlayState 即可。

**无代码需要实现。** 此任务已标记为移除。

---

## 任务2: ValueMapper 数值映射器

**Files:**
- Create: `SitRight/Services/ValueMapper.cs`
- Create: `SitRight.Tests/ValueMapperTests.cs`

**对应完整计划章节:** 第8章 Overlay设计、第11章 ValueMapper说明

**设计说明：**
- ValueMapper.Map 的参数语义为"直接映射硬件端发来的 blurLevel"
- blurLevel 由硬件端经过平滑算法处理后发送，PC 端不做二次平滑

**TDD Step 1: 编写测试（RED）**

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
using SitRight.Models;

namespace SitRight.Services;

/// <summary>
/// ValueMapper：将硬件端发来的 blurLevel 直接映射为 OverlayState 视觉参数
/// 对应第8章 Overlay设计、第11章 ValueMapper说明
///
/// 注意：blurLevel 由硬件端完成平滑处理，PC 端不做二次平滑
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
    /// 将硬件端发来的 blurLevel 映射为 Overlay 视觉状态
    /// </summary>
    /// <param name="blurLevel">硬件端发来的 0~100 平滑值（已在硬件端完成平滑处理）</param>
    /// <returns>OverlayState 视觉参数</returns>
    public OverlayState Map(int blurLevel)
    {
        return OverlayState.FromDisplayLevel(blurLevel, _hintStartLevel, _urgentLevel);
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
git add SitRight/Services/ValueMapper.cs SitRight.Tests/ValueMapperTests.cs
git commit -m "feat: 实现 ValueMapper 数值映射器 (TDD)"
```

---

## 任务3: ConfigService 配置服务

**Files:**
- Create: `SitRight/Services/ConfigService.cs`
- Create: `SitRight.Tests/ConfigServiceTests.cs`
- Create: `SitRight/config.json`

**对应完整计划章节:** 第16章 配置与可扩展性

**设计说明：**
- AppConfig 中移除 SmoothingAlpha 和 DisplayRefreshIntervalMs（平滑在硬件端完成，PC端不需要这两个参数）

**TDD Step 1: 编写测试（RED）**

```csharp
using Xunit;
using SitRight.Services;
using SitRight.Models;
using System.IO;

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

        // Create new service instance to load from disk
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

**Step 2: 运行测试（RED）**

```bash
dotnet test --filter "FullyQualifiedName~ConfigServiceTests"
```
Expected: FAIL - ConfigService not found

**TDD Step 3: 实现 ConfigService（GREEN）**

```csharp
using System.IO;
using System.Text.Json;
using SitRight.Models;

namespace SitRight.Services;

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
git add SitRight/Services/ConfigService.cs SitRight.Tests/ConfigServiceTests.cs SitRight/config.json
git commit -m "feat: 实现 ConfigService 配置服务 (TDD)"
```

---

## 任务4: 集成到 MainWindow

**Files:**
- Modify: `SitRight/MainWindow.xaml.cs`

**设计说明：**
- 移除 BlurController 引用（平滑在硬件端完成）
- 移除 _displayTimer（不再需要定时器驱动平滑）
- 数据流简化为：SerialService -> DeviceProtocol -> 直接 ValueMapper.Map(value) -> OverlayState
- 每次收到硬件端数据时，立即映射并更新 OverlayState
- 注意：串口协议为双通道，ACK/ERR 校准回包当前未实现解析，**需在后续校准 UI 任务中补充 DeviceProtocol 的 ACK/ERR 解析能力**

**Step 1: 添加 ValueMapper 集成（GREEN）**

```csharp
using System.Windows;
using System.Windows.Threading;
using SitRight.Services;
using SitRight.Models;

namespace SitRight;

public partial class MainWindow : Window
{
    // 来自任务B的服务
    public SerialService SerialService { get; } = new();
    public DeviceProtocol Protocol { get; } = new();
    public DeviceStateManager StateManager { get; } = new();

    // 任务C新增的服务
    private readonly ValueMapper _valueMapper;
    private readonly ConfigService _configService;

    private DispatcherTimer? _timeoutTimer;

    public MainWindow()
    {
        InitializeComponent();

        // 初始化任务C的服务
        _configService = new ConfigService();
        var config = _configService.Load();

        _valueMapper = new ValueMapper(
            hintStartLevel: config.HintStartLevel,
            urgentLevel: config.UrgentLevel);

        InitializeTimeoutTimer(config.TimeoutThresholdMs);
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

    private void BindEvents()
    {
        // SerialService 事件处理
        // 数据流：SerialService -> DeviceProtocol -> ValueMapper.Map(blurLevel) -> OverlayState
        SerialService.OnLineReceived += line =>
        {
            if (Protocol.TryParse(line, out var value))
            {
                StateManager.ReceiveRawValue(value);
                Dispatcher.Invoke(() =>
                {
                    RawValueText.Text = value.ToString();
                    LastReceiveTimeText.Text = DateTime.Now.ToString("HH:mm:ss");

                    // 直接映射硬件端发来的 blurLevel（已在硬件端完成平滑）
                    var overlayState = _valueMapper.Map(value);
                    OnOverlayStateChanged?.Invoke(overlayState);
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
        SerialService.Dispose();
        base.OnClosed(e);
    }
}
```

**Step 2: 验证编译**

```bash
cd SitRight
dotnet build
```
Expected: BUILD SUCCEEDED

**Step 3: 提交**

```bash
git add SitRight/MainWindow.xaml.cs
git commit -m "feat: 集成 ValueMapper 到 MainWindow（直接映射硬件端 blurLevel）"
```

---

## 交付清单

本任务完成后的完整交付物：

| 文件 | 描述 | 对应完整计划章节 |
|------|------|------------------|
| `Services/ValueMapper.cs` | 数值映射器（直接映射硬件端 blurLevel） | 第8章、第11章 |
| `SitRight.Tests/ValueMapperTests.cs` | ValueMapper 测试 | - |
| `Services/ConfigService.cs` | 配置服务 | 第16章 |
| `SitRight.Tests/ConfigServiceTests.cs` | ConfigService 测试 | - |
| `config.json` | 默认配置文件 | 第12章 |

**关键接口：**
```csharp
// ValueMapper - 直接映射硬件端发来的 blurLevel
OverlayState Map(int blurLevel)

// ConfigService
AppConfig Load()
void Save(AppConfig config)
void Update(Action<AppConfig> updateAction)
```

**数据流（对应第5章）：**
```
MCU (硬件端平滑处理) -> SerialService -> DeviceProtocol -> ValueMapper.Map(blurLevel)
                                                               |
                                                               v
                                                    OnOverlayStateChanged(overlayState)
```

**架构决策记录：**
- 平滑算法（指数滑动平均）在硬件端完成，PC 端不包含 BlurController
- AppConfig 中移除 SmoothingAlpha 和 DisplayRefreshIntervalMs
- MainWindow 中移除 _displayTimer，改为每次收到数据时即时映射
- 数据流简化为：blurLevel -> ValueMapper -> OverlayState

**下一步依赖:**
- 任务D 将使用 `OnOverlayStateChanged` 事件来更新 OverlayWindow 显示
