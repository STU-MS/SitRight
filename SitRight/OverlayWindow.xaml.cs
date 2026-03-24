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

            //启动时强制完全透明
            MaskRect.Opacity = 0;
            EdgeRect.Opacity = 0;

            RootGrid.IsHitTestVisible = false;
        }

        public void ApplyState(OverlayState state)
        {
            MaskRect.Opacity = state.MaskOpacity;
            MaskRect.Fill = (Brush)new BrushConverter().ConvertFromString(state.MaskColor);

            EdgeRect.Opacity = state.EdgeOpacity;


            // 🔥 核心：控制是否穿透（真正生效的地方）
            SetClickThrough(!state.BlockInput);
        }
    }
}