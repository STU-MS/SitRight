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
}
