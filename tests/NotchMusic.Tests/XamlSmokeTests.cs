using System;
using System.Threading;
using System.Windows;
using NotchMusic.Views;
using Xunit;

namespace NotchMusic.Tests;

/// <summary>Memastikan XAML jendela dapat dimuat (resource, style, dan binding tidak memicu XamlParseException).</summary>
public class XamlSmokeTests
{
    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw new Xunit.Sdk.XunitException("Gagal memuat XAML: " + failure);
    }

    [Fact]
    public void SettingsWindow_Loads_WithNotchCustomizationSection()
    {
        RunSta(() =>
        {
            var settings = new ViewModelTestFactory.MemorySettings();
            var vm = ViewModelTestFactory.CreateSettings(settings);

            var window = new SettingsWindow(vm);
            window.ApplyTemplate();
            window.Measure(new Size(520, 2000));

            Assert.NotNull(window.FindResource("SwatchListBox"));
            Assert.Same(vm, window.DataContext);
            window.Close();
        });
    }

    [Fact]
    public void NotchEyes_EyeColor_UpdatesFillAndGlow()
    {
        RunSta(() =>
        {
            var eyes = new NotchEyes();
            eyes.EyeColor = System.Windows.Media.Color.FromRgb(0xFF, 0x3D, 0xD0);

            var left = (System.Windows.Shapes.Ellipse)eyes.FindName("LeftEye");
            var fill = (System.Windows.Media.SolidColorBrush)left.Fill;
            var glow = (System.Windows.Media.Effects.DropShadowEffect)left.Effect;

            Assert.Equal(System.Windows.Media.Color.FromRgb(0xFF, 0x3D, 0xD0), glow.Color);
            // Isi lebih terang daripada warna cahaya
            Assert.True(fill.Color.G > 0x3D);
            Assert.Equal(0xFF, fill.Color.R);
        });
    }
}
