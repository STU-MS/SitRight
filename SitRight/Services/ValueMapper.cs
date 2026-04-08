using SitRight.Models;

namespace SitRight.Services;

public class ValueMapper
{
    private readonly int _hintStartLevel;
    private readonly int _urgentLevel;

    // 校准角度
    private int? _normalAngle;
    private int? _slouchAngle;

    public ValueMapper(int hintStartLevel = 30, int urgentLevel = 80)
    {
        _hintStartLevel = hintStartLevel;
        _urgentLevel = urgentLevel;
    }

    // 让外部传入校准角度
    public void SetCalibration(int normalAngle, int slouchAngle)
    {
        _normalAngle = normalAngle;
        _slouchAngle = slouchAngle;
    }

    public OverlayState Map(int blurLevel)
    {
        // 未校准 或 角度 <= 坐正角度（后仰）→ 完全不遮罩
        if (!_normalAngle.HasValue || blurLevel <= _normalAngle.Value)
        {
            return OverlayState.FromDisplayLevel(0, _hintStartLevel, _urgentLevel);
        }

        // 在坐正 ~ 驼背之间 → 线性计算模糊度
        if (blurLevel < _slouchAngle)
        {
            var range = _slouchAngle - _normalAngle;
            var level = (int)((blurLevel - _normalAngle) / (double)range * 100);
            return OverlayState.FromDisplayLevel(level, _hintStartLevel, _urgentLevel);
        }

        // 超过驼背阈值 → 最大模糊
        return OverlayState.FromDisplayLevel(100, _hintStartLevel, _urgentLevel);
    }
}
