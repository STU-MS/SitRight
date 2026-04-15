using System;

namespace SitRight.Models;

public class OverlayState
{
    public double MaskOpacity { get; set; }
    public string MaskColor { get; set; } = "#FFFFFF";
    public double EdgeOpacity { get; set; }
    public string MessageText { get; set; } = string.Empty;
    public double MessageOpacity { get; set; }
    public int SeverityLevel { get; set; }
    public bool BlockInput { get; set; }

    public static OverlayState FromDisplayLevel(double level, int hintStart = 30, int urgentLevel = 80)
    {
        level = Math.Clamp(level, 0, 100);
        var normalized = level / 100.0;
        var maskOpacity = level > 95 ? 1.0 : 0.02 + Math.Pow(normalized, 2.8) * 0.98;
        var edgeOpacity = Math.Pow(normalized, 1.8) * 0.25;
        var message = level < hintStart ? string.Empty : level < urgentLevel ? "请调整坐姿" : "请立即调整坐姿！";
        var messageOpacity = level <= hintStart ? 0 : Math.Min(1.0, (level - hintStart) / 40.0);
        var severity = level switch { <= 20 => 0, <= 50 => 1, <= 79 => 2, _ => 3 };

        return new OverlayState
        {
            MaskOpacity = Math.Clamp(maskOpacity, 0, 1),
            MaskColor = level < 30 ? "#FFFFFF" : level < 60 ? "#E0E0E0" : level < 80 ? "#BDBDBD" : "#9E9E9E",
            EdgeOpacity = Math.Clamp(edgeOpacity, 0, 1),
            MessageText = message,
            MessageOpacity = Math.Clamp(messageOpacity, 0, 1),
            SeverityLevel = severity,
            BlockInput = false
        };
    }
}
