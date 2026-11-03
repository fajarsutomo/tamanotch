using System.Text.RegularExpressions;
using NotchMusic.Helpers;
using Xunit;

namespace NotchMusic.Tests;

public class AppVersionTests
{
    [Fact]
    public void Current_IsThreePartVersion_InTheZeroDotXDotYScheme()
    {
        Assert.Matches(new Regex(@"^\d+\.\d+\.\d+$"), AppVersion.Current);
        // Skema versi: 0.x.y (x = fitur, y = perbaikan). Ubah tes ini bila nanti naik ke 1.0.0.
        Assert.StartsWith("0.", AppVersion.Current);
    }

    [Fact]
    public void UserAgentToken_UsesMajorMinorOfCurrentVersion()
    {
        VersionHelper.TryParseCleanVersion(AppVersion.Current, out var v);

        Assert.Equal($"TAMANOTCH/{v.Major}.{v.Minor}", AppVersion.UserAgentToken);
        Assert.StartsWith("TAMANOTCH/0.", AppVersion.UserAgentToken);
    }

    [Theory]
    [InlineData("0.1.0", "0.1.1", true, false, false, true)]    // perbaikan bug -> y naik
    [InlineData("0.1.1", "0.2.0", true, false, true, false)]    // fitur baru -> x naik, y kembali ke 0
    [InlineData("0.2.5", "0.2.5", false, false, false, false)]
    [InlineData("0.3.0", "0.2.9", false, false, false, false)]  // lebih lama bukan pembaruan
    [InlineData("0.9.0", "1.0.0", true, true, false, false)]
    public void Compare_WorksForZeroDotXDotYVersions(string current, string remote, bool newer, bool major, bool minor, bool patch)
    {
        var result = VersionHelper.Compare(current, remote);

        Assert.Equal((newer, major, minor, patch), result);
    }
}
