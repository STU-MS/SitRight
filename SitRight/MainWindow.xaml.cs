using System;
using System.Windows;
using System.Windows.Threading;
using SitRight.Models;
using SitRight.Services;

namespace SitRight;
public partial class MainWindow : Window
{
    private bool _isSimulationMode = false;
    private readonly OverlayWindow _overlay;
    private readonly BlurController _blurController;
    private DispatcherTimer? _displayTimer;
    
    public MainWindow()
    {
        InitializeComponent();

        _overlay = new OverlayWindow();
        _overlay.Show();

        _blurController = new BlurController();
        _blurController.OnDisplayValueChanged += OnBlurDisplayValueChanged;

        _displayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _displayTimer.Tick += (s, e) => _blurController.Tick();
        _displayTimer.Start();

        Log("应用程序已启动");
    }

    private void OnBlurDisplayValueChanged(double displayValue)
    {
        var state = OverlayState.FromDisplayLevel(displayValue);
        _overlay.ApplyState(state);
    }
    
    private void SimulationModeChanged(object sender, RoutedEventArgs e)
    {
        _isSimulationMode = SimulationModeCheckBox.IsChecked == true;

        SimulatedValueSlider.IsEnabled = _isSimulationMode;

        if (_isSimulationMode)
        {
            // 开启模拟 → 推送当前值到 BlurController
            _blurController.PushRawValue((int)SimulatedValueSlider.Value);
        }
        else
        {
            // 关闭模拟 → 重置 BlurController，清空遮罩
            _blurController.Reset();
        }
    }
    
    private void SimulatedValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        double level = e.NewValue;

        SimulatedValueText.Text = ((int)level).ToString();

        // 仅在模拟模式下推送值到 BlurController
        if (_isSimulationMode)
        {
            _blurController.PushRawValue((int)level);
        }
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

    protected override void OnClosed(EventArgs e)
    {
        _displayTimer?.Stop();
        _overlay?.Close();
        base.OnClosed(e);
    }
}
