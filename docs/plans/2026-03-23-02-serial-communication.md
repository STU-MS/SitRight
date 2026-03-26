# SerialService 串口通信层实现计划

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.
>
> **TDD Required:** 所有实现必须遵循 Red-Green-Refactor 循环：
> 1. 先写测试，运行验证 FAIL
> 2. 再写实现，运行验证 PASS
> 3. 重构代码

**Goal:** 实现串口通信服务、设备协议解析、设备状态机

**Architecture:** SerialService 负责底层串口操作，通过事件向上推送完整行数据；DeviceProtocol 负责行解析为整数；DeviceStateManager 管理设备连接状态机（第7章设备状态机）。所有串口接收不在 UI 线程执行，通过事件回调同步到主线程。

**Tech Stack:** .NET 8.0 / WPF / System.IO.Ports / xUnit

**对应完整计划章节:** 第6章 串口协议设计、第7章 设备状态机、第11章 关键模块说明

---

## 任务0: 依赖准备

**Files:**
- Modify: `SitRight/SitRight.csproj`

**Step 1: 添加 System.IO.Ports 依赖**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.IO.Ports" Version="8.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.4">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

**Step 2: 还原包并验证编译**

```bash
cd SitRight
dotnet restore
dotnet build
```
Expected: BUILD SUCCEEDED

**Step 3: 提交**

```bash
git add SitRight/SitRight.csproj
git commit -m "chore: 添加 System.IO.Ports 依赖"
```

---

## 任务1: DeviceProtocol 协议解析

**Files:**
- Create: `SitRight/Services/DeviceProtocol.cs`
- Create: `SitRight.Tests/DeviceProtocolTests.cs`

**TDD Step 1: 编写测试（RED）**

```csharp
using Xunit;
using SitRight.Services;

namespace SitRight.Services;

public class DeviceProtocolTests
{
    private readonly DeviceProtocol _protocol = new();

    [Theory]
    [InlineData("37", 37)]
    [InlineData("0", 0)]
    [InlineData("100", 100)]
    [InlineData("  42  ", 42)]
    [InlineData("99", 99)]
    public void TryParse_ValidInput_ReturnsTrueAndValue(string input, int expected)
    {
        var result = _protocol.TryParse(input, out var value);
        Assert.True(result);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("101")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12.5")]
    [InlineData("12a")]
    public void TryParse_InvalidInput_ReturnsFalse(string input)
    {
        var result = _protocol.TryParse(input, out _);
        Assert.False(result);
    }

    [Fact]
    public void TryParse_NullInput_ReturnsFalse()
    {
        var result = _protocol.TryParse(null!, out _);
        Assert.False(result);
    }
}
```

**Step 2: 运行测试验证失败（RED）**

```bash
cd SitRight
dotnet test --filter "FullyQualifiedName~DeviceProtocolTests"
```
Expected: FAIL - DeviceProtocol not found

**TDD Step 3: 实现 DeviceProtocol（GREEN）**

```csharp
namespace SitRight.Services;

public class DeviceProtocol
{
    /// <summary>
    /// 解析设备协议：输入一行文本，尝试解析为 0~100 的整数
    /// 对应第6章 串口协议设计
    /// </summary>
    public bool TryParse(string? line, out int value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(line))
            return false;

        var trimmed = line.Trim();

        // 第一版协议：纯整数换行格式 "37\n"
        // 参见坐姿矫正仪开发计划 第6章
        if (!int.TryParse(trimmed, out value))
            return false;

        // 校验范围 [0, 100]
        if (value < 0 || value > 100)
            return false;

        return true;
    }
}
```

**Step 4: 运行测试验证通过（GREEN）**

```bash
dotnet test --filter "FullyQualifiedName~DeviceProtocolTests"
```
Expected: PASS

**Step 5: 提交**

```bash
git add SitRight/Services/DeviceProtocol.cs SitRight.Tests/DeviceProtocolTests.cs
git commit -m "feat: 实现 DeviceProtocol 协议解析 (TDD)"
```

---

## 任务2: SerialService 串口服务

**Files:**
- Create: `SitRight/Services/ISerialService.cs`
- Create: `SitRight/Services/SerialService.cs`
- Create: `SitRight.Tests/SerialServiceTests.cs`

**TDD Step 1: 编写接口测试（RED）**

```csharp
using Xunit;

namespace SitRight.Services;

public class ISerialServiceTests
{
    [Fact]
    public void ISerialService_DefinesRequiredMembers()
    {
        var type = typeof(ISerialService);
        Assert.True(type.IsInterface);
        Assert.NotNull(type.GetProperty("IsConnected"));
        Assert.NotNull(type.GetProperty("CurrentPort"));
        Assert.NotNull(type.GetEvent("OnLineReceived"));
        Assert.NotNull(type.GetEvent("OnError"));
        Assert.NotNull(type.GetEvent("OnConnected"));
        Assert.NotNull(type.GetEvent("OnDisconnected"));
    }

    [Fact]
    public void SerialService_ImplementsISerialService()
    {
        Assert.True(typeof(ISerialService).IsAssignableFrom(typeof(SerialService)));
    }
}
```

**Step 2: 运行测试（RED）**

```bash
dotnet test --filter "FullyQualifiedName~ISerialServiceTests"
```
Expected: FAIL - ISerialService not found

**TDD Step 3: 实现 ISerialService 接口（GREEN）**

```csharp
namespace SitRight.Services;

public interface ISerialService : IDisposable
{
    event Action<string>? OnLineReceived;
    event Action<Exception>? OnError;
    event Action? OnConnected;
    event Action? OnDisconnected;

    bool IsConnected { get; }
    string? CurrentPort { get; }
    void Connect(string portName, int baudRate);
    void Disconnect();
    string[] GetAvailablePorts();
}
```

**Step 4: 运行测试（GREEN）**

```bash
dotnet test --filter "FullyQualifiedName~ISerialServiceTests"
```
Expected: PASS

**TDD Step 5: 编写 SerialService 测试（RED）**

```csharp
using Xunit;

namespace SitRight.Services;

public class SerialServiceTests
{
    [Fact]
    public void GetAvailablePorts_ReturnsArray()
    {
        var service = new SerialService();
        var ports = service.GetAvailablePorts();
        Assert.NotNull(ports);
        service.Dispose();
    }

    [Fact]
    public void IsConnected_WhenNotConnected_ReturnsFalse()
    {
        var service = new SerialService();
        Assert.False(service.IsConnected);
        service.Dispose();
    }

    [Fact]
    public void CurrentPort_WhenNotConnected_ReturnsNull()
    {
        var service = new SerialService();
        Assert.Null(service.CurrentPort);
        service.Dispose();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var service = new SerialService();
        service.Dispose();
        var exception = Record.Exception(() => service.Dispose());
        Assert.Null(exception);
    }
}
```

**Step 6: 运行测试（RED）**

```bash
dotnet test --filter "FullyQualifiedName~SerialServiceTests"
```
Expected: FAIL - SerialService not found

**TDD Step 7: 实现 SerialService（GREEN）**

```csharp
using System.IO.Ports;
using System.Text;

namespace SitRight.Services;

/// <summary>
/// 串口服务：负责打开串口、接收数据、按行分割、异常处理
/// 对应第11章关键模块说明 - SerialService职责
/// </summary>
public class SerialService : ISerialService
{
    private SerialPort? _serialPort;
    private StringBuilder _buffer = new();
    private bool _disposed;

    public event Action<string>? OnLineReceived;
    public event Action<Exception>? OnError;
    public event Action? OnConnected;
    public event Action? OnDisconnected;

    public bool IsConnected => _serialPort?.IsOpen ?? false;
    public string? CurrentPort => _serialPort?.PortName;

    public string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames();
    }

    public void Connect(string portName, int baudRate)
    {
        Disconnect();

        _serialPort = new SerialPort(portName, baudRate)
        {
            NewLine = "\n",
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };

        _serialPort.DataReceived += OnDataReceived;
        _serialPort.ErrorReceived += OnErrorReceived;

        _serialPort.Open();
        OnConnected?.Invoke();
    }

    public void Disconnect()
    {
        if (_serialPort != null)
        {
            _serialPort.DataReceived -= OnDataReceived;
            _serialPort.ErrorReceived -= OnErrorReceived;

            if (_serialPort.IsOpen)
                _serialPort.Close();

            _serialPort.Dispose();
            _serialPort = null;
            OnDisconnected?.Invoke();
        }
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var port = (SerialPort)sender;
            var data = port.ReadExisting();
            _buffer.Append(data);

            string buffer = _buffer.ToString();
            int newlineIndex;
            while ((newlineIndex = buffer.IndexOf('\n')) >= 0)
            {
                var line = buffer.Substring(0, newlineIndex);
                _buffer.Remove(0, newlineIndex + 1);
                buffer = _buffer.ToString();

                if (!string.IsNullOrEmpty(line))
                    OnLineReceived?.Invoke(line);
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
        }
    }

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        OnError?.Invoke(new Exception($"Serial error: {e.EventType}"));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Disconnect();
            _disposed = true;
        }
    }
}
```

**Step 8: 运行测试（GREEN）**

```bash
dotnet test --filter "FullyQualifiedName~SerialServiceTests"
```
Expected: PASS

**Step 9: 提交**

```bash
git add SitRight/Services/ISerialService.cs SitRight/Services/SerialService.cs SitRight.Tests/SerialServiceTests.cs
git commit -m "feat: 实现 SerialService 串口服务 (TDD)"
```

---

## 任务3: DeviceStateManager 设备状态机

**Files:**
- Create: `SitRight/Services/DeviceStateManager.cs`
- Create: `SitRight.Tests/DeviceStateManagerTests.cs`

**对应完整计划章节:** 第7章 设备状态机设计

**TDD Step 1: 编写状态机测试（RED）**

```csharp
using Xunit;
using SitRight.Services;
using SitRight.Models;

namespace SitRight.Services;

public class DeviceStateManagerTests
{
    private readonly DeviceStateManager _manager;

    public DeviceStateManagerTests()
    {
        _manager = new DeviceStateManager(timeoutMs: 2000);
    }

    [Fact]
    public void InitialState_IsDisconnected()
    {
        Assert.Equal(DeviceConnectionState.Disconnected, _manager.State.ConnectionState);
    }

    [Fact]
    public void OnConnecting_TransitionsToConnecting()
    {
        _manager.OnConnecting();
        Assert.Equal(DeviceConnectionState.Connecting, _manager.State.ConnectionState);
    }

    [Fact]
    public void OnConnected_FromConnecting_TransitionsToConnectedIdle()
    {
        _manager.OnConnecting();
        _manager.OnConnected();
        Assert.Equal(DeviceConnectionState.ConnectedIdle, _manager.State.ConnectionState);
    }

    [Fact]
    public void ReceiveRawValue_FromConnectedIdle_TransitionsToReceiving()
    {
        _manager.OnConnecting();
        _manager.OnConnected();
        _manager.ReceiveRawValue(50);

        Assert.Equal(DeviceConnectionState.Receiving, _manager.State.ConnectionState);
        Assert.Equal(50, _manager.State.RawValue);
    }

    [Fact]
    public void ReceiveRawValue_UpdatesRawValue()
    {
        _manager.ReceiveRawValue(37);
        Assert.Equal(37, _manager.State.RawValue);
    }

    [Fact]
    public void ReceiveRawValue_UpdatesLastReceiveTime()
    {
        _manager.ReceiveRawValue(50);
        Assert.NotNull(_manager.State.LastReceiveTime);
    }

    [Fact]
    public void Disconnect_TransitionsToDisconnected()
    {
        _manager.OnConnecting();
        _manager.OnConnected();
        _manager.Disconnect();

        Assert.Equal(DeviceConnectionState.Disconnected, _manager.State.ConnectionState);
        Assert.Equal(0, _manager.State.RawValue);
        Assert.Null(_manager.State.LastReceiveTime);
    }

    [Fact]
    public void OnFault_TransitionsToFault()
    {
        _manager.OnConnecting();
        _manager.OnConnected();
        _manager.OnFault("Test error");

        Assert.Equal(DeviceConnectionState.Fault, _manager.State.ConnectionState);
        Assert.Equal("Test error", _manager.State.LastError);
        Assert.Equal(1, _manager.State.ErrorCount);
    }

    [Fact]
    public void OnStateChanged_EventIsRaised()
    {
        var changedCount = 0;
        _manager.OnStateChanged += _ => changedCount++;

        _manager.ReceiveRawValue(50);

        Assert.Equal(1, changedCount);
    }
}
```

**Step 2: 运行测试（RED）**

```bash
dotnet test --filter "FullyQualifiedName~DeviceStateManagerTests"
```
Expected: FAIL - DeviceStateManager not found

**TDD Step 3: 实现 DeviceStateManager（GREEN）**

```csharp
using SitRight.Models;

namespace SitRight.Services;

/// <summary>
/// 设备状态管理器：实现第7章定义的设备状态机
/// Disconnected -> Connecting -> ConnectedIdle -> Receiving -> Timeout/Fault -> Disconnected
/// </summary>
public class DeviceStateManager
{
    private readonly object _lock = new();
    private DateTime? _lastReceiveTime;
    private readonly int _timeoutMs;

    public event Action<DeviceState>? OnStateChanged;

    public DeviceState State { get; private set; } = new();

    public DeviceStateManager(int timeoutMs = 2000)
    {
        _timeoutMs = timeoutMs;
    }

    public void OnConnecting()
    {
        lock (_lock)
        {
            UpdateState(s => s.ConnectionState = DeviceConnectionState.Connecting);
        }
    }

    public void OnConnected()
    {
        lock (_lock)
        {
            UpdateState(s =>
            {
                s.ConnectionState = DeviceConnectionState.ConnectedIdle;
                s.LastReceiveTime = null;
            });
        }
    }

    public void OnDisconnected()
    {
        lock (_lock)
        {
            UpdateState(s =>
            {
                s.ConnectionState = DeviceConnectionState.Disconnected;
                s.RawValue = 0;
                s.DisplayValue = 0;
                s.LastReceiveTime = null;
            });
        }
    }

    public void ReceiveRawValue(int value)
    {
        lock (_lock)
        {
            _lastReceiveTime = DateTime.Now;
            UpdateState(s =>
            {
                s.ConnectionState = DeviceConnectionState.Receiving;
                s.RawValue = value;
                s.LastReceiveTime = _lastReceiveTime;
            });
        }
    }

    public void OnTimeout()
    {
        lock (_lock)
        {
            UpdateState(s =>
            {
                if (s.ConnectionState == DeviceConnectionState.Receiving ||
                    s.ConnectionState == DeviceConnectionState.ConnectedIdle)
                {
                    s.ConnectionState = DeviceConnectionState.Timeout;
                }
            });
        }
    }

    public void OnFault(string error)
    {
        lock (_lock)
        {
            UpdateState(s =>
            {
                s.ConnectionState = DeviceConnectionState.Fault;
                s.LastError = error;
                s.ErrorCount++;
            });
        }
    }

    public void Disconnect()
    {
        lock (_lock)
        {
            UpdateState(s =>
            {
                s.ConnectionState = DeviceConnectionState.Disconnected;
                s.RawValue = 0;
                s.DisplayValue = 0;
                s.LastReceiveTime = null;
            });
        }
    }

    /// <summary>
    /// 检查是否超时 - 由定时器调用
    /// </summary>
    public void CheckTimeout()
    {
        lock (_lock)
        {
            if (_lastReceiveTime.HasValue &&
                (DateTime.Now - _lastReceiveTime.Value).TotalMilliseconds > _timeoutMs)
            {
                if (State.ConnectionState == DeviceConnectionState.Receiving)
                {
                    UpdateState(s => s.ConnectionState = DeviceConnectionState.Timeout);
                }
            }
        }
    }

    private void UpdateState(Action<DeviceState> update)
    {
        update(State);
        OnStateChanged?.Invoke(State);
    }
}
```

**Step 4: 运行测试（GREEN）**

```bash
dotnet test --filter "FullyQualifiedName~DeviceStateManagerTests"
```
Expected: PASS

**Step 5: 提交**

```bash
git add SitRight/Services/DeviceStateManager.cs SitRight.Tests/DeviceStateManagerTests.cs
git commit -m "feat: 实现 DeviceStateManager 设备状态机 (TDD)"
```

---

## 任务4: 集成到 MainWindow（占位符）

**Files:**
- Modify: `SitRight/MainWindow.xaml.cs`

**说明:** 此任务在任务B中进行基础集成，后续由任务C和任务D完善

**Step 1: 添加服务引用到 MainWindow（GREEN - 最小集成）**

```csharp
using System.Windows;
using SitRight.Services;
using SitRight.Models;

namespace SitRight;

public partial class MainWindow : Window
{
    // 串口服务实例 - 供后续任务使用
    public SerialService SerialService { get; } = new();
    public DeviceProtocol Protocol { get; } = new();
    public DeviceStateManager StateManager { get; } = new();

    private System.Windows.Threading.DispatcherTimer? _timeoutTimer;

    public MainWindow()
    {
        InitializeComponent();
        InitializeTimeoutTimer();
        Log("应用程序已启动 - 串口通信层已加载");
    }

    private void InitializeTimeoutTimer()
    {
        _timeoutTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timeoutTimer.Tick += (s, e) => StateManager.CheckTimeout();
        _timeoutTimer.Start();
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
git commit -m "feat: 集成串口服务到 MainWindow"
```

---

## 交付清单

本任务完成后的完整交付物：

| 文件 | 描述 | 对应完整计划章节 |
|------|------|------------------|
| `Services/ISerialService.cs` | 串口服务接口 | 第11章 |
| `Services/SerialService.cs` | 串口连接/接收/异常处理 | 第6章、第11章 |
| `SitRight.Tests/SerialServiceTests.cs` | 串口服务测试 | - |
| `Services/DeviceProtocol.cs` | 协议解析（行→整数） | 第6章 |
| `SitRight.Tests/DeviceProtocolTests.cs` | 协议解析测试 | - |
| `Services/DeviceStateManager.cs` | 设备状态机 | 第7章 |
| `SitRight.Tests/DeviceStateManagerTests.cs` | 状态机测试 | - |
| `SitRight/MainWindow.xaml.cs` | 主窗口最小接线（串口连接、状态显示、超时检查） | 第9章 |

**服务接口事件（供任务C/D订阅）:**
```csharp
// SerialService 事件
OnLineReceived(string line)      // 收到完整一行
OnConnected()                     // 连接建立
OnDisconnected()                  // 连接断开
OnError(Exception ex)            // 串口错误

// DeviceStateManager 事件
OnStateChanged(DeviceState state) // 状态变更
```

**下一步依赖:**
- 任务C 将使用 SerialService.OnLineReceived 事件，订阅后推送值到 BlurController
- 任务D 将使用 DeviceState 在 UI 中显示状态
