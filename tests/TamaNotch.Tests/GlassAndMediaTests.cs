using TamaNotch.Services.Media;
using TamaNotch.Services.Theme;
using Xunit;

namespace TamaNotch.Tests;

public class GlassAndMediaTests
{
    [Theory]
    [InlineData(2.3, true)]    // bunyi notifikasi WhatsApp Web
    [InlineData(14.9, true)]
    [InlineData(15, false)]
    [InlineData(210, false)]   // lagu
    [InlineData(0, false)]     // siaran langsung / durasi tidak diketahui
    public void ShortClips_AreTreatedAsNotificationSounds(double seconds, bool expected)
        => Assert.Equal(expected, WindowsMediaSessionService.IsNotificationClip(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void GlassTint_FollowsTheSetting_AndThickensOnBrightBackdropsWhenAdaptive()
    {
        Assert.Equal(38, GlassOptions.TintAlpha(0, 15, adaptive: true));      // 15% di atas latar gelap
        Assert.Equal(128, GlassOptions.TintAlpha(1, 15, adaptive: true));     // +35 poin di atas latar putih
        Assert.Equal(38, GlassOptions.TintAlpha(1, 15, adaptive: false));     // tidak adaptif: tetap
        Assert.Equal(0, GlassOptions.TintAlpha(0, 0, adaptive: false));       // paling bening
        Assert.Equal(230, GlassOptions.TintAlpha(1, 80, adaptive: true));     // dibatasi 90%
    }

    [Fact]
    public void GlassOptions_ClampOutOfRangeSettings()
    {
        GlassOptions.Load(new TamaNotch.Models.AppSettings { GlassBlurStrength = 99, GlassTintPercent = -5, GlassRefreshMs = 5 });
        try
        {
            Assert.Equal(GlassOptions.MaxBlur, GlassOptions.BlurStrength);
            Assert.Equal(GlassOptions.MinTint, GlassOptions.TintPercent);
            Assert.Equal(GlassOptions.MinRefreshMs, GlassOptions.RefreshMs);
        }
        finally
        {
            GlassOptions.Load(new TamaNotch.Models.AppSettings());
        }
    }

    [Theory]
    [InlineData(0.5, 0)]
    [InlineData(6.9, 0)]
    [InlineData(7, 1)]    // 1 minggu: kuning
    [InlineData(13.9, 1)]
    [InlineData(14, 2)]   // 2 minggu: oranye
    [InlineData(21, 3)]   // 3 minggu: merah
    [InlineData(60, 3)]
    public void UptimeLevel_ChangesColorEveryWeek(double days, int level)
        => Assert.Equal(level, TamaNotch.ViewModels.MainViewModel.UptimeLevel(TimeSpan.FromDays(days)));

    [Fact]
    public void UptimeColors_AreNeonGreenYellowOrangeRed()
    {
        var c = TamaNotch.ViewModels.MainViewModel.UptimeColors;
        Assert.Equal(4, c.Length);
        Assert.True(c[0].G > 200 && c[0].R < 100);             // hijau
        Assert.True(c[1].R > 200 && c[1].G > 200 && c[1].B < 50); // kuning
        Assert.True(c[2].R > 200 && c[2].G is > 100 and < 180);   // oranye
        Assert.True(c[3].R > 200 && c[3].G < 80);              // merah
    }

    [Fact]
    public void BoxBlur_SpreadsABrightPixel_AndKeepsAlphaOpaque()
    {
        const int w = 9, h = 9, stride = w * 4;
        var px = new byte[stride * h];
        var center = 4 * stride + 4 * 4;
        px[center] = px[center + 1] = px[center + 2] = 255;

        IslandGlass.BoxBlur(px, w, h, stride, 1);

        Assert.True(px[center] < 255 && px[center] > 0);
        Assert.True(px[3 * stride + 3 * 4] > 0, "tetangga diagonal ikut terang");
        Assert.Equal(0, px[0]);
        Assert.All(Enumerable.Range(0, w * h), i => Assert.Equal(255, px[i * 4 + 3]));
    }

    [Fact]
    public void AverageLuminance_IsZeroForBlack_AndOneForWhite()
    {
        const int w = 4, h = 2, stride = w * 4;
        var black = new byte[stride * h];
        var white = Enumerable.Repeat((byte)255, stride * h).ToArray();

        Assert.Equal(0, IslandGlass.AverageLuminance(black, w, h, stride), 3);
        Assert.Equal(1, IslandGlass.AverageLuminance(white, w, h, stride), 3);
    }

    [Fact]
    public void WindowGlass_FollowsTheTintSlider()
    {
        Assert.Equal(1.0, GlassBackdrop.GlassStyle.TintScale(GlassOptions.DefaultTint), 3);   // bawaan: tampilan lama
        Assert.Equal(0.5, GlassBackdrop.GlassStyle.TintScale(0), 3);                          // paling bening
        Assert.True(GlassBackdrop.GlassStyle.TintScale(80) > 3);                              // paling pekat

        GlassOptions.Load(new TamaNotch.Models.AppSettings { GlassTintPercent = 0 });
        try
        {
            var clear = ((System.Windows.Media.LinearGradientBrush)GlassBackdrop.GlassStyle.Fill).GradientStops[2].Color.A;
            GlassOptions.Load(new TamaNotch.Models.AppSettings { GlassTintPercent = 60 });
            var dense = ((System.Windows.Media.LinearGradientBrush)GlassBackdrop.GlassStyle.Fill).GradientStops[2].Color.A;
            Assert.True(dense > clear);
        }
        finally
        {
            GlassOptions.Load(new TamaNotch.Models.AppSettings());
        }
    }
}
