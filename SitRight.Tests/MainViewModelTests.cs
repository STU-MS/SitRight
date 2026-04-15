using Xunit;
using Moq;
using SitRight.Services;
using SitRight.Models;
using SitRight.ViewModels;

namespace SitRight.Tests;

public class MainViewModelTests
{
    private readonly Mock<ISerialService> _mockSerial;
    private readonly DeviceProtocol _protocol;
    private readonly DeviceStateManager _stateManager;
    private readonly ValueMapper _valueMapper;
    private readonly string _testPath;
    private readonly ConfigService _configService;
    private MainViewModel _viewModel;

    public MainViewModelTests()
    {
        _mockSerial = new Mock<ISerialService>();
        _mockSerial.Setup(s => s.GetAvailablePorts()).Returns(new[] { "COM1", "COM2" });

        _protocol = new DeviceProtocol();
        _stateManager = new DeviceStateManager();
        _valueMapper = new ValueMapper();

        _testPath = Path.Combine(Path.GetTempPath(), $"test_vm_{Guid.NewGuid()}.json");
        _configService = new ConfigService(_testPath);

        _viewModel = CreateViewModel();
    }

    [Fact]
    public void InitialStatus_IsDisconnected()
    {
        Assert.Equal("Disconnected", _viewModel.StatusText);
    }

    [Fact]
    public void AvailablePorts_ReturnsFromSerialService()
    {
        var ports = _viewModel.AvailablePorts;
        Assert.Contains("COM1", ports);
        Assert.Contains("COM2", ports);
    }

    [Fact]
    public void InitialIsConnected_IsFalse()
    {
        Assert.False(_viewModel.IsConnected);
    }

    [Fact]
    public void InitialRawValue_IsDash()
    {
        Assert.Equal("--", _viewModel.RawValueText);
    }

    [Fact]
    public void InitialDisplayValue_IsDash()
    {
        Assert.Equal("--", _viewModel.DisplayValueText);
    }

    [Fact]
    public void SetSimulationMode_RaisesEvent()
    {
        var eventRaised = false;
        _viewModel.OnSimulationModeChanged += _ => eventRaised = true;

        _viewModel.IsSimulationMode = true;

        Assert.True(eventRaised);
    }

    [Fact]
    public void SimulateValue_WhenSimulationMode_MapsDirectly()
    {
        _viewModel.IsSimulationMode = true;
        _viewModel.SimulateValue(50);

        Assert.Equal("50", _viewModel.RawValueText);
    }

    [Fact]
    public void SimulateValue_WhenNotSimulationMode_DoesNothing()
    {
        _viewModel.IsSimulationMode = false;
        _viewModel.SimulateValue(50);

        Assert.Equal("--", _viewModel.RawValueText);
    }

    [Fact]
    public void Disconnect_CallsSerialDisconnect()
    {
        _viewModel.Disconnect();
        _mockSerial.Verify(s => s.Disconnect(), Times.Once);
    }

    [Fact]
    public void OnOverlayStateChanged_FiredOnSimulate()
    {
        OverlayState? receivedState = null;
        _viewModel.OnOverlayStateChanged += state => receivedState = state;

        // 设置为完全校准状态，以便触发遮罩渲染
        _viewModel.CalibrationData.ApplyAck(new CalibrationAckData("SET_SLOUCH", new Dictionary<string, string> { { "ANGLE", "10.0" } }));

        _viewModel.IsSimulationMode = true;
        _viewModel.SimulateValue(50);

        Assert.NotNull(receivedState);
    }

    [Fact]
    public void Constructor_WithPersistedCalibration_RestoresCalibrationAndMapper()
    {
        var calibratedAt = new DateTime(2026, 4, 15, 10, 30, 0, DateTimeKind.Local);
        _configService.Save(new AppConfig
        {
            CalibratedNormalAngle = 10,
            CalibratedSlouchAngle = 25,
            CalibratedAt = calibratedAt
        });

        _viewModel = CreateViewModel();

        OverlayState? receivedState = null;
        _viewModel.OnOverlayStateChanged += state => receivedState = state;
        _viewModel.IsSimulationMode = true;
        _viewModel.SimulateValue(25);

        Assert.Equal(CalibrationState.FullyCalibrated, _viewModel.CalibrationData.State);
        Assert.Equal(10, _viewModel.CalibrationData.NormalAngle);
        Assert.Equal(25, _viewModel.CalibrationData.SlouchAngle);
        Assert.Equal(calibratedAt, _viewModel.CalibrationData.LastCalibrated);
        Assert.NotNull(receivedState);
        Assert.True(receivedState!.MaskOpacity > 0.6);
    }

    [Fact]
    public void CalibrationAck_WhenFullyCalibrated_PersistsCalibrationToConfig()
    {
        _mockSerial.Raise(s => s.OnLineReceived += null, "ACK:SET_NORMAL,ANGLE:10.0");
        _mockSerial.Raise(s => s.OnLineReceived += null, "ACK:SET_SLOUCH,ANGLE:25.0");

        var reloaded = new ConfigService(_testPath).Load();

        Assert.Equal(10.0, reloaded.CalibratedNormalAngle);
        Assert.Equal(25.0, reloaded.CalibratedSlouchAngle);
        Assert.NotNull(reloaded.CalibratedAt);
    }

    [Fact]
    public void CalibrationAck_ClearsExistingOverlayState()
    {
        OverlayState? receivedState = null;
        _viewModel.OnOverlayStateChanged += state => receivedState = state;

        _mockSerial.Raise(s => s.OnLineReceived += null, "ACK:SET_NORMAL,ANGLE:10.0");
        _mockSerial.Raise(s => s.OnLineReceived += null, "ACK:SET_SLOUCH,ANGLE:25.0");
        _viewModel.IsSimulationMode = true;
        _viewModel.SimulateValue(25);

        Assert.NotNull(receivedState);
        Assert.True(receivedState!.MaskOpacity > 0.6);

        _mockSerial.Raise(s => s.OnLineReceived += null, "ACK:SET_NORMAL,ANGLE:11.0");

        Assert.Equal(0, receivedState.MaskOpacity);
        Assert.Equal(string.Empty, receivedState.MessageText);
    }

    private MainViewModel CreateViewModel()
    {
        return new MainViewModel(
            _mockSerial.Object,
            _protocol,
            _stateManager,
            _valueMapper,
            _configService);
    }
}
