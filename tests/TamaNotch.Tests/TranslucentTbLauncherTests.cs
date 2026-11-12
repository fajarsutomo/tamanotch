using System.IO;
using TamaNotch.Services.Taskbar;
using Xunit;

namespace TamaNotch.Tests;

public class TranslucentTbLauncherTests
{
    [Fact]
    public void CandidatePaths_CoverPortableStoreAndProgramFilesInstalls()
    {
        var paths = TranslucentTbLauncher.CandidatePaths(@"C:\L", @"C:\PF", @"C:\PF86").ToList();

        Assert.Equal(Path.Combine(@"C:\L", "TranslucentTB", "TranslucentTB.exe"), paths[0]);
        Assert.Contains(paths, p => p.Contains("WindowsApps"));
        Assert.Contains(Path.Combine(@"C:\PF", "TranslucentTB", "TranslucentTB.exe"), paths);
        Assert.All(paths, p => Assert.EndsWith("TranslucentTB.exe", p));
    }

    [Fact]
    public void LaunchTranslucentTb_IsOffByDefault()
        => Assert.False(new TamaNotch.Models.AppSettings().LaunchTranslucentTb);
}
