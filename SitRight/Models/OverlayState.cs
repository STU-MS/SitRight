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

    public static OverlayState FromDisplayLevel(double level, int hintStart = 30, int urgentLevel = 80)
    {
        var normalized = level / 100.0;

        // Opacity mapping: low level = subtle, high level = aggressive
        var maskOpacity = 0.05 + Math.Pow(normalized, 1.4) * 0.65;

        // Color: white (cold) -> light gray -> darker gray
        string color;
        if (level < 30)
            color = "#FFFFFF";
        else if (level < 60)
            color = "#E0E0E0";
        else if (level < 80)
            color = "#BDBDBD";
        else
            color = "#9E9E9E";

        // Edge opacity for fog effect
        var edgeOpacity = Math.Pow(normalized, 1.8) * 0.25;

        // Message text based on severity
        string message;
        if (level < hintStart)
            message = string.Empty;
        else if (level < urgentLevel)
            message = "请调整坐姿";
        else
            message = "请立即调整坐姿！";

        // Message opacity: fade in after hintStart
        var messageOpacity = level > hintStart
            ? Math.Min(1.0, (level - hintStart) / 40.0)
            : 0;

        // Severity level
        var severity = level switch
        {
            <= 20 => 0,
            <= 50 => 1,
            <= 79 => 2,
            _ => 3
        };

        return new OverlayState
        {
            MaskOpacity = maskOpacity,
            MaskColor = color,
            EdgeOpacity = edgeOpacity,
            MessageText = message,
            MessageOpacity = messageOpacity,
            SeverityLevel = severity
        };
    }
}
