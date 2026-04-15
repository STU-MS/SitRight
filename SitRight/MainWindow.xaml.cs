using System;
using System.Windows;
using System.Windows.Threading;
using SitRight.Models;
using SitRight.Services;

namespace SitRight;

public partial class MainWindow : Window
{
    private bool _isSimulationMode;
    private readonly OverlayWindow _overlay;
    private readonly ConfigService _configService;
    private readonly CalibrationService _calibrationService;
    private readonly BlurController _blurController;
    private readonly DispatcherTimer _displayTimer;

    private AppConfig _config;
    private int _lastRawValue;

    public MainWindow()
    {
        InitializeComponent();

        _configService = new ConfigService();
        _calibrationService = new CalibrationService();
        _config = _configService.Load();
        _blurController = new BlurController(alpha: _config.SmoothingAlpha);
        _blurController.DisplayValueChanged += OnDisplayValueChanged;

        _displayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_config.DisplayRefreshIntervalMs)
        };
        _displayTimer.Tick += (_, _) => _blurController.Tick();
        _displayTimer.Start();

        _overlay = new OverlayWindow();
        _overlay.Show();

        UpdateCalibrationInfo();
        Log("应用程序已启动");
    }

    private void CalibrateButton_Click(object sender, RoutedEventArgs e)
    {
        var baselineSource = _isSimulationMode ? (int)SimulatedValueSlider.Value : _lastRawValue;

        _calibrationService.ApplyCalibration(_config, baselineSource, DateTime.Now);
        _configService.Save(_config);
        UpdateCalibrationInfo();

        _blurController.Reset();
        PushRawValue(baselineSource);
    }

    private void SimulationModeChanged(object sender, RoutedEventArgs e)
    {
        _isSimulationMode = SimulationModeCheckBox.IsChecked == true;
        SimulatedValueSlider.IsEnabled = _isSimulationMode;

        if (_isSimulationMode)
        {
            PushRawValue((int)SimulatedValueSlider.Value);
        }

        _overlay.ApplyState(new OverlayState
        {
            _blurController.Reset();
            _overlay.ApplyState(new OverlayState());
        }
    }

    private void SimulatedValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        SimulatedValueText.Text = ((int)e.NewValue).ToString();

        if (_isSimulationMode)
        {
            PushRawValue((int)e.NewValue);
        }
    }

    private void PushRawValue(int rawValue)
    {
        _lastRawValue = rawValue;
        var normalized = _calibrationService.Normalize(rawValue, _config.CalibrationBaseline);
        _blurController.PushRawValue(normalized);
    }

    private void OnDisplayValueChanged(double displayValue)
    {
        Dispatcher.Invoke(() =>
        {
            DisplayValueText.Text = displayValue.ToString("F1");
            _overlay.ApplyState(OverlayState.FromDisplayLevel(
                displayValue,
                _config.HintStartLevel,
                _config.UrgentLevel));
        });
    }

    private void UpdateCalibrationInfo()
    {
        CalibrationBaselineText.Text = _config.CalibrationBaseline.ToString();
        CalibrationTimeText.Text = _config.CalibratedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未校准";
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

    private void UpdateStatus(DeviceState state)
    {
        StatusText.Text = state.ConnectionState.ToString();
        StatusText.Foreground = state.ConnectionState switch
        {
            DeviceConnectionState.Receiving or DeviceConnectionState.ConnectedIdle => Brushes.Green,
            DeviceConnectionState.Connecting => Brushes.DodgerBlue,
            DeviceConnectionState.Timeout => Brushes.Orange,
            DeviceConnectionState.Fault => Brushes.Red,
            _ => Brushes.Gray
        };

        if (!state.LastReceiveTime.HasValue && state.ConnectionState == DeviceConnectionState.Disconnected)
        {
            RawValueText.Text = "--";
            DisplayValueText.Text = "--";
            LastReceiveTimeText.Text = "--";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _timeoutTimer.Stop();
        SerialService.Dispose();
        _overlay.Close();
        base.OnClosed(e);
    }
}
