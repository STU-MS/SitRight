# PostureOverlayApp 项目骨架搭建计划

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.
>
> **TDD Required:** 所有实现必须遵循 Red-Green-Refactor 循环：
> 1. 先写测试，运行验证 FAIL
> 2. 再写实现，运行验证 PASS
> 3. 重构代码

**Goal:** 创建 WPF 项目骨架，包含目录结构、App.xaml、MainWindow 基础布局、Models 基础类

**Architecture:** 采用标准 WPF 项目结构，按功能分为 Models、Services、ViewModels、Utils、Assets 五个目录。MainWindow 作为主控制窗口，承载串口配置、连接控制、状态显示、模拟输入功能。

**Tech Stack:** .NET 8.0 / WPF / C# / xUnit

**对应完整计划章节:** 第4章 软件架构设计、第9章 主控制窗口设计

---

## 任务0: 创建 WPF 项目结构

**Files:**
- Create: `PostureOverlayApp/PostureOverlayApp.csproj`
- Create: `PostureOverlayApp/App.xaml`
- Create: `PostureOverlayApp/App.xaml.cs`
- Create: `PostureOverlayApp/MainWindow.xaml`
- Create: `PostureOverlayApp/MainWindow.xaml.cs`
- Create: `PostureOverlayApp/Models/.gitkeep`
- Create: `PostureOverlayApp/Services/.gitkeep`
- Create: `PostureOverlayApp/ViewModels/.gitkeep`
- Create: `PostureOverlayApp/Utils/.gitkeep`
- Create: `PostureOverlayApp/Assets/.gitkeep`

**Step 1: 创建项目文件**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <ApplicationIcon />
    <StartupObject />
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.4">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

**Step 2: 创建 App.xaml**

```xml
<Application x:Class="PostureOverlayApp.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
    </Application.Resources>
</Application>
```

**Step 3: 创建 App.xaml.cs**

```csharp
using System.Windows;
namespace PostureOverlayApp;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
    }
}
```

**Step 4: 验证项目编译**

```bash
cd PostureOverlayApp
dotnet build
```

Expected: BUILD SUCCEEDED

**Step 5: 提交**

```bash
git add PostureOverlayApp/
git commit -m "feat: 初始化 WPF 项目骨架"
```

---

## 任务1: MainWindow 基础布局

**Files:**
- Modify: `PostureOverlayApp/MainWindow.xaml`
- Modify: `PostureOverlayApp/MainWindow.xaml.cs`

**Step 1: 编写 MainWindow.xaml 基础布局**

```xml
<Window x:Class="PostureOverlayApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="SitRight - 坐姿矫正仪"
        Width="480" Height="400"
        WindowStartupLocation="CenterScreen">
    <Grid Margin="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- 串口配置区 -->
        <GroupBox Grid.Row="0" Header="串口连接" Margin="0,0,0,12">
            <StackPanel Orientation="Horizontal" Margin="8">
                <TextBlock Text="COM 口:" VerticalAlignment="Center" Margin="0,0,8,0"/>
                <ComboBox x:Name="ComPortComboBox" Width="120" Margin="0,0,16,0"/>
                <TextBlock Text="波特率:" VerticalAlignment="Center" Margin="0,0,8,0"/>
                <ComboBox x:Name="BaudRateComboBox" Width="100" Margin="0,0,16,0">
                    <ComboBoxItem Content="9600"/>
                    <ComboBoxItem Content="115200" IsSelected="True"/>
                </ComboBox>
                <Button x:Name="RefreshButton" Content="刷新" Width="60" Margin="0,0,8,0"/>
                <Button x:Name="ConnectButton" Content="连接" Width="80"/>
            </StackPanel>
        </GroupBox>

        <!-- 状态显示区 -->
        <GroupBox Grid.Row="1" Header="设备状态" Margin="0,0,0,12">
            <Grid Margin="8">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>
                <TextBlock Grid.Row="0" Grid.Column="0" Text="状态:" FontWeight="Bold" Margin="0,0,12,4"/>
                <TextBlock Grid.Row="0" Grid.Column="1" x:Name="StatusText" Text="未连接" Foreground="Gray"/>
                <TextBlock Grid.Row="1" Grid.Column="0" Text="原始值:" Margin="0,0,12,4"/>
                <TextBlock Grid.Row="1" Grid.Column="1" x:Name="RawValueText" Text="--" Foreground="Blue"/>
                <TextBlock Grid.Row="2" Grid.Column="0" Text="显示值:" Margin="0,0,12,4"/>
                <TextBlock Grid.Row="2" Grid.Column="1" x:Name="DisplayValueText" Text="--" Foreground="Green"/>
                <TextBlock Grid.Row="3" Grid.Column="0" Text="最后接收:" Margin="0,0,12,0"/>
                <TextBlock Grid.Row="3" Grid.Column="1" x:Name="LastReceiveTimeText" Text="--" Foreground="Orange"/>
            </Grid>
        </GroupBox>

        <!-- 模拟模式区 -->
        <GroupBox Grid.Row="2" Header="模拟模式" Margin="0,0,0,12">
            <StackPanel Orientation="Horizontal" Margin="8">
                <CheckBox x:Name="SimulationModeCheckBox" Content="启用模拟" VerticalAlignment="Center"/>
                <Slider x:Name="SimulatedValueSlider" Minimum="0" Maximum="100" Value="0"
                        Width="200" Margin="16,0,0,0" IsEnabled="False"/>
                <TextBlock x:Name="SimulatedValueText" Text="0" VerticalAlignment="Center" Margin="8,0,0,0" Width="30"/>
            </StackPanel>
        </GroupBox>

        <!-- 日志区域 -->
        <GroupBox Grid.Row="3" Header="日志">
            <ScrollViewer>
                <TextBox x:Name="LogTextBox" IsReadOnly="True" TextWrapping="Wrap"
                         VerticalScrollBarVisibility="Auto" FontFamily="Consolas" FontSize="11"/>
            </ScrollViewer>
        </GroupBox>
    </Grid>
</Window>
```

**Step 2: 创建 MainWindow.xaml.cs**

```csharp
using System.Windows;
namespace PostureOverlayApp;
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Log("应用程序已启动");
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

    protected void UpdateStatus(string status, string color = "Gray")
    {
        Dispatcher.Invoke(() => StatusText.Text = status);
    }
}
```

**Step 3: 运行验证**

```bash
cd PostureOverlayApp
dotnet run --project PostureOverlayApp.csproj
```

Expected: 窗口显示正常，标题为 "SitRight - 坐姿矫正仪"，四个区域正确布局

**Step 4: 提交**

```bash
git add PostureOverlayApp/
git commit -m "feat: 添加 MainWindow 基础布局"
```

---

## 任务2: 添加基础 Model 类

**Files:**
- Create: `PostureOverlayApp/Models/DeviceState.cs`
- Create: `PostureOverlayApp/Models/DeviceStateTests.cs`
- Create: `PostureOverlayApp/Models/OverlayState.cs`
- Create: `PostureOverlayApp/Models/AppConfig.cs`

**TDD Step 1: 编写 DeviceStateTests（RED）**

```csharp
using Xunit;
using PostureOverlayApp.Models;

namespace PostureOverlayApp.Models;

public class DeviceStateTests
{
    [Fact]
    public void NewInstance_HasDefaultValues()
    {
        var state = new DeviceState();
        Assert.Equal(DeviceConnectionState.Disconnected, state.ConnectionState);
        Assert.Equal(0, state.RawValue);
        Assert.Equal(0, state.DisplayValue);
        Assert.Null(state.LastReceiveTime);
    }

    [Fact]
    public void DeviceConnectionState_HasAllExpectedValues()
    {
        var states = Enum.GetValues<DeviceConnectionState>();
        Assert.Contains(DeviceConnectionState.Disconnected, states);
        Assert.Contains(DeviceConnectionState.Connecting, states);
        Assert.Contains(DeviceConnectionState.ConnectedIdle, states);
        Assert.Contains(DeviceConnectionState.Receiving, states);
        Assert.Contains(DeviceConnectionState.Timeout, states);
        Assert.Contains(DeviceConnectionState.Fault, states);
    }
}
```

**Step 2: 运行测试（RED）**

```bash
cd PostureOverlayApp
dotnet test --filter "FullyQualifiedName~DeviceStateTests"
```
Expected: FAIL - type not found

**TDD Step 3: 实现 DeviceState（GREEN）**

```csharp
namespace PostureOverlayApp.Models;

public enum DeviceConnectionState
{
    Disconnected,
    Connecting,
    ConnectedIdle,
    Receiving,
    Timeout,
    Fault
}

public class DeviceState
{
    public DeviceConnectionState ConnectionState { get; set; } = DeviceConnectionState.Disconnected;
    public int RawValue { get; set; }
    public double DisplayValue { get; set; }
    public DateTime? LastReceiveTime { get; set; }
    public int ErrorCount { get; set; }
    public string? LastError { get; set; }
}
```

**Step 4: 运行测试（GREEN）**

```bash
dotnet test --filter "FullyQualifiedName~DeviceStateTests"
```
Expected: PASS

**TDD Step 5: 编写 OverlayStateTests（RED）**

```csharp
using Xunit;
using PostureOverlayApp.Models;

namespace PostureOverlayApp.Models;

public class OverlayStateTests
{
    [Fact]
    public void NewInstance_HasDefaultValues()
    {
        var state = new OverlayState();
        Assert.Equal(0, state.MaskOpacity);
        Assert.Equal("#FFFFFF", state.MaskColor);
        Assert.Equal(0, state.EdgeOpacity);
        Assert.Equal(string.Empty, state.MessageText);
        Assert.Equal(0, state.MessageOpacity);
        Assert.Equal(0, state.SeverityLevel);
    }

    [Fact]
    public void FromDisplayLevel_LevelZero_ReturnsMinimalMask()
    {
        var state = OverlayState.FromDisplayLevel(0);
        Assert.True(state.MaskOpacity < 0.1);
        Assert.Equal(string.Empty, state.MessageText);
        Assert.Equal(0, state.SeverityLevel);
    }

    [Fact]
    public void FromDisplayLevel_Level100_ReturnsMaxMask()
    {
        var state = OverlayState.FromDisplayLevel(100);
        Assert.True(state.MaskOpacity > 0.6);
        Assert.NotEmpty(state.MessageText);
        Assert.Equal(3, state.SeverityLevel);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(20, 0)]
    [InlineData(50, 1)]
    [InlineData(79, 2)]
    [InlineData(100, 3)]
    public void FromDisplayLevel_ReturnsCorrectSeverity(int level, int expectedSeverity)
    {
        var state = OverlayState.FromDisplayLevel(level);
        Assert.Equal(expectedSeverity, state.SeverityLevel);
    }
}
```

**Step 6: 运行测试（RED）**

```bash
dotnet test --filter "FullyQualifiedName~OverlayStateTests"
```
Expected: FAIL - method not found

**TDD Step 7: 实现 OverlayState（GREEN）**

```csharp
namespace PostureOverlayApp.Models;

public class OverlayState
{
    public double MaskOpacity { get; set; }
    public string MaskColor { get; set; } = "#FFFFFF";
    public double EdgeOpacity { get; set; }
    public string MessageText { get; set; } = string.Empty;
    public double MessageOpacity { get; set; }
    public int SeverityLevel { get; set; }

    public static OverlayState FromDisplayLevel(double level, int hintStart = 30, int urgentLevel = 80)
    {
        var normalized = level / 100.0;

        // Opacity mapping: low level = subtle, high level = aggressive
        var maskOpacity = 0.05 + Math.Pow(normalized, 1.4) * 0.65;

        // Color: white (cold) -> light gray -> darker gray
        string color;
        if (level < 30)
            color = "#FFFFFF";
        else if (level < 60)
            color = "#E0E0E0";
        else if (level < 80)
            color = "#BDBDBD";
        else
            color = "#9E9E9E";

        // Edge opacity for fog effect
        var edgeOpacity = Math.Pow(normalized, 1.8) * 0.25;

        // Message text based on severity
        string message;
        if (level < hintStart)
            message = string.Empty;
        else if (level < urgentLevel)
            message = "请调整坐姿";
        else
            message = "请立即调整坐姿！";

        // Message opacity: fade in after hintStart
        var messageOpacity = level > hintStart
            ? Math.Min(1.0, (level - hintStart) / 40.0)
            : 0;

        // Severity level
        var severity = level switch
        {
            < 20 => 0,
            < 50 => 1,
            < 80 => 2,
            _ => 3
        };

        return new OverlayState
        {
            MaskOpacity = maskOpacity,
            MaskColor = color,
            EdgeOpacity = edgeOpacity,
            MessageText = message,
            MessageOpacity = messageOpacity,
            SeverityLevel = severity
        };
    }
}
```

**Step 8: 运行测试（GREEN）**

```bash
dotnet test --filter "FullyQualifiedName~OverlayStateTests"
```
Expected: PASS

**TDD Step 9: 编写 AppConfigTests（RED）**

```csharp
using Xunit;
using PostureOverlayApp.Models;

namespace PostureOverlayApp.Models;

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
    }
}
```

**Step 10: 运行测试（RED）**

```bash
dotnet test --filter "FullyQualifiedName~AppConfigTests"
```
Expected: FAIL - type not found

**TDD Step 11: 实现 AppConfig（GREEN）**

```csharp
namespace PostureOverlayApp.Models;

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
}
```

**Step 12: 运行测试（GREEN）**

```bash
dotnet test --filter "FullyQualifiedName~AppConfigTests"
```
Expected: PASS

**Step 13: 运行所有测试**

```bash
dotnet test
```
Expected: All tests PASS

**Step 14: 提交**

```bash
git add PostureOverlayApp/Models/
git commit -m "feat: 添加基础 Model 类 (TDD)"
```

---

## 任务3: 创建目录占位文件

**Files:**
- Create: `PostureOverlayApp/Services/.gitkeep`
- Create: `PostureOverlayApp/ViewModels/.gitkeep`
- Create: `PostureOverlayApp/Utils/.gitkeep`

**Step 1: 验证目录结构**

```bash
find PostureOverlayApp -type d | sort
```

Expected:
```
PostureOverlayApp
PostureOverlayApp/Assets
PostureOverlayApp/Models
PostureOverlayApp/Services
PostureOverlayApp/Utils
PostureOverlayApp/ViewModels
```

**Step 2: 提交**

```bash
git add PostureOverlayApp/Services/.gitkeep PostureOverlayApp/ViewModels/.gitkeep PostureOverlayApp/Utils/.gitkeep
git commit -m "chore: 创建 Services/ViewModels/Utils 目录占位文件"
```

---

## 交付清单

本任务完成后的完整交付物：

| 文件 | 描述 | 对应完整计划章节 |
|------|------|------------------|
| `PostureOverlayApp.csproj` | .NET 8.0 WPF 项目 | 第4章 |
| `App.xaml/cs` | WPF 应用入口 | 第4章 |
| `MainWindow.xaml/cs` | 主控制窗口基础布局 | 第9章 |
| `Models/DeviceState.cs` | 设备状态枚举与模型 | 第7章 |
| `Models/OverlayState.cs` | Overlay 视觉参数模型 | 第8章 |
| `Models/AppConfig.cs` | 应用配置模型 | 第16章 |

**下一步依赖:**
- 成员2 将在 Services 目录实现 SerialService、DeviceProtocol、DeviceStateManager
- 成员3 将在 Services 目录实现 BlurController、ValueMapper、ConfigService
- 成员4 将在 ViewModels 和根目录实现 OverlayViewModel、OverlayWindow
