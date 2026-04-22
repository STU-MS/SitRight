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

        // 线性映射 — 非线性敏感度由硬件端处理
        var maskOpacity = normalized * 0.95;
        if (level > 90)
            maskOpacity = 1.0;

        var edgeOpacity = Math.Pow(normalized, 1.8) * 0.25;

        string messageText = string.Empty;
        double messageOpacity = 0;

        if (level >= urgentLevel)
        {
            messageText = "请立即调整坐姿！";
            messageOpacity = 1;
        }
        else if (level >= hintStart)
        {
            messageText = "请调整坐姿";
            var range = Math.Max(1, urgentLevel - hintStart);
            messageOpacity = 0.3 + ((level - hintStart) / range) * 0.5;
        }

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
            MaskColor = "#FFFFFF",
            EdgeOpacity = edgeOpacity,
            MessageText = messageText,
            MessageOpacity = messageOpacity,
            SeverityLevel = severity,
        };
    }
}
