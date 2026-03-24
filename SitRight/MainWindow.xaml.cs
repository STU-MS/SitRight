using System;
using System.Windows;
using SitRight.Models;

namespace SitRight;
public partial class MainWindow : Window
{
    private bool _isSimulationMode = false;
    private OverlayWindow _overlay;
    
    public MainWindow()
    {
        InitializeComponent();
        
        _overlay = new OverlayWindow();
        _overlay.Show();
        
        Log("应用程序已启动");
    }
    
    private void SimulationModeChanged(object sender, RoutedEventArgs e)
    {
        _isSimulationMode = SimulationModeCheckBox.IsChecked == true;

        SimulatedValueSlider.IsEnabled = _isSimulationMode;

        if (_isSimulationMode)
        {
            // 开启模拟 → 立即应用当前值
            var level = SimulatedValueSlider.Value;
            var state = OverlayState.FromDisplayLevel(level);
            _overlay.ApplyState(state);
        }
        else
        {
            // 关闭模拟 → 清空遮罩
            _overlay.ApplyState(new OverlayState
            {
                MaskOpacity = 0,
                EdgeOpacity = 0,
                MessageOpacity = 0,
                MessageText = "",
                BlockInput = false
            });
        }
    }
    
    private void SimulatedValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        double level = e.NewValue;

        SimulatedValueText.Text = ((int)level).ToString();

        var state = OverlayState.FromDisplayLevel(level);
        _overlay.ApplyState(state);
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

    protected void UpdateStatus(string status, string color = "Gray")
    {
        Dispatcher.Invoke(() => StatusText.Text = status);
    }
}
