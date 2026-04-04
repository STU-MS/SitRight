using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using SitRight.Models;
using SitRight.Services;
using SitRight.ViewModels;

namespace SitRight;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly OverlayWindow _overlay;
    private readonly DispatcherTimer _timeoutTimer;
    private readonly DeviceStateManager _stateManager;
    private readonly ConfigService _configService;

    public MainWindow()
    {
        InitializeComponent();

        var configService = new ConfigService();
        var config = configService.Load();
        _configService = configService;

        var serialService = new SerialService();
        var protocol = new DeviceProtocol();
        _stateManager = new DeviceStateManager();
        var valueMapper = new ValueMapper(config.HintStartLevel, config.UrgentLevel);

        _viewModel = new MainViewModel(
            serialService,
            protocol,
            _stateManager,
            valueMapper,
            configService);

        _overlay = new OverlayWindow();
        _overlay.Show();

        RefreshMonitors(config.TargetMonitorIndex);

        _viewModel.OnOverlayStateChanged += state => Dispatcher.Invoke(() => _overlay.ApplyState(state));

        _stateManager.OnStateChanged += state => Dispatcher.Invoke(() => UpdateStatus(state));

        _timeoutTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timeoutTimer.Tick += (_, _) => _stateManager.CheckTimeout();
        _timeoutTimer.Start();

        BindUIEvents();
        RefreshPorts();
        UpdateStatus(_stateManager.State);

        Log("应用程序已启动");
    }

    private void BindUIEvents()
    {
        RefreshButton.Click += (_, _) => RefreshPorts();
        ConnectButton.Click += ConnectButtonClicked;

        MonitorComboBox.SelectionChanged += (_, _) =>
        {
            var index = MonitorComboBox.SelectedIndex;
            if (index >= 0)
            {
                _overlay.MoveToMonitor(index);
                _configService.Update(c => c.TargetMonitorIndex = index);
                Log($"Overlay 已切换到显示器 {index + 1}");
            }
        };

        CalibrateNormalButton.Click += (_, _) =>
        {
            if (!_viewModel.IsConnected)
            {
                Log("请先连接设备");
                return;
            }
            _viewModel.SendCalibrationCommand("SET_NORMAL");
            Log("已发送校准坐正命令...");
        };

        CalibrateSlouchButton.Click += (_, _) =>
        {
            if (!_viewModel.IsConnected)
            {
                Log("请先连接设备");
                return;
            }
            _viewModel.SendCalibrationCommand("SET_SLOUCH");
            Log("已发送校准驼背命令...");
        };

        SimulationModeCheckBox.Checked += (_, _) =>
        {
            _viewModel.IsSimulationMode = true;
            SimulatedValueSlider.IsEnabled = true;
        };

        SimulationModeCheckBox.Unchecked += (_, _) =>
        {
            _viewModel.IsSimulationMode = false;
            SimulatedValueSlider.IsEnabled = false;
            _overlay.ApplyState(new OverlayState());
        };

        SimulatedValueSlider.ValueChanged += (_, e) =>
        {
            var value = (int)e.NewValue;
            SimulatedValueText.Text = value.ToString();
            _viewModel.SimulateValue(value);
        };

        _viewModel.OnConnectionStateChanged += _ => Dispatcher.Invoke(() =>
        {
            ConnectButton.Content = _viewModel.IsConnected ? "断开" : "连接";
        });

        _viewModel.OnCalibrationChanged += _ => Dispatcher.Invoke(() =>
        {
            CalibrationStatusText.Text = _viewModel.CalibrationStatusText;
            NormalAngleText.Text = _viewModel.NormalAngleText;
            SlouchAngleText.Text = _viewModel.SlouchAngleText;

            var data = _viewModel.CalibrationData;
            if (data.State == CalibrationState.Error)
                Log($"校准错误: {data.LastError}");
            else if (data.LastCalibrated.HasValue)
                Log($"校准状态: {_viewModel.CalibrationStatusText}");
        });
    }

    private void ConnectButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsConnected)
        {
            _viewModel.Disconnect();
            return;
        }

        var portName = ComPortComboBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(portName))
        {
            Log("请先选择 COM 口");
            return;
        }

        try
        {
            var baudRate = GetSelectedBaudRate();
            _viewModel.Connect(portName, baudRate);
        }
        catch (Exception ex)
        {
            Log($"连接失败: {ex.Message}");
        }
    }

    private int GetSelectedBaudRate()
    {
        if (BaudRateComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem comboBoxItem)
        {
            var contentText = comboBoxItem.Content?.ToString();
            if (int.TryParse(contentText, out var comboValue))
                return comboValue;
        }

        if (int.TryParse(BaudRateComboBox.Text, out var textValue))
            return textValue;

        return 115200;
    }

    private void RefreshPorts()
    {
        var ports = _viewModel.AvailablePorts;
        ComPortComboBox.ItemsSource = ports;

        if (ports.Length > 0 && ComPortComboBox.SelectedItem is null)
            ComPortComboBox.SelectedIndex = 0;

        Log($"已刷新串口列表: {ports.Length} 个端口");
    }

    private void RefreshMonitors(int savedIndex = 0)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        var items = screens.Select((s, i) =>
        {
            var label = s.Primary ? "主显示器" : $"显示器 {i + 1}";
            return $"{label} ({s.Bounds.Width}x{s.Bounds.Height})";
        }).ToArray();

        MonitorComboBox.ItemsSource = items;
        MonitorComboBox.SelectedIndex = savedIndex < items.Length ? savedIndex : 0;
    }

    private void UpdateStatus(DeviceState state)
    {
        StatusText.Text = state.ConnectionState.ToString();
        StatusText.Foreground = state.ConnectionState switch
        {
            DeviceConnectionState.Receiving or DeviceConnectionState.ConnectedIdle => System.Windows.Media.Brushes.Green,
            DeviceConnectionState.Connecting => System.Windows.Media.Brushes.DodgerBlue,
            DeviceConnectionState.Timeout => System.Windows.Media.Brushes.Orange,
            DeviceConnectionState.Fault => System.Windows.Media.Brushes.Red,
            _ => System.Windows.Media.Brushes.Gray
        };

        if (!state.LastReceiveTime.HasValue && state.ConnectionState == DeviceConnectionState.Disconnected)
        {
            RawValueText.Text = "--";
            DisplayValueText.Text = "--";
            LastReceiveTimeText.Text = "--";
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

    protected override void OnClosed(EventArgs e)
    {
        _timeoutTimer.Stop();
        _overlay.Close();
        base.OnClosed(e);
    }
}
