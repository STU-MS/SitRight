using SitRight.Services;

namespace SitRight.Services;

public class DeviceProtocolTests
{
    private readonly DeviceProtocol _protocol = new();

    [Theory]
    [InlineData("37", 37)]
    [InlineData("0", 0)]
    [InlineData("100", 100)]
    [InlineData("  42  ", 42)]
    [InlineData("99", 99)]
    public void TryParse_ValidInput_ReturnsTrueAndValue(string input, int expected)
    {
        var result = _protocol.TryParse(input, out var value);

        Assert.True(result);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("101")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12.5")]
    [InlineData("12a")]
    public void TryParse_InvalidInput_ReturnsFalse(string input)
    {
        var result = _protocol.TryParse(input, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParse_NullInput_ReturnsFalse()
    {
        var result = _protocol.TryParse(null, out _);

        Assert.False(result);
    }
}
