using SitRight.Services;

namespace SitRight.Services;

public class DeviceProtocolTests
{
    private readonly DeviceProtocol _protocol = new();

    [Theory]
    [InlineData("37", 37)]
    [InlineData("0", 0)]
    [InlineData("100", 100)]
    [InlineData("  42  ", 42)]
    [InlineData("99", 99)]
    public void TryParse_ValidInput_ReturnsTrueAndValue(string input, int expected)
    {
        var result = _protocol.TryParse(input, out var value);

        Assert.True(result);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("101")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12.5")]
    [InlineData("12a")]
    public void TryParse_InvalidInput_ReturnsFalse(string input)
    {
        var result = _protocol.TryParse(input, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParse_NullInput_ReturnsFalse()
    {
        var result = _protocol.TryParse(null, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParseFull_ACK_SetNormal()
    {
        var result = _protocol.TryParseFull("ACK:SET_NORMAL,ANGLE:12.34", out var type, out _, out var ack, out _);

        Assert.True(result);
        Assert.Equal(ProtocolLineType.CalibrationAck, type);
        Assert.NotNull(ack);
        Assert.Equal("SET_NORMAL", ack.Command);
        Assert.Single(ack.Fields);
        Assert.Equal("12.34", ack.Fields["ANGLE"]);
    }

    [Fact]
    public void TryParseFull_ACK_SetSlouch()
    {
        var result = _protocol.TryParseFull("ACK:SET_SLOUCH,ANGLE:25.67", out var type, out _, out var ack, out _);

        Assert.True(result);
        Assert.Equal(ProtocolLineType.CalibrationAck, type);
        Assert.NotNull(ack);
        Assert.Equal("SET_SLOUCH", ack.Command);
        Assert.Equal("25.67", ack.Fields["ANGLE"]);
    }

    [Fact]
    public void TryParseFull_ACK_GetStatus_MultipleFields()
    {
        var result = _protocol.TryParseFull("ACK:GET_STATUS,NORMAL:12.34,SLOUCH:25.67,STATUS:CALIBRATED", out var type, out _, out var ack, out _);

        Assert.True(result);
        Assert.Equal(ProtocolLineType.CalibrationAck, type);
        Assert.NotNull(ack);
        Assert.Equal("GET_STATUS", ack.Command);
        Assert.Equal(3, ack.Fields.Count);
        Assert.Equal("12.34", ack.Fields["NORMAL"]);
        Assert.Equal("25.67", ack.Fields["SLOUCH"]);
        Assert.Equal("CALIBRATED", ack.Fields["STATUS"]);
    }

    [Fact]
    public void TryParseFull_ACK_Reset_NoFields()
    {
        var result = _protocol.TryParseFull("ACK:RESET", out var type, out _, out var ack, out _);

        Assert.True(result);
        Assert.Equal(ProtocolLineType.CalibrationAck, type);
        Assert.NotNull(ack);
        Assert.Equal("RESET", ack.Command);
        Assert.Empty(ack.Fields);
    }

    [Fact]
    public void TryParseFull_ERR_Busy()
    {
        var result = _protocol.TryParseFull("ERR:BUSY", out var type, out _, out _, out var err);

        Assert.True(result);
        Assert.Equal(ProtocolLineType.CalibrationErr, type);
        Assert.NotNull(err);
        Assert.Equal("BUSY", err.ErrorCode);
    }

    [Fact]
    public void TryParseFull_ERR_UnknownCmd()
    {
        var result = _protocol.TryParseFull("ERR:UNKNOWN_CMD", out var type, out _, out _, out var err);

        Assert.True(result);
        Assert.Equal(ProtocolLineType.CalibrationErr, type);
        Assert.NotNull(err);
        Assert.Equal("UNKNOWN_CMD", err.ErrorCode);
    }

    [Fact]
    public void TryParseFull_RuntimeData()
    {
        var result = _protocol.TryParseFull("37", out var type, out var value, out var ack, out var err);

        Assert.True(result);
        Assert.Equal(ProtocolLineType.RuntimeData, type);
        Assert.Equal(37, value);
        Assert.Null(ack);
        Assert.Null(err);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseFull_NullOrEmpty_ReturnsFalse(string? input)
    {
        var result = _protocol.TryParseFull(input, out _, out _, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParseFull_InvalidRuntimeData_ReturnsFalse()
    {
        var result = _protocol.TryParseFull("abc", out _, out _, out _, out _);

        Assert.False(result);
    }
}
