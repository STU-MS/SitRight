using System.Collections.Generic;

namespace SitRight.Services;

public enum ProtocolLineType
{
    RuntimeData,
    CalibrationAck,
    CalibrationErr
}

public record CalibrationAckData(string Command, Dictionary<string, string> Fields);

public record CalibrationErrData(string ErrorCode);
