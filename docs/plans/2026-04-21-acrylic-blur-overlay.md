# Acrylic Blur Overlay Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 将白色遮罩替换为 Windows 原生亚克力高斯模糊效果。

**Architecture:** 通过 `SetWindowCompositionAttribute` P/Invoke 启用系统级亚克力模糊，`OverlayState` 新增 `BlurEnabled` 控制开关，`FromDisplayLevel` 映射产生 ARGB 半透明色控制模糊浓度。移除旧的白色矩形遮罩。

**Tech Stack:** WPF/.NET 8, P/Invoke (user32.dll SetWindowCompositionAttribute), xUnit

---

### Task 1: 更新 OverlayState 模型

**Files:**
- Modify: `SitRight/Models/OverlayState.cs`
- Test: `SitRight.Tests/OverlayStateTests.cs`

**Step 1: 写失败测试 — 新属性和映射**

在 `OverlayStateTests.cs` 中添加：

```csharp
[Fact]
public void FromDisplayLevel_LevelZero_BlurDisabled()
{
    var state = OverlayState.FromDisplayLevel(0);
    Assert.False(state.BlurEnabled);
}

[Fact]
public void FromDisplayLevel_LevelAboveZero_BlurEnabled()
{
    var state = OverlayState.FromDisplayLevel(1);
    Assert.True(state.BlurEnabled);
}

[Theory]
[InlineData(0, false)]
[InlineData(1, true)]
[InlineData(50, true)]
[InlineData(100, true)]
public void FromDisplayLevel_BlurEnabled_MatchesLevel(int level, bool expected)
{
    var state = OverlayState.FromDisplayLevel(level);
    Assert.Equal(expected, state.BlurEnabled);
}

[Theory]
[InlineData(0)]
[InlineData(50)]
[InlineData(100)]
public void FromDisplayLevel_MaskOpacity_AlwaysBetween0And1(int level)
{
    var state = OverlayState.FromDisplayLevel(level);
    Assert.InRange(state.MaskOpacity, 0.0, 1.0);
}

[Fact]
public void NewInstance_HasBlurDisabled()
{
    var state = new OverlayState();
    Assert.False(state.BlurEnabled);
}
```

同时更新 `NewInstance_HasDefaultValues` 测试，断言 `BlurEnabled` 默认为 false。

**Step 2: 运行测试确认失败**

Run: `cd SitRight.Tests && dotnet test --filter "FullyQualifiedName~OverlayStateTests" -v n`
Expected: FAIL（`BlurEnabled` 属性不存在）

**Step 3: 实现 — 更新 OverlayState**

在 `OverlayState.cs` 中：

1. 新增属性 `public bool BlurEnabled { get; set; }`
2. 修改 `FromDisplayLevel`：
   - 设置 `BlurEnabled = level > 0`
   - `MaskColor` 改为 ARGB 格式（如 `#00FFFFFF` 表示完全透明的白色）
   - Alpha 通道随 level 增大而增大（控制模糊遮罩浓度）
   - 保留 `MaskOpacity` 用于渐进曲线
   - 保留 `EdgeOpacity`、`MessageText`、`MessageOpacity`、`SeverityLevel` 不变

`FromDisplayLevel` 新映射逻辑：

```csharp
public static OverlayState FromDisplayLevel(double level, int hintStart = 30, int urgentLevel = 80)
{
    level = Math.Clamp(level, 0, 100);
    var normalized = level / 100.0;

    // Alpha: 控制亚克力遮罩的浓度（0=透明, 255=不透明）
    byte alpha = (byte)(Math.Pow(normalized, 2.0) * 200);
    if (level > 95)
        alpha = 230;

    string color = $"#{alpha:X2}FFFFFF"; // ARGB: 半透明白色

    var maskOpacity = 0.02 + Math.Pow(normalized, 2.8) * 0.98;
    if (level > 95)
        maskOpacity = 1.0;

    var edgeOpacity = Math.Pow(normalized, 1.8) * 0.25;

    string messageText = string.Empty;
    double messageOpacity = 0;

    if (level >= urgentLevel)
    {
        messageText = "请立即调整坐姿！";
        messageOpacity = 1;
    }
    else if (level >= hintStart)
    {
        messageText = "请调整坐姿";
        var range = Math.Max(1, urgentLevel - hintStart);
        messageOpacity = 0.3 + ((level - hintStart) / range) * 0.5;
    }

    var severity = level switch
    {
        <= 20 => 0,
        <= 50 => 1,
        <= 79 => 2,
        _ => 3
    };

    return new OverlayState
    {
        MaskOpacity = maskOpacity,
        MaskColor = color,
        EdgeOpacity = edgeOpacity,
        MessageText = messageText,
        MessageOpacity = messageOpacity,
        SeverityLevel = severity,
        BlurEnabled = level > 0,
    };
}
```

**Step 4: 运行测试确认通过**

Run: `cd SitRight.Tests && dotnet test --filter "FullyQualifiedName~OverlayStateTests" -v n`
Expected: ALL PASS

**Step 5: 提交**

```bash
git add SitRight/Models/OverlayState.cs SitRight.Tests/OverlayStateTests.cs
git commit -m "feat: add BlurEnabled to OverlayState, update color mapping to ARGB"
```

---

### Task 2: 更新 ValueMapper 和 OverlayViewModel 测试

**Files:**
- Modify: `SitRight.Tests/ValueMapperTests.cs`
- Modify: `SitRight.Tests/OverlayViewModelTests.cs`

**Step 1: 更新 ValueMapper 颜色测试**

`Map_Level_ReturnsCorrectColor` 测试需要更新预期颜色为 ARGB 格式：

```csharp
[Theory]
[InlineData(0, false)]
[InlineData(50, true)]
[InlineData(100, true)]
public void Map_BlurEnabled_MatchesLevel(int level, bool expected)
{
    var state = _mapper.Map(level);
    Assert.Equal(expected, state.BlurEnabled);
}
```

移除旧的 `Map_Level_ReturnsCorrectColor`（颜色现在是 ARGB 动态计算，不再按阶梯断言）。

**Step 2: 运行测试**

Run: `cd SitRight.Tests && dotnet test --filter "FullyQualifiedName~ValueMapperTests" -v n`
Expected: PASS

**Step 3: 更新 OverlayViewModel 测试**

`InitialState_HasDefaultColor` 改为断言 `BlurEnabled` 为 false。移除 `UpdateFrom_RespectsSeverityColor`（颜色逻辑已在 OverlayState 测试中覆盖）。

新增：

```csharp
[Fact]
public void UpdateFrom_BlurEnabled_IsSet()
{
    var vm = new OverlayViewModel();
    var state = OverlayState.FromDisplayLevel(50);
    vm.UpdateFrom(state);
    Assert.True(vm.BlurEnabled);
}
```

**Step 4: 运行测试**

Run: `cd SitRight.Tests && dotnet test --filter "FullyQualifiedName~OverlayViewModelTests" -v n`
Expected: FAIL（`BlurEnabled` 属性在 ViewModel 中不存在）

**Step 5: 更新 OverlayViewModel**

新增属性：

```csharp
private bool _blurEnabled;

public bool BlurEnabled
{
    get => _blurEnabled;
    set => SetProperty(ref _blurEnabled, value);
}
```

在 `UpdateFrom` 中添加：

```csharp
BlurEnabled = state.BlurEnabled;
```

**Step 6: 运行全部测试**

Run: `cd SitRight.Tests && dotnet test -v n`
Expected: ALL PASS

**Step 7: 提交**

```bash
git add SitRight/ViewModels/OverlayViewModel.cs SitRight.Tests/ValueMapperTests.cs SitRight.Tests/OverlayViewModelTests.cs
git commit -m "feat: add BlurEnabled to OverlayViewModel, update mapper/viewmodel tests"
```

---

### Task 3: 实现 OverlayWindow 亚克力效果

**Files:**
- Modify: `SitRight/OverlayWindow.xaml`
- Modify: `SitRight/OverlayWindow.xaml.cs`

**Step 1: 更新 OverlayWindow.xaml**

简化 XAML — 移除 `MaskRect` 和 `EdgeRect`，保留一个用于显示提示文字的容器：

```xml
<Window x:Class="SitRight.OverlayWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        WindowState="Normal">

    <Grid x:Name="RootGrid" IsHitTestVisible="False">
        <!-- 半透明着色层 — 颜色由 ApplyState 动态设置 -->
        <Rectangle x:Name="TintRect"
                   IsHitTestVisible="False" />
    </Grid>
</Window>
```

**Step 2: 更新 OverlayWindow.xaml.cs — 添加亚克力 P/Invoke**

```csharp
using System;
using System.Windows;
using System.Windows.Media;
using SitRight.Models;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace SitRight
{
    public partial class OverlayWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;

        // 亚克力效果相关结构体
        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
            ACCENT_INVALID_STATE = 7
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public uint GradientColor; // ARGB
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        private const int WCA_ACCENT_POLICY = 19;

        private void SetClickThrough(bool enable)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            if (enable)
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
            else
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
        }

        private void EnableAcrylicBlur(IntPtr hwnd, bool enable, uint argbColor)
        {
            var accent = new AccentPolicy
            {
                AccentState = enable ? AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND : AccentState.ACCENT_DISABLED,
                AccentFlags = 2, // ACCENT_FLAG_DRAW_ALL
                GradientColor = argbColor
            };

            var accentSize = Marshal.SizeOf(typeof(AccentPolicy));
            IntPtr accentPtr = Marshal.AllocHGlobal(accentSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WCA_ACCENT_POLICY,
                Data = accentPtr,
                SizeOfData = accentSize
            };

            SetWindowCompositionAttribute(hwnd, ref data);
            Marshal.FreeHGlobal(accentPtr);
        }

        public OverlayWindow()
        {
            InitializeComponent();
            TintRect.Opacity = 0;
            RootGrid.IsHitTestVisible = false;
            MoveToMonitor(0);
        }

        public void MoveToMonitor(int index)
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            var target = index >= 0 && index < screens.Length
                ? screens[index]
                : System.Windows.Forms.Screen.PrimaryScreen ?? screens[0];

            var bounds = target.Bounds;

            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Normal;
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
        }

        public void ApplyState(OverlayState state)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            if (state.BlurEnabled && hwnd != IntPtr.Zero)
            {
                // 从 MaskColor 解析 ARGB
                var color = (Color)ColorConverter.ConvertFromString(state.MaskColor);
                uint argb = (uint)(color.A << 24) | (uint)(color.R << 16) | (uint)(color.G << 8) | color.B;
                EnableAcrylicBlur(hwnd, true, argb);
                TintRect.Opacity = 0; // 亚克力效果由系统渲染
            }
            else
            {
                EnableAcrylicBlur(hwnd, false, 0);
                TintRect.Opacity = 0;
            }

            SetClickThrough(!state.BlockInput);
        }
    }
}
```

**Step 3: 手动测试**

Run: `cd SitRight && dotnet run`
Expected: 连接设备或开启模拟模式后，遮罩从白色变为半透明模糊效果

**Step 4: 提交**

```bash
git add SitRight/OverlayWindow.xaml SitRight/OverlayWindow.xaml.cs
git commit -m "feat: replace white mask with Windows Acrylic blur effect"
```

---

### Task 4: 清理遗留测试和验证全量测试

**Files:**
- Modify: `SitRight.Tests/OverlayStateTests.cs`（清理旧断言）
- Modify: `SitRight.Tests/ValueMapperTests.cs`（最终清理）

**Step 1: 运行全量测试**

Run: `cd SitRight.Tests && dotnet test -v n`
Expected: ALL PASS

**Step 2: 检查是否有测试引用了被移除的旧字段**

搜索 `EdgeRect`、`MaskRect`、`#E0E0E0`、`#BDBDBD`、`#9E9E9E` 在测试中的引用并更新。

**Step 3: 最终全量验证**

Run: `cd SitRight.Tests && dotnet test -v n`
Expected: ALL PASS

**Step 4: 提交**

```bash
git add -A
git commit -m "refactor: clean up legacy mask tests for acrylic blur overlay"
```
