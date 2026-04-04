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
    private readonly ConfigService _configService;
    private readonly MainViewModel _viewModel;

    public MainViewModelTests()
    {
        _mockSerial = new Mock<ISerialService>();
        _mockSerial.Setup(s => s.GetAvailablePorts()).Returns(new[] { "COM1", "COM2" });

        _protocol = new DeviceProtocol();
        _stateManager = new DeviceStateManager();
        _valueMapper = new ValueMapper();

        var testPath = Path.Combine(Path.GetTempPath(), $"test_vm_{Guid.NewGuid()}.json");
        _configService = new ConfigService(testPath);

        _viewModel = new MainViewModel(
            _mockSerial.Object,
            _protocol,
            _stateManager,
            _valueMapper,
            _configService);
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

        _viewModel.IsSimulationMode = true;
        _viewModel.SimulateValue(50);

        Assert.NotNull(receivedState);
    }
}
