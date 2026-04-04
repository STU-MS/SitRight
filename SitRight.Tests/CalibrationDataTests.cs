using SitRight.Models;
using SitRight.Services;

namespace SitRight.Tests;

public class CalibrationDataTests
{
    private readonly CalibrationData _data = new();

    [Fact]
    public void Initial_State_IsNotCalibrated()
    {
        Assert.Equal(CalibrationState.NotCalibrated, _data.State);
        Assert.Null(_data.NormalAngle);
        Assert.Null(_data.SlouchAngle);
        Assert.Null(_data.LastError);
    }

    [Fact]
    public void ApplyAck_SetNormal_StateIsNormalSet()
    {
        var ack = new CalibrationAckData("SET_NORMAL", new Dictionary<string, string> { ["ANGLE"] = "12.34" });

        _data.ApplyAck(ack);

        Assert.Equal(CalibrationState.NormalSet, _data.State);
    }

    [Fact]
    public void ApplyAck_SetNormal_StoresAngle()
    {
        var ack = new CalibrationAckData("SET_NORMAL", new Dictionary<string, string> { ["ANGLE"] = "12.34" });

        _data.ApplyAck(ack);

        Assert.Equal(12.34, _data.NormalAngle);
    }

    [Fact]
    public void ApplyAck_SetSlouch_AfterNormal_StateIsFullyCalibrated()
    {
        _data.ApplyAck(new CalibrationAckData("SET_NORMAL", new Dictionary<string, string> { ["ANGLE"] = "10.0" }));
        _data.ApplyAck(new CalibrationAckData("SET_SLOUCH", new Dictionary<string, string> { ["ANGLE"] = "25.0" }));

        Assert.Equal(CalibrationState.FullyCalibrated, _data.State);
        Assert.Equal(25.0, _data.SlouchAngle);
    }

    [Fact]
    public void ApplyAck_SetSlouch_StoresSlouchAngle()
    {
        _data.State = CalibrationState.NormalSet;
        var ack = new CalibrationAckData("SET_SLOUCH", new Dictionary<string, string> { ["ANGLE"] = "25.67" });

        _data.ApplyAck(ack);

        Assert.Equal(25.67, _data.SlouchAngle);
    }

    [Fact]
    public void ApplyError_SetsErrorState()
    {
        var err = new CalibrationErrData("BUSY");

        _data.ApplyError(err);

        Assert.Equal(CalibrationState.Error, _data.State);
    }

    [Fact]
    public void ApplyError_StoresErrorCode()
    {
        var err = new CalibrationErrData("NO_SENSOR");

        _data.ApplyError(err);

        Assert.Equal("NO_SENSOR", _data.LastError);
    }

    [Fact]
    public void Reset_ReturnsToNotCalibrated()
    {
        _data.ApplyAck(new CalibrationAckData("SET_NORMAL", new Dictionary<string, string> { ["ANGLE"] = "10.0" }));
        _data.Reset();

        Assert.Equal(CalibrationState.NotCalibrated, _data.State);
        Assert.Null(_data.NormalAngle);
        Assert.Null(_data.SlouchAngle);
        Assert.Null(_data.LastError);
    }

    [Fact]
    public void ApplyAck_SetSlouch_WithoutNormal_StillTransitions()
    {
        var ack = new CalibrationAckData("SET_SLOUCH", new Dictionary<string, string> { ["ANGLE"] = "30.0" });

        _data.ApplyAck(ack);

        Assert.Equal(CalibrationState.FullyCalibrated, _data.State);
        Assert.Null(_data.NormalAngle);
        Assert.Equal(30.0, _data.SlouchAngle);
    }

    [Fact]
    public void ApplyAck_FullyCalibrated_ReNormal_ReturnsToNormalSet()
    {
        _data.ApplyAck(new CalibrationAckData("SET_NORMAL", new Dictionary<string, string> { ["ANGLE"] = "10.0" }));
        _data.ApplyAck(new CalibrationAckData("SET_SLOUCH", new Dictionary<string, string> { ["ANGLE"] = "25.0" }));

        _data.ApplyAck(new CalibrationAckData("SET_NORMAL", new Dictionary<string, string> { ["ANGLE"] = "11.0" }));

        Assert.Equal(CalibrationState.NormalSet, _data.State);
        Assert.Equal(11.0, _data.NormalAngle);
    }

    [Fact]
    public void ApplyAck_SetsLastCalibrated()
    {
        var before = DateTime.Now;
        _data.ApplyAck(new CalibrationAckData("SET_NORMAL", new Dictionary<string, string> { ["ANGLE"] = "10.0" }));
        var after = DateTime.Now;

        Assert.NotNull(_data.LastCalibrated);
        Assert.True(_data.LastCalibrated >= before && _data.LastCalibrated <= after);
    }

    [Fact]
    public void ApplyAck_NoAngleField_DoesNotThrow()
    {
        var ack = new CalibrationAckData("SET_NORMAL", new Dictionary<string, string>());

        _data.ApplyAck(ack);

        Assert.Equal(CalibrationState.NormalSet, _data.State);
        Assert.Null(_data.NormalAngle);
    }
}
