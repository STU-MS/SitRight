namespace SitRight.Models;

public class AppConfig
{
    public string DefaultComPort { get; set; } = "COM1";
    public int BaudRate { get; set; } = 115200;
    public int TimeoutThresholdMs { get; set; } = 2000;
    public double MaxMaskOpacity { get; set; } = 0.70;
    public int HintStartLevel { get; set; } = 30;
    public int UrgentLevel { get; set; } = 80;
}
