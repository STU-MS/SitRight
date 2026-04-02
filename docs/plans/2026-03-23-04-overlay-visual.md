# Overlay 视觉呈现层实现计划

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.
>
> **TDD Required:** 所有实现必须遵循 Red-Green-Refactor 循环：
> 1. 先写测试，运行验证 FAIL
> 2. 再写实现，运行验证 PASS
> 3. 重构代码

**Goal:** 实现全屏 OverlayWindow 遮罩窗口和 OverlayViewModel，直接映射硬件端 blurLevel 的视觉反馈

**Architecture:**
- OverlayWindow 是一个全屏、置顶、无边框、透明背景的窗口（第8章 Overlay设计）
- OverlayViewModel 绑定视觉属性，通过 INotifyPropertyChanged 驱动 UI 更新
- MainViewModel 整合所有服务，提供完整的 MainWindow 数据绑定

**Tech Stack:** .NET 8.0 / WPF / C# / xUnit

**对应完整计划章节:** 第8章 Overlay设计、第9章 主控制窗口设计、第10章 时序设计

---

## 任务0: 验证依赖服务存在

**Files:**
- Read: `SitRight/Models/OverlayState.cs`

**Step 1: 验证项目编译**

```bash
cd SitRight
dotnet build
```
Expected: BUILD SUCCEEDED

---

## 任务1: OverlayViewModel 视图模型

**Files:**
- Create: `SitRight/ViewModels/OverlayViewModel.cs`
- Create: `SitRight/ViewModels/OverlayViewModelTests.cs`

**对应完整计划章节:** 第8章 Overlay设计

**TDD Step 1: 编写测试（RED）**

```csharp
using Xunit;
using SitRight.Models;
using SitRight.ViewModels;

namespace SitRight.ViewModels;

public class OverlayViewModelTests
{
    [Fact]
    public void InitialState_IsInvisible()
    {
        var vm = new OverlayViewModel();
        Assert.False(vm.IsVisible);
        Assert.Equal(0, vm.MaskOpacity);
        Assert.Equal(0, vm.EdgeOpacity);
        Assert.Equal(0, vm.MessageOpacity);
    }

    [Fact]
    public void InitialState_HasDefaultColor()
    {
        var vm = new OverlayViewModel();
        Assert.Equal("#FFFFFF", vm.MaskColor);
        Assert.Equal(string.Empty, vm.MessageText);
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
            MessageText = "请调整坐姿",
            MessageOpacity = 0.8,
            SeverityLevel = 2
        };

        vm.UpdateFrom(state);

        Assert.Equal(0.5, vm.MaskOpacity);
        Assert.Equal("#E0E0E0", vm.MaskColor);
        Assert.Equal(0.2, vm.EdgeOpacity);
        Assert.Equal("请调整坐姿", vm.MessageText);
        Assert.Equal(0.8, vm.MessageOpacity);
        Assert.Equal(2, vm.SeverityLevel);
    }

    [Fact]
    public void UpdateFrom_ZeroOpacity_SetsInvisible()
    {
        var vm = new OverlayViewModel();
        vm.UpdateFrom(new OverlayState { MaskOpacity = 0, MessageText = "" });
        Assert.False(vm.IsVisible);
    }

    [Fact]
    public void UpdateFrom_HasMessage_SetsVisible()
    {
        var vm = new OverlayViewModel();
        vm.UpdateFrom(new OverlayState { MaskOpacity = 0, MessageText = "请调整" });
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

**Step 2: 运行测试（RED）**

```bash
cd SitRight
dotnet test --filter "FullyQualifiedName~OverlayViewModelTests"
```
Expected: FAIL - OverlayViewModel not found

**TDD Step 3: 实现 OverlayViewModel（GREEN）**

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SitRight.Models;

namespace SitRight.ViewModels;

/// <summary>
/// OverlayViewModel：绑定 OverlayWindow 的视觉属性
/// 对应第8章 Overlay设计
/// </summary>
public class OverlayViewModel : INotifyPropertyChanged
{
    private double _maskOpacity;
    private string _maskColor = "#FFFFFF";
    private double _edgeOpacity;
    private string _messageText = string.Empty;
    private double _messageOpacity;
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

    public string MessageText
    {
        get => _messageText;
        set => SetProperty(ref _messageText, value);
    }

    public double MessageOpacity
    {
        get => _messageOpacity;
        set => SetProperty(ref _messageOpacity, value);
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
        MessageText = state.MessageText;
        MessageOpacity = state.MessageOpacity;
        SeverityLevel = state.SeverityLevel;
        IsVisible = state.MaskOpacity > 0.01 || !string.IsNullOrEmpty(state.MessageText);
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

**Step 4: 运行测试（GREEN）**

```bash
dotnet test --filter "FullyQualifiedName~OverlayViewModelTests"
```
Expected: PASS

**Step 5: 提交**

```bash
git add SitRight/ViewModels/OverlayViewModel.cs SitRight/ViewModels/OverlayViewModelTests.cs
git commit -m "feat: 实现 OverlayViewModel (TDD)"
```

---

## 任务2: OverlayWindow 全屏遮罩窗口

**Files:**
- Create: `SitRight/OverlayWindow.xaml`
- Create: `SitRight/OverlayWindow.xaml.cs`

**对应完整计划章节:** 第8章 Overlay设计

**Step 1: 创建 OverlayWindow.xaml**

```xml
<Window x:Class="SitRight.OverlayWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Overlay"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        WindowState="Maximized"
        WindowStartupLocation="CenterScreen">

    <!--
        第8章 Overlay 设计：
        - 全屏、无边框、置顶、不出现在任务栏、背景透明
        - 内部结构：Grid -> 主遮罩层 + 边缘雾层 + 提示文字 + 调试角标
    -->
    <Grid x:Name="RootGrid">
        <!-- 主遮罩层 -->
        <Rectangle x:Name="MainMask"
                    Fill="#FFFFFF"
                    Opacity="0"/>

        <!-- 边缘雾层（径向渐变模拟雾化效果） -->
        <Rectangle x:Name="EdgeMask">
            <Rectangle.Fill>
                <RadialGradientBrush GradientOrigin="0.5,0.5" Center="0.5,0.5">
                    <GradientStop Color="#00000000" Offset="0.5"/>
                    <GradientStop Color="#40000000" Offset="1.0"/>
                </RadialGradientBrush>
            </Rectangle.Fill>
        </Rectangle>

        <!-- 提示文字区域 -->
        <TextBlock x:Name="MessageText"
                   Text=""
                   FontSize="48"
                   FontWeight="Bold"
                   Foreground="#333333"
                   HorizontalAlignment="Center"
                   VerticalAlignment="Top"
                   Margin="0,120,0,0"
                   Opacity="0"
                   TextWrapping="Wrap"
                   TextAlignment="Center">
            <TextBlock.Effect>
                <DropShadowEffect Color="White"
                                  ShadowDepth="0"
                                  BlurRadius="20"
                                  Opacity="0.9"/>
            </TextBlock.Effect>
        </TextBlock>

        <!-- 调试信息角标（默认隐藏） -->
        <Border x:Name="DebugBadge"
                Background="#80000000"
                CornerRadius="4"
                Padding="8,4"
                HorizontalAlignment="Right"
                VerticalAlignment="Bottom"
                Margin="0,0,20,20"
                Visibility="Collapsed">
            <StackPanel>
                <TextBlock x:Name="DebugLevelText"
                           Foreground="White"
                           FontFamily="Consolas"
                           FontSize="14"/>
                <TextBlock x:Name="DebugOpacityText"
                           Foreground="#AAA"
                           FontFamily="Consolas"
                           FontSize="12"/>
            </StackPanel>
        </Border>
    </Grid>
</Window>
```

**Step 2: 创建 OverlayWindow.xaml.cs**

```csharp
using System.Windows;
using System.Windows.Media;
using SitRight.Models;

namespace SitRight;

/// <summary>
/// OverlayWindow：全屏遮罩窗口，对应第8章 Overlay 设计
/// - 全屏、置顶、无边框、背景透明
/// - 内部包含主遮罩层、边缘雾层、提示文字
/// </summary>
public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        // 初始化为隐藏状态
        Hide();
    }

    /// <summary>
    /// 更新 Overlay 显示状态
    /// </summary>
    /// <param name="state">由 ValueMapper 生成的状态</param>
    public void Update(OverlayState state)
    {
        Dispatcher.Invoke(() =>
        {
            ApplyVisualState(state);
        });
    }

    private void ApplyVisualState(OverlayState state)
    {
        // 应用主遮罩透明度
        MainMask.Opacity = state.MaskOpacity;

        // 应用颜色
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(state.MaskColor);
            MainMask.Fill = new SolidColorBrush(color);
        }
        catch
        {
            MainMask.Fill = Brushes.White;
        }

        // 应用边缘雾
        EdgeMask.Opacity = state.EdgeOpacity;

        // 应用提示文字
        MessageText.Text = state.MessageText;
        MessageText.Opacity = state.MessageOpacity;

        // 根据严重程度调整文字样式
        if (state.SeverityLevel >= 3)
        {
            // 紧急状态：红色大字
            MessageText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#B71C1C"));
            MessageText.FontSize = 56;
        }
        else if (state.SeverityLevel >= 2)
        {
            // 警告状态：深灰
            MessageText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#333333"));
            MessageText.FontSize = 48;
        }
        else
        {
            // 提示状态：浅灰
            MessageText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#555555"));
            MessageText.FontSize = 40;
        }

        // 控制窗口可见性
        if (state.MaskOpacity > 0.01 || !string.IsNullOrEmpty(state.MessageText))
        {
            Show();
            Activate();
        }
        else
        {
            Hide();
        }
    }

    /// <summary>
    /// 设置调试模式
    /// </summary>
    public void SetDebugMode(bool enabled)
    {
        Dispatcher.Invoke(() =>
        {
            DebugBadge.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    /// <summary>
    /// 更新调试信息
    /// </summary>
    public void UpdateDebugInfo(double displayLevel, double maskOpacity)
    {
        Dispatcher.Invoke(() =>
        {
            DebugLevelText.Text = $"Level: {displayLevel:F1}";
            DebugOpacityText.Text = $"Opacity: {maskOpacity:F2}";
        });
    }
}
```

**Step 3: 验证编译**

```bash
cd SitRight
dotnet build
```
Expected: BUILD SUCCEEDED

**Step 4: 提交**

```bash
git add SitRight/OverlayWindow.xaml SitRight/OverlayWindow.xaml.cs
git commit -m "feat: 实现 OverlayWindow 全屏遮罩窗口"
```

---

## 任务3: MainViewModel 主视图模型

**Files:**
- Create: `SitRight/ViewModels/MainViewModel.cs`
- Create: `SitRight/ViewModels/MainViewModelTests.cs`

**对应完整计划章节:** 第9章 主控制窗口设计

**TDD Step 1: 编写测试（RED）**

```csharp
using Xunit;
using Moq;
using SitRight.Services;
using SitRight.Models;
using SitRight.ViewModels;

namespace SitRight.ViewModels;

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
        _configService = new ConfigService();

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
    public void Disconnect_SetsIsConnectedToFalse()
    {
        _mockSerial.Setup(s => s.IsConnected).Returns(true);
        _viewModel.Disconnect();
        Assert.False(_viewModel.IsConnected);
    }

    [Fact]
    public void OnConnectionStateChanged_EventIsRaised()
    {
        var eventRaised = false;
        _viewModel.OnConnectionStateChanged += _ => eventRaised = true;

        _viewModel.Disconnect();

        Assert.True(eventRaised);
    }

    [Fact]
    public void OnOverlayStateChanged_EventIsRaised()
    {
        var eventRaised = false;
        _viewModel.OnOverlayStateChanged += _ => eventRaised = true;

        _viewModel.IsSimulationMode = true;
        _viewModel.SimulateValue(50);

        Assert.True(eventRaised);
    }
}
```

**Step 2: 运行测试（RED）**

```bash
dotnet test --filter "FullyQualifiedName~MainViewModelTests"
```
Expected: FAIL - MainViewModel not found

**Note:** MainViewModelTests 使用 Moq，需要添加包引用：

**TDD Step 3: 添加 Moq 依赖（GREEN）**

```xml
<PackageReference Include="Moq" Version="4.20.70" />
```

```bash
cd SitRight
dotnet add package Moq --version 4.20.70
```

**TDD Step 4: 实现 MainViewModel（GREEN）**

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SitRight.Models;
using SitRight.Services;

namespace SitRight.ViewModels;

/// <summary>
/// MainViewModel：整合所有服务，提供 MainWindow 数据绑定
/// 对应第9章 主控制窗口设计
/// </summary>
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
        // SerialService 行接收处理
        _serialService.OnLineReceived += line =>
        {
            if (_protocol.TryParse(line, out var value))
            {
                _stateManager.ReceiveRawValue(value);
                // 直接映射硬件端 blurLevel，平滑算法在硬件端完成
                // 注意：ACK/ERR 校准回包解析尚未实现，需在后续校准 UI 任务中补充
                RawValueText = value.ToString();
                LastReceiveTimeText = DateTime.Now.ToString("HH:mm:ss");

                // 直接映射到 OverlayState
                var overlayState = _valueMapper.Map(value);
                DisplayValueText = value.ToString("F1");
                OnOverlayStateChanged?.Invoke(overlayState);
            }
        };

        // 设备状态变更处理
        _stateManager.OnStateChanged += state =>
        {
            StatusText = state.ConnectionState.ToString();
            OnConnectionStateChanged?.Invoke(state.ConnectionState);
        };
    }

    public void Connect(string portName, int baudRate)
    {
        try
        {
            _serialService.Connect(portName, baudRate);
            IsConnected = true;
        }
        catch (Exception ex)
        {
            _stateManager.OnFault(ex.Message);
            throw;
        }
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
            // 直接映射，不经过 BlurController
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

**Step 5: 运行测试（GREEN）**

```bash
dotnet test --filter "FullyQualifiedName~MainViewModelTests"
```
Expected: PASS

**Step 6: 提交**

```bash
git add SitRight/ViewModels/MainViewModel.cs SitRight/ViewModels/MainViewModelTests.cs
git commit -m "feat: 实现 MainViewModel (TDD)"
```

---

## 任务4: 集成到 MainWindow

**Files:**
- Modify: `SitRight/MainWindow.xaml.cs`
- Modify: `SitRight/MainWindow.xaml`

**Step 1: 重构 MainWindow 使用 MainViewModel**

```csharp
using System.Windows;
using System.Windows.Threading;
using SitRight.Services;
using SitRight.Models;
using SitRight.ViewModels;

namespace SitRight;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private OverlayWindow? _overlayWindow;

    public MainWindow()
    {
        InitializeComponent();

        // 初始化服务
        var configService = new ConfigService();
        var config = configService.Load();

        var serialService = new SerialService();
        var protocol = new DeviceProtocol();
        var stateManager = new DeviceStateManager(config.TimeoutThresholdMs);
        var valueMapper = new ValueMapper(config.HintStartLevel, config.UrgentLevel);

        // 创建 ViewModel（平滑算法在硬件端完成，软件端直接映射 blurLevel）
        _viewModel = new MainViewModel(
            serialService,
            protocol,
            stateManager,
            valueMapper,
            configService);

        DataContext = _viewModel;

        // 初始化定时器
        InitializeTimers(config);

        // 初始化 Overlay 窗口
        InitializeOverlay();

        // 绑定 UI 事件
        BindUIEvents();

        Log("应用程序已启动");
    }

    private void InitializeTimers(AppConfig config)
    {
        // 超时检查定时器
        var timeoutTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timeoutTimer.Tick += (s, e) => _viewModel.StatusText; // 通过 ViewModel 访问触发检查
        timeoutTimer.Start();

        // 显示更新定时器（数据流为直接映射，由 OnLineReceived 事件驱动）
    }

    private void InitializeOverlay()
    {
        _overlayWindow = new OverlayWindow();
        _viewModel.OnOverlayStateChanged += state => _overlayWindow?.Update(state);
    }

    private void BindUIEvents()
    {
        // 连接按钮
        ConnectButton.Click += (s, e) =>
        {
            if (_viewModel.IsConnected)
            {
                _viewModel.Disconnect();
                ConnectButton.Content = "连接";
                Log("已断开连接");
            }
            else
            {
                try
                {
                    var port = ComPortComboBox.SelectedItem?.ToString();
                    var baudRate = int.Parse(BaudRateComboBox.SelectedItem?.ToString() ?? "115200");
                    if (!string.IsNullOrEmpty(port))
                    {
                        _viewModel.Connect(port, baudRate);
                        ConnectButton.Content = "断开";
                        Log($"正在连接 {port}...");
                    }
                }
                catch (Exception ex)
                {
                    Log($"连接失败: {ex.Message}");
                }
            }
        };

        // 刷新串口按钮
        RefreshButton.Click += (s, e) =>
        {
            ComPortComboBox.ItemsSource = _viewModel.AvailablePorts;
            Log("已刷新串口列表");
        };

        // 模拟模式复选框
        SimulationModeCheckBox.Checked += (s, e) =>
        {
            _viewModel.IsSimulationMode = true;
            SimulatedValueSlider.IsEnabled = true;
        };

        SimulationModeCheckBox.Unchecked += (s, e) =>
        {
            _viewModel.IsSimulationMode = false;
            SimulatedValueSlider.IsEnabled = false;
        };

        // 模拟滑条
        SimulatedValueSlider.ValueChanged += (s, e) =>
        {
            var value = (int)SimulatedValueSlider.Value;
            _viewModel.SimulateValue(value);
            SimulatedValueText.Text = value.ToString();
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
        _overlayWindow?.Close();
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
git commit -m "feat: 重构 MainWindow 使用 MainViewModel"
```

---

## 任务5: 端到端集成测试

**Files:**
- Create: `SitRight/IntegrationTests.cs`

**Step 1: 编写集成测试**

```csharp
using Xunit;
using SitRight.Models;
using SitRight.Services;
using SitRight.ViewModels;

namespace SitRight;

public class IntegrationTests
{
    [Fact]
    public void FullPipeline_RawValueToOverlayState()
    {
        // 模拟完整数据流：RawValue -> ValueMapper -> OverlayState（平滑算法在硬件端完成）
        var valueMapper = new ValueMapper(hintStartLevel: 30, urgentLevel: 80);

        // 直接映射硬件端输出的 blurLevel
        var overlayState = valueMapper.Map(100);

        // Verify
        Assert.Equal(100, overlayState.MaskOpacity, 0.1);
        Assert.Equal(3, overlayState.SeverityLevel);
        Assert.NotEmpty(overlayState.MessageText);
    }

    [Fact]
    public void FullPipeline_LowValue_NoMessage()
    {
        var valueMapper = new ValueMapper(hintStartLevel: 30, urgentLevel: 80);

        var overlayState = valueMapper.Map(10);

        Assert.True(overlayState.MaskOpacity < 0.1);
        Assert.Empty(overlayState.MessageText);
    }

    [Fact]
    public void ConfigService_PreservesRecommendedDefaults()
    {
        // 第12章推荐参数默认值
        var config = new AppConfig();

        Assert.Equal(115200, config.BaudRate);
        Assert.Equal(2000, config.TimeoutThresholdMs);
        Assert.Equal(33, config.DisplayRefreshIntervalMs);
        Assert.Equal(0.18, config.SmoothingAlpha);
        Assert.Equal(0.70, config.MaxMaskOpacity);
    }

    [Fact]
    public void OverlayState_FromDisplayLevel_MatchesChapter8()
    {
        // 第8章：0~20 基本透明仅轻提示，21~50 明显白雾/浅灰层
        var state0 = OverlayState.FromDisplayLevel(0, 30, 80);
        var state50 = OverlayState.FromDisplayLevel(50, 30, 80);
        var state100 = OverlayState.FromDisplayLevel(100, 30, 80);

        Assert.True(state0.MaskOpacity < state50.MaskOpacity);
        Assert.True(state50.MaskOpacity < state100.MaskOpacity);
    }
}
```

**Step 2: 运行集成测试**

```bash
dotnet test --filter "FullyQualifiedName~IntegrationTests"
```
Expected: PASS

**Step 3: 提交**

```bash
git add SitRight/IntegrationTests.cs
git commit -m "test: 添加端到端集成测试"
```

---

## 交付清单

本任务完成后的完整交付物：

| 文件 | 描述 | 对应完整计划章节 |
|------|------|------------------|
| `ViewModels/OverlayViewModel.cs` | Overlay 视觉属性绑定 | 第8章 |
| `ViewModels/OverlayViewModelTests.cs` | OverlayViewModel 测试 | - |
| `ViewModels/MainViewModel.cs` | 主窗口 ViewModel | 第9章 |
| `ViewModels/MainViewModelTests.cs` | MainViewModel 测试 | - |
| `OverlayWindow.xaml/cs` | 全屏遮罩窗口 | 第8章 |
| `MainWindow.xaml/cs` | 重构后的主窗口 | 第9章 |
| `IntegrationTests.cs` | 端到端集成测试 | 全部章节 |

**完整系统架构（对应文档第4章目录结构）：**
```
SitRight/
├─ App.xaml / App.xaml.cs
├─ MainWindow.xaml / MainWindow.xaml.cs      ← 重构后整合所有服务
├─ OverlayWindow.xaml / OverlayWindow.xaml.cs ← 任务D新增
├─ Models/
│  ├─ DeviceState.cs
│  ├─ OverlayState.cs
│  └─ AppConfig.cs
├─ Services/
│  ├─ ISerialService.cs
│  ├─ SerialService.cs
│  ├─ DeviceProtocol.cs
│  ├─ DeviceStateManager.cs
│  ├─ ValueMapper.cs
│  └─ ConfigService.cs
├─ ViewModels/
│  ├─ OverlayViewModel.cs
│  └─ MainViewModel.cs
└─ config.json
```

**数据流（对应第5章、第10章）：**
```
MCU(含平滑算法) → SerialService → DeviceProtocol → ValueMapper → OverlayViewModel → OverlayWindow
                       ↓
                MainViewModel ← DeviceStateManager
                       ↓
                MainWindow (状态显示)
```
