using System.Text.RegularExpressions;
using NotchMusic.Helpers;
using Xunit;

namespace NotchMusic.Tests;

public class AppVersionTests
{
    [Fact]
    public void Current_IsThreePartVersion()
    {
        // Skema: 0.x.y sebelum rilis pertama, lalu 1.x.y (x = fitur, y = perbaikan).
        Assert.Matches(new Regex(@"^\d+\.\d+\.\d+$"), AppVersion.Current);
        VersionHelper.TryParseCleanVersion(AppVersion.Current, out var v);
        Assert.InRange(v.Major, 0, 1);
    }

    [Fact]
    public void UserAgentToken_UsesMajorMinorOfCurrentVersion()
    {
        VersionHelper.TryParseCleanVersion(AppVersion.Current, out var v);

        Assert.Equal($"TAMANOTCH/{v.Major}.{v.Minor}", AppVersion.UserAgentToken);
        Assert.StartsWith("TAMANOTCH/", AppVersion.UserAgentToken);
    }

    [Theory]
    [InlineData("0.1.0", "0.1.1", true, false, false, true)]    // perbaikan bug -> y naik
    [InlineData("0.1.1", "0.2.0", true, false, true, false)]    // fitur baru -> x naik, y kembali ke 0
    [InlineData("0.2.5", "0.2.5", false, false, false, false)]
    [InlineData("0.3.0", "0.2.9", false, false, false, false)]  // lebih lama bukan pembaruan
    [InlineData("0.9.0", "1.0.0", true, true, false, false)]
    [InlineData("1.0.0", "1.0.1", true, false, false, true)]    // setelah rilis pertama
    [InlineData("1.0.1", "1.1.0", true, false, true, false)]
    public void Compare_WorksForZeroAndOneDotXDotYVersions(string current, string remote, bool newer, bool major, bool minor, bool patch)
    {
        var result = VersionHelper.Compare(current, remote);

        Assert.Equal((newer, major, minor, patch), result);
    }
}
