using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SitRight.Models;
using SitRight.Services;

namespace SitRight;

public partial class MainWindow : Window
{
    private bool _isSimulationMode;
    private readonly OverlayWindow _overlay;
    private readonly DispatcherTimer _timeoutTimer;

    public SerialService SerialService { get; } = new();
    public DeviceProtocol Protocol { get; } = new();
    public DeviceStateManager StateManager { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        _overlay = new OverlayWindow();
        _overlay.Show();

        BindEvents();
        RefreshPorts();
        UpdateStatus(StateManager.State);

        _timeoutTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timeoutTimer.Tick += (_, _) => StateManager.CheckTimeout();
        _timeoutTimer.Start();

        Log("应用程序已启动");
    }

    private void BindEvents()
    {
        RefreshButton.Click += (_, _) => RefreshPorts();
        ConnectButton.Click += ConnectButtonClicked;

        SerialService.OnConnected += () =>
        {
            StateManager.OnConnected();
            Dispatcher.Invoke(() =>
            {
                ConnectButton.Content = "断开";
                Log($"串口已连接: {SerialService.CurrentPort}");
            });
        };

        SerialService.OnDisconnected += () =>
        {
            StateManager.OnDisconnected();
            Dispatcher.Invoke(() =>
            {
                ConnectButton.Content = "连接";
                Log("串口已断开");
            });
        };

        SerialService.OnError += ex =>
        {
            StateManager.OnFault(ex.Message);
            Dispatcher.Invoke(() => Log($"串口错误: {ex.Message}"));
        };

        SerialService.OnLineReceived += line =>
        {
            if (!Protocol.TryParse(line, out var value))
            {
                Dispatcher.Invoke(() => Log($"收到非法串口数据: {line}"));
                return;
            }

            StateManager.ReceiveRawValue(value);
            Dispatcher.Invoke(() =>
            {
                RawValueText.Text = value.ToString();
                DisplayValueText.Text = value.ToString();
                LastReceiveTimeText.Text = DateTime.Now.ToString("HH:mm:ss");
            });
        };

        StateManager.OnStateChanged += state => Dispatcher.Invoke(() => UpdateStatus(state));
    }

    private void ConnectButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (SerialService.IsConnected)
        {
            SerialService.Disconnect();
            return;
        }

        var portName = ComPortComboBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(portName))
        {
            Log("请先选择 COM 口");
            return;
        }

        StateManager.OnConnecting();

        try
        {
            SerialService.Connect(portName, GetSelectedBaudRate());
        }
        catch (Exception ex)
        {
            StateManager.OnFault(ex.Message);
            Log($"连接失败: {ex.Message}");
        }
    }

    private int GetSelectedBaudRate()
    {
        if (BaudRateComboBox.SelectedItem is ComboBoxItem comboBoxItem)
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
        var ports = SerialService.GetAvailablePorts();
        ComPortComboBox.ItemsSource = ports;

        if (ports.Length > 0 && ComPortComboBox.SelectedItem is null)
            ComPortComboBox.SelectedIndex = 0;

        Log($"已刷新串口列表: {ports.Length} 个端口");
    }

    private void SimulationModeChanged(object sender, RoutedEventArgs e)
    {
        _isSimulationMode = SimulationModeCheckBox.IsChecked == true;
        SimulatedValueSlider.IsEnabled = _isSimulationMode;

        if (_isSimulationMode)
        {
            var level = SimulatedValueSlider.Value;
            _overlay.ApplyState(OverlayState.FromDisplayLevel(level));
            return;
        }

        _overlay.ApplyState(new OverlayState
        {
            MaskOpacity = 0,
            EdgeOpacity = 0,
            MessageOpacity = 0,
            MessageText = string.Empty,
            BlockInput = false
        });
    }

    private void SimulatedValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var level = e.NewValue;
        SimulatedValueText.Text = ((int)level).ToString();

        if (_isSimulationMode)
            _overlay.ApplyState(OverlayState.FromDisplayLevel(level));
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
