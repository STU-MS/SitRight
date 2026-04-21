# 向量投影校准方案设计

## 背景

当前固件使用 `atan2(-ax, sqrt(ay² + az²))` 计算 MPU6050 X 轴的倾斜角作为姿态指标。这个方案存在三个问题：

1. **安装方向依赖**：固定测量 X 轴，只有传感器垂直于皮肤安装才能正确工作。实际需要平贴在腰背部。
2. **`fabs()` 丢失方向**：`computeBlurLevel` 使用绝对值偏差，无法区分前倾和后仰。
3. **PC 端维度不匹配**：ValueMapper 把 0-100 blurLevel 当作角度与校准角度比较，逻辑混乱。

## 设计目标

- 传感器可任意方向平贴在腰背部，不限制安装方式
- 后仰自动不触发遮罩
- 符合"硬件负责计算，PC 只负责渲染"的设计哲学
- 简化 PC 端架构

## 方案：三轴向量投影

### 核心思路

不预设哪个轴是"驼背轴"。校准时记录两个姿态的完整重力向量，它们的差值自动定义驼背方向。运行时将当前姿态投影到这个方向上，得到 0-1 的比值。

- 投影 = 0：和坐正一样
- 投影 = 1：和驼背一样
- 投影 < 0：后仰（不触发）
- 投影 > 1：超过驼背（封顶）

---

## 固件改动

### EEPROM 结构（version 0x02）

```
struct CalibData {
  uint16_t magic;       // 0xA5A5
  uint8_t  version;     // 0x02
  float    normalX, normalY, normalZ;   // 坐正姿态重力向量
  float    slouchX, slouchY, slouchZ;   // 驼背姿态重力向量
  uint8_t  checksum;
};
```

不兼容旧版（version 0x01）数据。读到旧版或无效数据时使用默认空值，进入等待校准模式。

### 校准流程

1. 收到 `CMD:SET_NORMAL`：采样 10 次取平均，归一化为单位向量，存入 EEPROM
2. 收到 `CMD:SET_SLOUCH`：同样采样归一化，存入 EEPROM
3. ACK 格式简化为 `ACK:SET_NORMAL\n` 和 `ACK:SET_SLOUCH\n`（不带 ANGLE 字段）

### 校准状态与工作模式

| EEPROM 状态 | 固件行为 |
|------------|---------|
| 无有效数据 | 只监听串口命令，不读传感器，不发数据 |
| 只有 normal | 只监听命令，不发数据 |
| 两个都有 | 正常工作：采集传感器，计算并发送 0-100 |

### 启动逻辑

1. 读取 EEPROM，校验 magic + version + checksum
2. 有效且两个向量都有 → 正常工作模式
3. 无效或不完整 → 等待校准模式（只监听串口命令）

### 运行时算法（每 200ms）

```
1. 读取加速度计 (AcX, AcY, AcZ)
2. 归一化：current = (ax, ay, az) / |(ax, ay, az)|
3. deviation = current - normal
4. axis = slouch - normal
5. projection = dot(deviation, axis) / dot(axis, axis)
6. x = clamp(projection, 0.0, 1.2)   // 负值截断=后仰不触发
7. 三段非线性映射（保持现有曲线）：
   - x in [0, 0.3]    → blur 0~30   (exponent 1.6)
   - x in (0.3, 0.7]  → blur 30~70  (exponent 1.2)
   - x in (0.7, 1.2]  → blur 70~100 (exponent 0.8)
8. EMA 平滑（alpha=0.25）
9. 输出整数 0-100
```

### 删除的固件逻辑

- `angleY = atan2(-ax, sqrt(ay*ay + az*az))` — 不再使用单轴角度
- `fabs(angle - calibNormal)` — 不再使用绝对值偏差
- 默认校准值 `DEFAULT_NORMAL=0.0, DEFAULT_SLOUCH=15.0` — 不再有意义的默认值
- `OUTPUT_MODE_ANGLE` 编译开关 — 只保留一种输出模式（0-100 blurLevel）

---

## PC 端改动

### ValueMapper 简化

删除校准角度字段（`_normalAngle`、`_slouchAngle`）和线性插值逻辑。`Map` 方法变为：

```csharp
public OverlayState Map(int level)
{
    return OverlayState.FromDisplayLevel(level, _hintStartLevel, _urgentLevel);
}
```

删除 `SetCalibration()` 方法。

### CalibrationData 简化

删除 `NormalAngle` / `SlouchAngle` 属性。只保留：
- `State`：状态机（NotCalibrated → NormalSet → FullyCalibrated → Error）
- `LastError`：错误信息
- `ApplyAck` / `ApplyError` / `Reset`：状态转换方法

`ApplyAck` 不再解析 ANGLE 字段，只根据 Command 更新状态：
- `SET_NORMAL` → State = NormalSet
- `SET_SLOUCH` → State = FullyCalibrated
- `RESET` → State = NotCalibrated

### 校准状态恢复

PC 重启后需要知道固件是否已校准。利用一个关键事实：**未校准的固件不发数据，已校准的固件才会发 RuntimeData**。

恢复逻辑：
1. PC 连接固件后，如果收到 RuntimeData → 固件必然已校准 → 自动设置 State = FullyCalibrated
2. 如果没收到数据（超时进入 ConnectedIdle）→ 固件未校准 → State 保持 NotCalibrated
3. 无需在 PC 端持久化校准状态，固件的 EEPROM 是唯一真相来源

MainViewModel 的 HandleInputValue 中：
- 收到 RuntimeData 时，如果 CalibrationData.State 不是 FullyCalibrated，自动提升为 FullyCalibrated 并刷新 UI
- 这替代了之前从 config.json 恢复校准状态的逻辑

### CalibrationService 简化

不再需要持久化任何校准数据到 config.json。固件 EEPROM 是唯一真相来源，PC 重启后通过"是否收到 RuntimeData"自动推断校准状态。

可以大幅简化或直接删除 CalibrationService。如果保留，只作为 CalibrationData.ApplyAck 的薄包装。

### AppConfig 简化

删除 `CalibratedNormalAngle` / `CalibratedSlouchAngle` / `CalibratedAt`。PC 不再持久化校准信息。

### MainViewModel 简化

- 删除 `NormalAngleText` / `SlouchAngleText` 属性及其 setter
- 删除 `SyncCalibrationToValueMapper` 方法（ValueMapper 不再需要校准数据）
- `HandleInputValue`：直接调 `_valueMapper.Map(value)`，不再检查 CalibrationData.State（固件未校准不会发数据）
- `HandleCalibrationAck`：只更新 CalibrationData 状态和 UI，不再存角度
- 保留 `OnCalibrationChanged` 事件用于 UI 刷新校准状态

### MainWindow.xaml 简化

删除坐正/驼背角度的 TextBlock，只保留校准状态文本和两个校准按钮。

### 删除汇总

| 删除项 | 涉及文件 |
|--------|---------|
| 校准角度字段 | ValueMapper, CalibrationData, AppConfig |
| 角度持久化逻辑 | CalibrationService, AppConfig |
| 角度 UI 显示 | MainWindow.xaml, MainViewModel |
| 线性插值映射 | ValueMapper.Map |
| 后仰判断逻辑 | ValueMapper.Map |
| SetCalibration 方法 | ValueMapper |
| SyncCalibrationToValueMapper | MainViewModel |
| NormalAngleText / SlouchAngleText | MainViewModel, MainWindow.xaml |
| RestoreCalibrationFromConfig | MainViewModel（不再从 config 恢复校准） |
| CalibratedAt | CalibrationData, AppConfig（不再 PC 端持久化） |
| PersistCalibration | MainViewModel（不再存角度到 config） |

---

## 不变的部分

- 串口通信层（SerialService, ISerialService, DeviceProtocol）：协议格式不变，RuntimeData 仍然是 0-100 整数
- DeviceStateManager：状态机不变
- OverlayState / OverlayWindow：视觉渲染逻辑不变
- 三段非线性映射曲线：参数不变（0-30-70-100, exponents 1.6/1.2/0.8）
- EMA 平滑参数：alpha=0.25 不变
- 测试框架：xUnit + Moq，现有测试按新接口更新

## 校准 ACK 协议变更

| | 旧格式 | 新格式 |
|---|--------|--------|
| SET_NORMAL | `ACK:SET_NORMAL,ANGLE:12.34` | `ACK:SET_NORMAL` |
| SET_SLOUCH | `ACK:SET_SLOUCH,ANGLE:14.80` | `ACK:SET_SLOUCH` |

ERR 格式不变。
