using SitRight.Services;
using SitRight.Models;

namespace SitRight.Tests;

public class CalibrationDataTests
{
    private readonly CalibrationData _data = new();

    [Fact]
    public void Initial_State_IsNotCalibrated()
    {
        Assert.Equal(CalibrationState.NotCalibrated, _data.State);
        Assert.Null(_data.LastError);
    }

    [Fact]
    public void ApplyAck_SetNormal_StateIsNormalSet()
    {
        var ack = new CalibrationAckData("SET_NORMAL", new Dictionary<string, string>());
        _data.ApplyAck(ack);
        Assert.Equal(CalibrationState.NormalSet, _data.State);
    }

    [Fact]
    public void ApplyAck_SetSlouch_StateIsFullyCalibrated()
    {
        var ack = new CalibrationAckData("SET_SLOUCH", new Dictionary<string, string>());
        _data.ApplyAck(ack);
        Assert.Equal(CalibrationState.FullyCalibrated, _data.State);
    }

    [Fact]
    public void ApplyAck_SetNormalThenSlouch_StateIsFullyCalibrated()
    {
        _data.ApplyAck(new CalibrationAckData("SET_NORMAL", new Dictionary<string, string>()));
        _data.ApplyAck(new CalibrationAckData("SET_SLOUCH", new Dictionary<string, string>()));
        Assert.Equal(CalibrationState.FullyCalibrated, _data.State);
    }

    [Fact]
    public void ApplyAck_Reset_ReturnsToNotCalibrated()
    {
        _data.ApplyAck(new CalibrationAckData("SET_NORMAL", new Dictionary<string, string>()));
        _data.ApplyAck(new CalibrationAckData("RESET", new Dictionary<string, string>()));
        Assert.Equal(CalibrationState.NotCalibrated, _data.State);
    }

    [Fact]
    public void ApplyAck_ReNormal_ReturnsToNormalSet()
    {
        _data.ApplyAck(new CalibrationAckData("SET_NORMAL", new Dictionary<string, string>()));
        _data.ApplyAck(new CalibrationAckData("SET_SLOUCH", new Dictionary<string, string>()));
        _data.ApplyAck(new CalibrationAckData("SET_NORMAL", new Dictionary<string, string>()));
        Assert.Equal(CalibrationState.NormalSet, _data.State);
    }

    [Fact]
    public void ApplyError_SetsErrorState()
    {
        _data.ApplyError(new CalibrationErrData("BUSY"));
        Assert.Equal(CalibrationState.Error, _data.State);
        Assert.Equal("BUSY", _data.LastError);
    }

    [Fact]
    public void Reset_ReturnsToNotCalibrated()
    {
        _data.ApplyAck(new CalibrationAckData("SET_NORMAL", new Dictionary<string, string>()));
        _data.Reset();
        Assert.Equal(CalibrationState.NotCalibrated, _data.State);
        Assert.Null(_data.LastError);
    }

    [Fact]
    public void ApplyAck_ClearsPreviousError()
    {
        _data.ApplyError(new CalibrationErrData("BUSY"));
        _data.ApplyAck(new CalibrationAckData("SET_NORMAL", new Dictionary<string, string>()));
        Assert.Null(_data.LastError);
    }
}
