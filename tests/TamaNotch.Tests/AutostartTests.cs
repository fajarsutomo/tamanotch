using TamaNotch.Services.Settings;
using Xunit;

namespace TamaNotch.Tests;

public class AutostartTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    // exe lama sebelum rename: selalu ditulis ulang, ada atau tidak
    [InlineData("\"C:\\Users\\x\\AppData\\Local\\Programs\\TAMANOTCH\\NotchMusic.exe\"", true)]
    [InlineData("C:\\apps\\notchmusic.EXE", true)]
    public void ShouldRefresh_ForMissingOrLegacyValues(string? current, bool expected)
        => Assert.Equal(expected, SettingsService.ShouldRefreshAutostart(current, _ => true));

    [Fact]
    public void ShouldRefresh_WhenTheTargetNoLongerExists()
    {
        var value = "\"C:\\Programs\\TAMANOTCH\\TamaNotch.exe\"";

        Assert.True(SettingsService.ShouldRefreshAutostart(value, _ => false));
    }

    [Fact]
    public void ShouldNotRefresh_WhenItPointsToAnExistingExe_SoDevRunsDoNotHijackIt()
    {
        string? checkedPath = null;
        var value = "\"C:\\Programs\\TAMANOTCH\\TamaNotch.exe\"";

        var refresh = SettingsService.ShouldRefreshAutostart(value, p => { checkedPath = p; return true; });

        Assert.False(refresh);
        Assert.Equal("C:\\Programs\\TAMANOTCH\\TamaNotch.exe", checkedPath); // tanda kutip dibuang sebelum dicek
    }
}
