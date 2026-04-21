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
    public string? LastError { get; set; }

    public void ApplyAck(CalibrationAckData ack)
    {
        LastError = null;

        switch (ack.Command)
        {
            case "SET_NORMAL":
                State = CalibrationState.NormalSet;
                break;
            case "SET_SLOUCH":
                State = CalibrationState.FullyCalibrated;
                break;
            case "RESET":
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
        LastError = null;
    }
}
