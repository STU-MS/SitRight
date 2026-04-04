using SitRight.Models;

namespace SitRight.Services;

public class ValueMapper
{
    private readonly int _hintStartLevel;
    private readonly int _urgentLevel;

    public ValueMapper(int hintStartLevel = 30, int urgentLevel = 80)
    {
        _hintStartLevel = hintStartLevel;
        _urgentLevel = urgentLevel;
    }

    public OverlayState Map(int blurLevel)
    {
        return OverlayState.FromDisplayLevel(blurLevel, _hintStartLevel, _urgentLevel);
    }
}
