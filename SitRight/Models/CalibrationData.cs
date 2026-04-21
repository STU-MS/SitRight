using System;
using System.Collections.Generic;
using SitRight.Services;

namespace SitRight.Models;

public enum CalibrationState
{
    NotCalibrated,
    NormalSet,
    FullyCalibrated,
    Error
}

public class CalibrationData
{
    public CalibrationState State { get; set; } = CalibrationState.NotCalibrated;
    public double? NormalAngle { get; set; }
    public double? SlouchAngle { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastCalibrated { get; set; }

    public void ApplyAck(CalibrationAckData ack)
    {
        LastError = null;
        LastCalibrated = DateTime.Now;

        switch (ack.Command)
        {
            case "SET_NORMAL":
                if (ack.Fields.TryGetValue("ANGLE", out var normalStr) && double.TryParse(normalStr, out var normalAngle))
                    NormalAngle = normalAngle;
                State = CalibrationState.NormalSet;
                break;

            case "SET_SLOUCH":
                if (ack.Fields.TryGetValue("ANGLE", out var slouchStr) && double.TryParse(slouchStr, out var slouchAngle))
                    SlouchAngle = slouchAngle;
                State = CalibrationState.FullyCalibrated;
                break;

            case "RESET":
                NormalAngle = null;
                SlouchAngle = null;
                State = CalibrationState.NotCalibrated;
                break;
        }
    }

    public void ApplyError(CalibrationErrData err)
    {
        State = CalibrationState.Error;
        LastError = err.ErrorCode;
    }

    public void Reset()
    {
        State = CalibrationState.NotCalibrated;
        NormalAngle = null;
        SlouchAngle = null;
        LastError = null;
    }
}
