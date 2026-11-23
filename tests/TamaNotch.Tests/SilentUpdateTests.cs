using System;
using TamaNotch.Services.Update;
using Xunit;

namespace TamaNotch.Tests;

/// <summary>Pembaruan tanpa wizard: argumen installer dan kapan pemasangan otomatis boleh berjalan.</summary>
public class SilentUpdateTests
{
    [Fact]
    public void InstallerArguments_AreSilent_CloseTheApp_AndRelaunchIt()
    {
        var args = UpdateService.BuildSilentInstallerArguments();

        Assert.Contains("/VERYSILENT", args);
        Assert.Contains("/SUPPRESSMSGBOXES", args);
        Assert.Contains("/CLOSEAPPLICATIONS", args);
        Assert.Contains("/NORESTART", args);
        Assert.Contains("/RELAUNCH", args);
        Assert.DoesNotContain("/LOG", args);
    }

    [Fact]
    public void InstallerArguments_QuoteTheLogPath()
    {
        var args = UpdateService.BuildSilentInstallerArguments(@"C:\Users\A B\Temp\install.log");

        Assert.EndsWith(@"/LOG=""C:\Users\A B\Temp\install.log""", args);
    }

    [Fact]
    public void Installs_WhenNothingIsHappening_AndUserIsAway()
        => Assert.True(UpdateInstallPolicy.IsGoodMomentToInstall(false, false, false, TimeSpan.FromMinutes(5)));

    [Theory]
    [InlineData(true, false, false, 10)]  // musik/radio/TV/Al-Qur'an diputar
    [InlineData(false, true, false, 10)]  // adzan berjalan
    [InlineData(false, false, true, 10)]  // island sedang terbuka
    [InlineData(false, false, false, 4)]  // pengguna baru saja memakai PC
    public void Waits_WhenTheAppOrUserIsBusy(bool playback, bool adhan, bool islandOpen, int idleMinutes)
        => Assert.False(UpdateInstallPolicy.IsGoodMomentToInstall(playback, adhan, islandOpen, TimeSpan.FromMinutes(idleMinutes)));
}
