using System;
using System.Linq;
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

    [Fact]
    public void PhoneSizeMenu_ClickingAPhone_AppliesWindowSizeWithChrome()
    {
        RunSta(() =>
        {
            double appliedW = 0, appliedH = 0;
            var menu = NotchMusic.Helpers.PhoneSizePresets.CreateMenu(
                System.Windows.Media.Brushes.White,
                (w, h) => { appliedW = w; appliedH = h; },
                () => (16, 72),
                () => new Size(1920, 1040),
                340, 420,
                () => { });

            // 5 ukuran HP + pemisah + "Putar"
            Assert.Equal(NotchMusic.Helpers.PhoneSizePresets.All.Count + 2, menu.Items.Count);

            var iphone = menu.Items.OfType<System.Windows.Controls.MenuItem>().Single(i => i.Header.ToString()!.Contains("iPhone 14"));
            iphone.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));

            Assert.Equal(390 + 16, appliedW);
            Assert.Equal(844 + 72, appliedH);
        });
    }
}
