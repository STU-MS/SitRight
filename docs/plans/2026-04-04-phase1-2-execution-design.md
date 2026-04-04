# 阶段 1-2 执行设计：顺序 TDD 方案

> 目标：跑通核心流程，完成业务逻辑层 + MVVM 重构
> 日期: 2026-04-04
> 状态: 已确认

## 整体架构

```
MCU (平滑处理) -> SerialService -> DeviceProtocol -> ValueMapper -> OverlayViewModel -> OverlayWindow
                       |                              ^
                MainViewModel <- DeviceStateManager  ConfigService
                       |
                MainWindow (MVVM 绑定)
```

## 执行步骤

| # | 任务 | 文件 | 提交信息 |
|---|------|------|----------|
| 1 | 清理 AppConfig 废弃字段 | AppConfig.cs, AppConfigTests.cs | chore: 清理 AppConfig 废弃字段 |
| 2 | 安装 Moq | SitRight.Tests.csproj | chore: 添加 Moq 测试框架 |
| 3 | ValueMapper (TDD) | Services/ValueMapper.cs, ValueMapperTests.cs | feat: 实现 ValueMapper 数值映射器 |
| 4 | ConfigService (TDD) | Services/ConfigService.cs, ConfigServiceTests.cs, config.json | feat: 实现 ConfigService 配置服务 |
| 5 | OverlayViewModel (TDD) | ViewModels/OverlayViewModel.cs, OverlayViewModelTests.cs | feat: 实现 OverlayViewModel |
| 6 | MainViewModel (TDD) | ViewModels/MainViewModel.cs, MainViewModelTests.cs | feat: 实现 MainViewModel |
| 7 | MainWindow 迁移 MVVM | MainWindow.xaml, MainWindow.xaml.cs | feat: MainWindow 迁移至 MVVM 绑定 |

## 约束

- 每步 Red-Green-Refactor TDD
- 参考已有 Plan C/D 代码模板

## 依赖关系

```
步骤1 (AppConfig清理) ──┬──> 步骤3 (ValueMapper) ──> 步骤5 (OverlayViewModel) ──┬──> 步骤7 (MainWindow迁移)
                        └──> 步骤4 (ConfigService) ────────────────────────────────┘
步骤2 (Moq) ──────────────────────────────────────────> 步骤6 (MainViewModel) ───┘
```

## 不包含（阶段 3-4 延后）

- 校准协议 (ACK/ERR, SendLine, CalibrationData)
- 校准 UI
- 显示器选择功能
- .sln 文件创建
