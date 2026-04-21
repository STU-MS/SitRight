using System.Collections.Generic;

namespace SitRight.Services;

public class DeviceProtocol
{
    public bool TryParse(string? line, out int value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(line))
            return false;

        if (!int.TryParse(line.Trim(), out value))
            return false;

        return value is >= 0 and <= 100;
    }

    public bool TryParseFull(string? line, out ProtocolLineType type, out int runtimeValue,
        out CalibrationAckData? ack, out CalibrationErrData? err)
    {
        type = default;
        runtimeValue = 0;
        ack = null;
        err = null;

        if (string.IsNullOrWhiteSpace(line))
            return false;

        var trimmed = line.Trim();

        if (trimmed.StartsWith("ACK:"))
        {
            type = ProtocolLineType.CalibrationAck;
            var content = trimmed[4..];
            var parts = content.Split(',');
            var command = parts[0];
            var fields = new Dictionary<string, string>();

            for (int i = 1; i < parts.Length; i++)
            {
                var kv = parts[i].Split(':', 2);
                if (kv.Length == 2)
                    fields[kv[0]] = kv[1];
            }

            ack = new CalibrationAckData(command, fields);
            return true;
        }

        if (trimmed.StartsWith("ERR:"))
        {
            type = ProtocolLineType.CalibrationErr;
            var errorCode = trimmed[4..];
            err = new CalibrationErrData(errorCode);
            return true;
        }

        if (int.TryParse(trimmed, out runtimeValue) && runtimeValue is >= 0 and <= 100)
        {
            type = ProtocolLineType.RuntimeData;
            return true;
        }

        return false;
    }
}
