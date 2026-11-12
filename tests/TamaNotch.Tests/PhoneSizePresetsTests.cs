using System.Linq;
using TamaNotch.Helpers;
using Xunit;

namespace TamaNotch.Tests;

public class PhoneSizePresetsTests
{
    private static readonly PhoneSize Phone = new("Uji", 390, 844);

    [Fact]
    public void Presets_ArePortraitAndHaveUniqueNames()
    {
        Assert.True(PhoneSizePresets.All.Count >= 5);
        Assert.All(PhoneSizePresets.All, p => Assert.True(p.Height > p.Width));
        Assert.Equal(PhoneSizePresets.All.Count, PhoneSizePresets.All.Select(p => p.Name).Distinct().Count());
        Assert.Contains("390 × 844", PhoneSizePresets.All.Single(p => p.Name == "iPhone 14 / 15").Label);
    }

    [Fact]
    public void ToWindowSize_AddsWindowChromeToPhoneSize()
    {
        var (w, h) = PhoneSizePresets.ToWindowSize(Phone, chromeWidth: 16, chromeHeight: 72,
            maxWidth: 2000, maxHeight: 2000, minWidth: 340, minHeight: 420);

        Assert.Equal(406, w);
        Assert.Equal(916, h);
    }

    [Fact]
    public void ToWindowSize_TooTallForScreen_ScalesDownKeepingAspect()
    {
        var (w, h) = PhoneSizePresets.ToWindowSize(Phone, 16, 72, maxWidth: 2000, maxHeight: 800, minWidth: 340, minHeight: 420);

        Assert.True(h <= 800);
        Assert.Equal(406.0 / 916.0, w / h, 2);
    }

    [Fact]
    public void ToWindowSize_ScaledBelowMinimumWidth_IsClampedToMinimum()
    {
        var (w, h) = PhoneSizePresets.ToWindowSize(Phone, 16, 72, maxWidth: 2000, maxHeight: 700, minWidth: 340, minHeight: 420);

        Assert.Equal(340, w);   // 310 setelah diperkecil, dijepit ke minimum
        Assert.True(h <= 700);
    }

    [Fact]
    public void ToWindowSize_NeverBelowWindowMinimum()
    {
        var tiny = new PhoneSize("Kecil", 100, 100);

        var (w, h) = PhoneSizePresets.ToWindowSize(tiny, 0, 0, 2000, 2000, minWidth: 340, minHeight: 420);

        Assert.Equal(340, w);
        Assert.Equal(420, h);
    }

    [Fact]
    public void ToWindowSize_NegativeChromeIsIgnored()
    {
        var (w, h) = PhoneSizePresets.ToWindowSize(Phone, -50, -50, 2000, 2000, 340, 420);

        Assert.Equal(390, w);
        Assert.Equal(844, h);
    }
}
