using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Application = System.Windows.Application;
using Point = System.Windows.Point;

namespace NotchMusic.Helpers;

/// <summary>
/// Animasi jendela menuju notch: isi jendela mengecil dan memudar ke arah notch idle, lalu jendela disembunyikan.
/// Hanya untuk jendela tembus pandang berisi WPF murni; WebView2 tidak ikut tertransformasi (airspace).
/// </summary>
public static class WindowAnimator
{
    public static readonly TimeSpan CloseDuration = TimeSpan.FromMilliseconds(300);

    public static readonly TimeSpan OpenDuration = TimeSpan.FromMilliseconds(340);

    /// <summary>Skala akhir sebelum jendela disembunyikan (hampir titik).</summary>
    private const double EndScale = 0.03;

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, State> States = new();

    private sealed class State
    {
        /// <summary>Animasi buka atau tutup sedang berjalan.</summary>
        public bool Busy;
        public int Version;
    }

    /// <summary>
    /// Titik notch (koordinat layar, DIP) untuk jendela tertentu, atau null bila notch belum tampil. Dapat diganti (pengujian).
    /// </summary>
    public static Func<Window, Point?> NotchLocator { get; set; } = DefaultNotchLocator;

    /// <summary>Mengikuti pengaturan Windows "Animation effects"; mati bila pengguna mematikannya.</summary>
    public static bool Enabled => SystemParameters.ClientAreaAnimation;

    /// <summary>Sembunyikan jendela dengan animasi tersedot ke notch; tanpa animasi bila dimatikan atau tidak memungkinkan.</summary>
    public static void HideToNotch(this Window window)
    {
        if (!window.IsVisible) return;

        var state = States.GetOrCreateValue(window);
        if (state.Busy) return;

        if (!Enabled || window.Content is not FrameworkElement root || window.ActualWidth <= 0 || window.ActualHeight <= 0)
        {
            window.Hide();
            return;
        }

        var target = NotchLocator(window) ?? new Point(window.Left + window.ActualWidth / 2.0, 0);
        root.RenderTransformOrigin = ComputeRelativeOrigin(target, window.Left, window.Top, window.ActualWidth, window.ActualHeight);

        var scale = new ScaleTransform(1, 1);
        root.RenderTransform = scale;
        root.CacheMode = new BitmapCache(1.0);   // bayangan jendela dirender sekali, bukan tiap frame

        state.Busy = true;
        int version = ++state.Version;

        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var shrink = new DoubleAnimation(1, EndScale, CloseDuration) { EasingFunction = ease };
        var fade = new DoubleAnimation(1, 0, CloseDuration) { EasingFunction = ease };

        shrink.Completed += (s, e) =>
        {
            if (version != state.Version) return;   // dibatalkan (jendela dibuka lagi) selagi bergerak
            Reset(root);
            state.Busy = false;
            window.Hide();
        };

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink.Clone());
        root.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    /// <summary>
    /// Tampilkan jendela dengan animasi keluar dari notch (kebalikan <see cref="HideToNotch"/>). Bila jendela masih tampil
    /// (mis. sedang menutup), animasi dibatalkan dan jendela dipulihkan. Tanpa animasi bila dimatikan, atau notch belum tampil
    /// (mis. saat aplikasi baru mulai dan jendela dipulihkan dari sesi lalu).
    /// </summary>
    public static void ShowFromNotch(this Window window)
    {
        if (window.IsVisible)
        {
            Cancel(window);
            return;
        }

        if (!Enabled || window.Content is not FrameworkElement root || NotchLocator(window) == null)
        {
            window.Show();
            return;
        }

        var state = States.GetOrCreateValue(window);
        state.Busy = true;
        int version = ++state.Version;

        // Posisi akhir jendela baru pasti setelah Loaded; sembunyikan isinya dulu agar tidak berkedip utuh sebelum animasi.
        root.Opacity = 0;
        window.Show();

        window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            if (version != state.Version)
            {
                System.Diagnostics.Debug.WriteLine($"[WindowAnimator] open dibatalkan (versi {version} != {state.Version})");
                return;
            }
            if (!window.IsVisible || window.ActualWidth <= 0 || window.ActualHeight <= 0)
            {
                Reset(root);
                state.Busy = false;
                return;
            }

            var target = NotchLocator(window) ?? new Point(window.Left + window.ActualWidth / 2.0, 0);
            root.RenderTransformOrigin = ComputeRelativeOrigin(target, window.Left, window.Top, window.ActualWidth, window.ActualHeight);
            var scale = new ScaleTransform(EndScale, EndScale);
            root.RenderTransform = scale;
            root.CacheMode = new BitmapCache(1.0);

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var grow = new DoubleAnimation(EndScale, 1, OpenDuration) { EasingFunction = ease };
            var fade = new DoubleAnimation(0, 1, OpenDuration) { EasingFunction = ease };
            grow.Completed += (s, e) =>
            {
                if (version != state.Version) return;
                Reset(root);
                state.Busy = false;
            };

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow.Clone());
            root.BeginAnimation(UIElement.OpacityProperty, fade);
        }));
    }

    /// <summary>Batalkan animasi yang sedang berjalan (mis. jendela dibuka lagi saat menutup): isi kembali utuh dan tetap tampil.</summary>
    public static void Cancel(Window? window)
    {
        if (window == null || !States.TryGetValue(window, out var state) || !state.Busy) return;

        state.Version++;
        state.Busy = false;
        if (window.Content is FrameworkElement root) Reset(root);
    }

    private static void Reset(FrameworkElement root)
    {
        root.BeginAnimation(UIElement.OpacityProperty, null);
        if (root.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }
        root.RenderTransform = Transform.Identity;
        root.RenderTransformOrigin = new Point(0, 0);
        root.ClearValue(UIElement.CacheModeProperty);
        root.Opacity = 1;
    }

    /// <summary>Tengah notch utama (jendela utama aplikasi) bila sedang tampil.</summary>
    private static Point? DefaultNotchLocator(Window window)
    {
        var main = Application.Current?.MainWindow;
        if (main == null || main == window || !main.IsVisible || main.ActualWidth <= 0) return null;
        // Saat island sedang terbuka (hover), tujuan tetap kapsul idle, bukan tengah island.
        double x = main is NotchMusic.MainWindow mw && mw.IdleNotchCenterX is double idleX ? idleX : main.Left + main.ActualWidth / 2.0;
        return new Point(x, main.Top + Math.Min(main.ActualHeight, 40.0) / 2.0);
    }

    /// <summary>Titik tujuan sebagai asal transformasi relatif terhadap jendela (boleh di luar 0..1 saat tujuan di luar jendela).</summary>
    public static Point ComputeRelativeOrigin(Point target, double left, double top, double width, double height)
        => new((target.X - left) / width, (target.Y - top) / height);
}
