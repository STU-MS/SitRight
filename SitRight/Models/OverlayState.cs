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
        var normalized = level / 100.0;
        
        //var blockInput = level >= 80;

        // Opacity mapping: low level = subtle, high level = aggressive
        /*var maskOpacity = 0.05 + Math.Pow(normalized, 1.4) * 0.65;*/
        var maskOpacity = 0.02 + Math.Pow(normalized, 2.8) * 0.98;
        
        if (level > 95)
        {
            maskOpacity = 1.0;
        }

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
            SeverityLevel = severity,
            //BlockInput = blockInput //决定能不能交互
        };
    }
}
