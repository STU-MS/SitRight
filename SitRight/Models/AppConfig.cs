using System;

namespace SitRight.Models;

public class AppConfig
{
    public string DefaultComPort { get; set; } = "COM1";
    public int BaudRate { get; set; } = 115200;
    public int TimeoutThresholdMs { get; set; } = 2000;
    public double SmoothingAlpha { get; set; } = 0.18;
    public double MaxMaskOpacity { get; set; } = 0.70;
    public int HintStartLevel { get; set; } = 30;
    public int UrgentLevel { get; set; } = 80;
    public int TargetMonitorIndex { get; set; } = 0;
    public double? CalibratedNormalAngle { get; set; }
    public double? CalibratedSlouchAngle { get; set; }
    public DateTime? CalibratedAt { get; set; }
}
