# SitRight - 坐姿矫正仪

一款配合硬件设备的 Windows 桌面坐姿矫正工具。通过 USB 串口接收硬件设备的姿态数据，在屏幕上显示全屏遮罩层——坐姿越差，遮罩越明显，从而提醒用户调整坐姿。

## 功能特性

- 串口连接硬件设备，实时接收姿态数据
- 全屏遮罩覆盖，根据姿态恶化程度逐渐加深
- 支持多显示器，可选择目标屏幕
- 双点校准（正常坐姿 / 弯腰姿势）
- 模拟模式，无需硬件即可测试

## 环境要求

- Windows 10 及以上
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## 运行

```bash
cd SitRight
dotnet run
```

## 打包 EXE

**独立部署（无需安装 .NET 运行时）：**

```bash
cd SitRight
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

产物位于 `SitRight/bin/Release/net8.0-windows/win-x64/publish/SitRight.exe`

**依赖框架（体积更小，需安装 .NET 8 运行时）：**

```bash
cd SitRight
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

## 运行测试

```bash
cd SitRight.Tests
dotnet test
```

## 硬件通信协议

串口参数：115200 8N1

| 方向 | 格式 | 示例 |
|------|------|------|
| 设备 → PC | 运行数据 | `37\n`（blurLevel 0-100） |
| 设备 → PC | 校准确认 | `ACK:SET_NORMAL,ANGLE:12.34\n` |
| PC → 设备 | 校准命令 | `CMD:SET_NORMAL\n` |

## License

MIT
