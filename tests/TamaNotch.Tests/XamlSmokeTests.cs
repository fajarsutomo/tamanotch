using System;
using System.Linq;
using System.Threading;
using System.Windows;
using TamaNotch.Views;
using Xunit;

namespace TamaNotch.Tests;

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
            window.Measure(new Size(780, 2000));

            Assert.NotNull(window.FindResource("SwatchListBox"));
            Assert.Same(vm, window.DataContext);
            window.Close();
        });
    }

    private static IEnumerable<System.Windows.Controls.Border> CategoryCards(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is System.Windows.Controls.Border b && b.Tag is string) yield return b;
            foreach (var nested in CategoryCards(child)) yield return nested;
        }
    }

    [Fact]
    public void SettingsWindow_Sidebar_EveryCardBelongsToACategory_AndOnlyTheSelectedOneShows()
    {
        RunSta(() =>
        {
            var vm = ViewModelTestFactory.CreateSettings(new ViewModelTestFactory.MemorySettings());
            var window = new SettingsWindow(vm);
            // Binding gaya (MultiBinding) baru aktif setelah jendela tampil; tampilkan di luar layar tanpa mengambil fokus.
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -20000; window.Top = -20000;
            window.ShowActivated = false; window.ShowInTaskbar = false;
            window.Show();

            var cards = CategoryCards(window).Where(b => b.Style == window.FindResource("CategoryCard")).ToList();
            Assert.NotEmpty(cards);
            Assert.All(cards, c => Assert.Contains((string)c.Tag, vm.Categories));
            Assert.All(vm.Categories, cat => Assert.Contains(cards, c => (string)c.Tag == cat));

            foreach (var category in vm.Categories)
            {
                vm.SelectedCategory = category;
                window.UpdateLayout();
                var actual = string.Join(", ", cards.Select(c => c.Tag + "=" + c.Visibility));
                foreach (var c in cards)
                    Assert.True(((string)c.Tag == category) == (c.Visibility == Visibility.Visible), $"Pilih [{category}]: {actual}");
            }
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
            var menu = TamaNotch.Helpers.PhoneSizePresets.CreateMenu(
                System.Windows.Media.Brushes.White,
                (w, h) => { appliedW = w; appliedH = h; },
                () => (16, 72),
                () => new Size(1920, 1040),
                340, 420,
                () => { });

            // 5 ukuran HP + pemisah + "Putar"
            Assert.Equal(TamaNotch.Helpers.PhoneSizePresets.All.Count + 2, menu.Items.Count);

            var iphone = menu.Items.OfType<System.Windows.Controls.MenuItem>().Single(i => i.Header.ToString()!.Contains("iPhone 14"));
            iphone.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));

            Assert.Equal(390 + 16, appliedW);
            Assert.Equal(844 + 72, appliedH);
        });
    }

    [Fact]
    public void ToolWindows_Load_WithThemeResourcesAndMaterialIcons()
    {
        RunSta(() =>
        {
            var settings = new ViewModelTestFactory.MemorySettings();
            var vm = ViewModelTestFactory.CreateMain();
            var windows = new System.Windows.Window[]
            {
                new RadioWindow(vm, settings),
                new QuranWindow(vm, settings),
                new PrayerWindow(vm, settings),
                new BmkgWindow(vm, settings),
                new ClaudeUsageWindow(vm, settings),
                new TodoWindow(vm, settings),
                new MusicWindow(vm, settings),
            };

            foreach (var window in windows)
            {
                window.ApplyTemplate();
                window.Measure(new Size(900, 900));
                Assert.Same(vm, window.DataContext);
                window.Close();
            }
        });
    }

    [Fact]
    public void EmbeddedPanels_Load_AndRaiseCloseRequested()
    {
        RunSta(() =>
        {
            var vm = ViewModelTestFactory.CreateMain();
            var closed = 0;
            var panels = new System.Windows.Controls.UserControl[]
            {
                new RadioPanel(), new QuranPanel(), new PrayerPanel(), new ClaudeUsagePanel(), new NotesPanel(), new MonitorPanel()
            };

            foreach (var panel in panels)
            {
                panel.DataContext = vm;
                panel.Measure(new Size(410, 900));
                Assert.True(panel.DesiredSize.Height > 0, $"{panel.GetType().Name} kosong");
            }

            ((RadioPanel)panels[0]).CloseRequested += (s, e) => closed++;
            var close = typeof(RadioPanel).GetMethod("CloseButton_Click", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            close.Invoke(panels[0], new object[] { panels[0], new RoutedEventArgs() });
            Assert.Equal(1, closed);
        });
    }
}
