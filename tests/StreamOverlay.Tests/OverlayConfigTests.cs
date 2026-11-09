using Xunit;

namespace StreamOverlay.Tests;

public class OverlayConfigTests
{
    [Fact]
    public void EnabledRequestIsRead()
    {
        OverlayConfig? config = OverlayConfig.Parse(
            "{\"OverlayEnabled\":true,\"OverlayPort\":8123,\"OverlayToken\":\"0123456789abcdef0123456789abcdef\"}", out string? why);

        Assert.Null(why);
        Assert.Equal(8123, config?.OverlayPort);
        Assert.Equal("0123456789abcdef0123456789abcdef", config?.OverlayToken);
    }

    [Fact]
    public void OffOrMissingFileIsSilent()
    {
        Assert.Null(OverlayConfig.Parse(null, out string? missing));
        Assert.Null(missing);
        Assert.Null(OverlayConfig.Parse("{\"OverlayEnabled\":false,\"OverlayPort\":8123}", out string? off));
        Assert.Null(off);
    }

    [Theory]
    [InlineData("{\"OverlayEnabled\":true,\"OverlayPort\":0,\"OverlayToken\":\"t\"}", "OverlayPort 0 is not a TCP port")]
    [InlineData("{\"OverlayEnabled\":true,\"OverlayPort\":8123}", "OverlayToken is missing")]
    public void RequestItCannotServeSaysWhy(string json, string expected)
    {
        Assert.Null(OverlayConfig.Parse(json, out string? why));
        Assert.Equal(expected, why);
    }
}
