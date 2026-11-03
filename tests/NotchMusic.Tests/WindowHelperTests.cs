using NotchMusic.Helpers;
using Xunit;

namespace NotchMusic.Tests;

public class WindowHelperTests
{
    [Fact]
    public void CalculateLeft_ZeroOffset_CentersWindow()
    {
        Assert.Equal(710, WindowHelper.CalculateLeft(0, 1920, 500, 0));
    }

    [Theory]
    [InlineData(10, 902)]    // 710 + 10% dari 1920
    [InlineData(-10, 518)]   // 710 - 10% dari 1920
    public void CalculateLeft_Offset_ShiftsByPercentOfScreenWidth(double percent, double expected)
    {
        Assert.Equal(expected, WindowHelper.CalculateLeft(0, 1920, 500, percent), 3);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    public void CalculateLeft_LargeOffset_StaysInsideScreen(double percent)
    {
        double left = WindowHelper.CalculateLeft(0, 1920, 500, percent);
        Assert.Equal(1420, left);
        Assert.True(left + 500 <= 1920);
    }

    [Fact]
    public void CalculateLeft_NegativeExtreme_StopsAtScreenEdge()
    {
        Assert.Equal(0, WindowHelper.CalculateLeft(0, 1920, 500, -80));
    }

    [Fact]
    public void CalculateLeft_SecondMonitor_RespectsScreenOrigin()
    {
        Assert.Equal(1920 + 710, WindowHelper.CalculateLeft(1920, 1920, 500, 0));
    }

    [Fact]
    public void CalculateLeft_WindowWiderThanScreen_PinsToLeftEdge()
    {
        Assert.Equal(0, WindowHelper.CalculateLeft(0, 800, 1000, 20));
    }
}
