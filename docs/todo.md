# SitRight 软件端 TODO 清单

> 基于项目文档和代码现状梳理，按实施阶段排序
> 更新日期: 2026-04-03

## 阶段 1: 清理 + 业务逻辑

- [ ] **清理 AppConfig 废弃字段**
  - 移除 `SmoothingAlpha` 和 `DisplayRefreshIntervalMs`（平滑算法已移至硬件端）
  - 更新对应测试 `AppConfigTests.cs`
  - 计划文档: Plan C (`2026-03-23-03-business-logic.md`)

- [ ] **安装 Moq 测试框架**
  - 在 `SitRight.Tests.csproj` 中添加 Moq 4.20.70 依赖
  - ViewModel 测试的前置条件

- [ ] **实现 ValueMapper 服务**
  - 将 `OverlayState.FromDisplayLevel()` 映射逻辑封装为 `Services/ValueMapper.cs`
  - 支持可配置的 hint/urgent 阈值
  - TDD: 先写 `ValueMapperTests.cs`
  - 计划文档: Plan C Task 3

- [ ] **实现 ConfigService**
  - `Services/ConfigService.cs`: JSON 配置文件读写 + 缓存
  - TDD: 先写 `ConfigServiceTests.cs`
  - 计划文档: Plan C Task 4

## 阶段 2: MVVM 重构

- [ ] **实现 OverlayViewModel**
  - `ViewModels/OverlayViewModel.cs`: INotifyPropertyChanged 数据绑定
  - 替代 OverlayWindow code-behind 中的直接赋值
  - TDD: 先写 `OverlayViewModelTests.cs`
  - 计划文档: Plan D Task 8

- [ ] **实现 MainViewModel**
  - `ViewModels/MainViewModel.cs`: 服务编排（SerialService / DeviceStateManager / Overlay 联动）
  - 从 MainWindow code-behind 中抽出业务逻辑
  - TDD: 先写 `MainViewModelTests.cs`（需 Mock ISerialService）
  - 计划文档: Plan D Task 9

- [ ] **MainWindow 迁移至 MVVM 绑定**
  - 用数据绑定替代事件处理中的手动 UI 更新
  - 计划文档: Plan D Task 10

## 阶段 3: 校准协议

- [ ] **添加 SerialService.SendLine()**
  - ISerialService 接口新增 `SendLine(string line)` 方法
  - 支持向 MCU 发送 `CMD:SET_NORMAL` 等指令
  - TDD: 更新 `SerialServiceTests.cs` 和 `ISerialServiceTests.cs`
  - 计划文档: `2026-04-01-serial-calibration-design.md` 第 4 节

- [ ] **扩展 DeviceProtocol 支持 ACK/ERR**
  - 实现 `TryParseFull()` 解析校准响应行
  - 当前 ACK/ERR 行被静默丢弃
  - TDD: 更新 `DeviceProtocolTests.cs`
  - 计划文档: `2026-04-01-serial-calibration-design.md` 第 2 节

- [ ] **实现 CalibrationData 模型**
  - `Models/CalibrationData.cs`: 校准状态机 + 校准数据
  - TDD: 先写 `CalibrationDataTests.cs`
  - 计划文档: `2026-04-01-serial-calibration-design.md` 第 3 节

- [ ] **校准 UI**
  - MainWindow 中添加校准操作界面
  - 计划文档: `2026-04-01-serial-calibration-design.md` 第 5-6 节

## 阶段 4: 收尾

- [ ] **显示器选择功能**
  - OverlayWindow 支持用户选择目标显示器（当前仅主显示器最大化）
  - 计划文档: `2026-04-01-serial-calibration-design.md` 第 7 节

- [ ] **创建 .sln 解决方案文件**
  - 方便 IDE 打开和构建管理
