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

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;

        private void SetClickThrough(bool enable)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            if (enable)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
            }
            else
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
            }
        }

        public OverlayWindow()
        {
            InitializeComponent();

            MaskRect.Opacity = 0;
            EdgeRect.Opacity = 0;
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
            MaskRect.Opacity = state.MaskOpacity;
            var brush = new BrushConverter().ConvertFromString(state.MaskColor) as Brush;
            MaskRect.Fill = brush ?? Brushes.White;

            EdgeRect.Opacity = state.EdgeOpacity;

            SetClickThrough(!state.BlockInput);
        }
    }
}
