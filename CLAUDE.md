# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## TOP RULES
must use superpower:brainstorm
always show a few plans and also give me a lower tech-debt one(if you dont understand you'd better ask user)

## Project Overview

SitRight 是一个 Windows 桌面坐姿矫正工具（WPF/.NET 8），通过 USB 串口连接 Arduino 硬件设备（MPU6050 传感器），实时接收姿态数据并在屏幕上显示全屏遮罩——坐姿越差，遮罩越明显。**仅限 Windows 平台**（依赖 WPF + WinForms）。

## Build & Run Commands

```bash
# 构建
cd SitRight && dotnet build

# 运行
cd SitRight && dotnet run

# 运行全部测试
cd SitRight.Tests && dotnet test

# 运行单个测试
cd SitRight.Tests && dotnet test --filter "FullyQualifiedName~MainViewModelTests.InitialStatus_IsDisconnected"

# 按类名运行测试
cd SitRight.Tests && dotnet test --filter "FullyQualifiedName~DeviceProtocolTests"

# 打包独立部署 EXE
cd SitRight && dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Architecture

数据流：
```
MPU6050 → Arduino(计算blurLevel 0-100) → USB Serial → SerialService → DeviceProtocol(解析) → BlurController(平滑) → ValueMapper(映射) → OverlayWindow(全屏遮罩)
```

### Key Components (SitRight/)

- **Models/**: `AppConfig`(配置+默认值)、`CalibrationData`(校准状态机)、`DeviceState`(连接状态)、`OverlayState`(遮罩视觉参数)
- **Services/**:
  - `SerialService` / `ISerialService` — 串口通信抽象，事件驱动（`OnLineReceived`）
  - `DeviceProtocol` — 文本行协议解析器（运行数据、ACK、ERR 三种消息）
  - `DeviceStateManager` — 设备连接状态机（Disconnected→Connecting→ConnectedIdle→Receiving→Timeout/Fault）
  - `BlurController` — 指数移动平均平滑（alpha=0.18）
  - `ValueMapper` — blurLevel 到 OverlayState 的映射，结合校准角度
  - `CalibrationService` — 校准数据持久化（通过 ConfigService）
  - `ConfigService` — JSON 配置文件读写（config.json，带内存缓存）
- **ViewModels/**: `MainViewModel`（核心协调器）、`OverlayViewModel`（遮罩状态 VM）
- **MainWindow.xaml.cs** — 事件驱动 code-behind，手动绑定 ViewModel 事件到 UI 控件（非严格 MVVM 数据绑定）
- **OverlayWindow.xaml.cs** — 全屏透明置顶窗口，P/Invoke user32.dll 实现点击穿透

### Testing (SitRight.Tests/)

- xUnit 2.5.3 + Moq 4.20.70
- 每个服务/模型对应一个测试文件，覆盖核心业务逻辑
- 临时文件策略：`Path.GetTempPath()` + GUID 命名

## Serial Protocol

115200 8N1，文本行协议（`\n` 分隔）：

| 方向 | 类型 | 格式 |
|------|------|------|
| 设备→PC | 运行数据 | `37\n`（blurLevel 0-100） |
| 设备→PC | 校准确认 | `ACK:SET_NORMAL,ANGLE:12.34\n` |
| 设备→PC | 错误 | `ERR:BUSY\n` |
| PC→设备 | 校准命令 | `CMD:SET_NORMAL\n` / `CMD:SET_SLOUCH\n` |

## Conventions

- 无 DI 容器，在 MainWindow 构造函数中手动实例化服务
- 事件驱动架构：服务间通过 C# events 通信
- Nullable enable 已开启
- UI 字符串均为中文，代码标识符用英文
- `OverlayWindow` 使用 WinForms `System.Windows.Forms.Screen` 实现多显示器选择
- 硬件固件在 `hardware/sitRight_firmware.ino`（Arduino，EEPROM 存储校准数据）
- 无 .sln 文件，两个独立项目（SitRight、SitRight.Tests）
