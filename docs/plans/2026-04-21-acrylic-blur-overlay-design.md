# Windows Acrylic 模糊遮罩设计

## 目标

将 OverlayWindow 的纯白色遮罩替换为 Windows 原生亚克力/高斯模糊效果，实现类似 macOS 毛玻璃的视觉体验。

## 方案

使用 Windows `SetWindowCompositionAttribute` API（方案 A），系统级 GPU 加速模糊。

## 架构变更

### 1. 新增 P/Invoke（OverlayWindow.xaml.cs）

```
SetWindowCompositionAttribute(hwnd, ref AccentPolicy)
```

- AccentState = ACCENT_ENABLE_ACRYLICBLURBEHIND (4)
- GradientColor = ARGB 半透明色，Alpha 控制模糊浓度
- blurLevel 越高 → Alpha 越高 → 遮罩越浓

### 2. OverlayState 调整

- 新增 `BlurEnabled` 属性（blurLevel=0 时不启用）
- `MaskColor` 改为 ARGB 半透明色
- `MaskOpacity` 改为控制 Acrylic Alpha 通道

### 3. OverlayWindow.xaml 简化

- 移除 MaskRect（白色矩形）
- 移除 EdgeRect（亚克力自带边缘效果）
- 窗口背景保持透明，模糊由系统 API 处理

### 4. OverlayState.FromDisplayLevel 映射

| blurLevel | 效果 |
|-----------|------|
| 0-30 | 几乎透明，轻微模糊 |
| 30-60 | 半透明 + 明显模糊 |
| 60-80 | 高度模糊，半透明白色叠加 |
| 80+ | 几乎不透明 + 最强模糊 |

### 5. 数据流

```
blurLevel → ValueMapper → OverlayState(BlurEnabled, BlurAlpha, TintColor)
                          → OverlayWindow.ApplyState()
                          → SetWindowCompositionAttribute()
```

## 测试

- OverlayState.FromDisplayLevel 单元测试验证各区间
- P/Invoke 部分手动验证

## 约束

- Windows 10 1803+ 或 Windows 11
- 项目本身已限 Windows 平台，无兼容性问题
