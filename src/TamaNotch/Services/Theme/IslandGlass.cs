using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;
using WpfPath = System.Windows.Shapes.Path;

namespace TamaNotch.Services.Theme;

/// <summary>
/// Liquid Glass untuk island. Blur bawaan Windows (accent/DWM) selalu berbentuk persegi jendela, sehingga island tidak
/// bisa menempel ke tepi atas layar dengan lekukan "tetes air". Di sini latar island dibuat sendiri: area layar di
/// belakang jendela disalin berkala, diperkecil dan di-blur, lalu dipakai sebagai kuas latar island (atau bentuk tetes),
/// jadi bentuknya bebas dan tingkat transparansinya bisa diatur. Supaya island tidak ikut tersalin, jendela dikecualikan
/// dari tangkapan layar (<c>WDA_EXCLUDEFROMCAPTURE</c>) selama tema Glass aktif; akibatnya island juga tidak tampak di
/// screenshot atau berbagi layar.
/// </summary>
public sealed class IslandGlass
{
    /// <summary>Faktor perkecilan salinan layar; blur dilakukan di ukuran kecil lalu diregangkan (lebih halus dan murah).</summary>
    private const int Downscale = 6;

    private readonly Window _window;
    private readonly FrameworkElement _island;
    private readonly WpfPath _drop;
    private readonly Func<bool> _useDropShape;
    private readonly DispatcherTimer _timer;

    private bool _active;
    private BitmapSource? _frame;
    private double _frameLuminance;

    private bool _captureQueued;

    public IslandGlass(Window window, FrameworkElement island, WpfPath drop, Func<bool> useDropShape)
    {
        _window = window;
        _island = island;
        _drop = drop;
        _useDropShape = useDropShape;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(GlassOptions.RefreshMs) };
        _timer.Tick += (_, _) => Capture();

        ThemeService.Changed += _ => Refresh();
        GlassOptions.Changed += () =>
        {
            _timer.Interval = TimeSpan.FromMilliseconds(GlassOptions.RefreshMs);
            Refresh();
            QueueCapture();
        };
        window.IsVisibleChanged += (_, _) => Refresh();
        window.Closed += (_, _) => Stop();
        island.SizeChanged += (_, _) => QueueCapture();
        drop.SizeChanged += (_, _) => QueueCapture();
    }

    public static bool IsActiveTheme => ThemeService.Current == ThemeService.Glass;

    /// <summary>Evaluasi ulang setelah tema, visibilitas, atau bentuk island berubah.</summary>
    public void Refresh()
    {
        var want = IsActiveTheme && GlassOptions.IslandBlur && _window.IsVisible;
        if (want && !_active) Start();
        else if (!want && _active) Stop();
        else if (_active) ApplyBrush();

        if (!_active) ApplyPlainGlass();
    }

    /// <summary>
    /// Glass tanpa blur island (dimatikan di Pengaturan): kaca tembus pandang polos yang tetap terlihat saat berbagi
    /// layar. Tanpa blur, latar di belakangnya tajam, jadi warnanya minimal 55% agar teks terbaca.
    /// </summary>
    private void ApplyPlainGlass()
    {
        if (!IsActiveTheme)
        {
            _island.ClearValue(System.Windows.Controls.Border.BackgroundProperty);
            RestoreDropFill();
            return;
        }

        var alpha = (byte)Math.Round(255 * Math.Max(GlassOptions.TintPercent, 55) / 100.0);
        var brush = new SolidColorBrush(Color.FromArgb(alpha, 0x18, 0x18, 0x1B));
        brush.Freeze();
        _island.SetValue(System.Windows.Controls.Border.BackgroundProperty, brush);
        _drop.Fill = brush;
    }

    private void Start()
    {
        var hwnd = new WindowInteropHelper(_window).Handle;
        if (hwnd == IntPtr.Zero) return;
        _active = true;
        SetWindowDisplayAffinity(hwnd, WdaExcludeFromCapture);
        _timer.Start();
        QueueCapture();
    }

    private void Stop()
    {
        if (!_active) return;
        _active = false;
        _timer.Stop();
        var hwnd = new WindowInteropHelper(_window).Handle;
        if (hwnd != IntPtr.Zero) SetWindowDisplayAffinity(hwnd, WdaNone);
        _island.ClearValue(System.Windows.Controls.Border.BackgroundProperty);
        RestoreDropFill();
        _frame = null;
    }

    /// <summary>Salin ulang setelah render berikutnya (mis. island baru saja berubah ukuran).</summary>
    private void QueueCapture()
    {
        if (!_active || _captureQueued) return;
        _captureQueued = true;
        _window.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _captureQueued = false;
            Capture();
        }));
    }

    private void Capture()
    {
        if (!_active || !_window.IsVisible) return;
        var hwnd = new WindowInteropHelper(_window).Handle;
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var r)) return;
        int width = r.Right - r.Left, height = r.Bottom - r.Top;
        if (width < 8 || height < 8) return;

        try
        {
            int sw = Math.Max(2, width / Downscale), sh = Math.Max(2, height / Downscale);
            using var full = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            using (var g = System.Drawing.Graphics.FromImage(full))
                g.CopyFromScreen(r.Left, r.Top, 0, 0, new System.Drawing.Size(width, height));

            using var small = new System.Drawing.Bitmap(sw, sh, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            using (var g = System.Drawing.Graphics.FromImage(small))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                g.DrawImage(full, new System.Drawing.Rectangle(0, 0, sw, sh));
            }

            var data = small.LockBits(new System.Drawing.Rectangle(0, 0, sw, sh),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            var pixels = new byte[data.Stride * sh];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            var stride = data.Stride;
            small.UnlockBits(data);

            BoxBlur(pixels, sw, sh, stride, GlassOptions.BlurStrength);
            BoxBlur(pixels, sw, sh, stride, GlassOptions.BlurStrength);
            _frameLuminance = AverageLuminance(pixels, sw, sh, stride);

            var frame = BitmapSource.Create(sw, sh, 96, 96, PixelFormats.Bgr32, null, pixels, stride);
            frame.Freeze();
            _frame = frame;

            ApplyBrush();
        }
        catch
        {
            // Layar terkunci / UAC: lewati satu salinan
        }
    }

    /// <summary>Isi bentuk tetes di XAML adalah resource tema; kembalikan itu (bukan dikosongkan).</summary>
    private void RestoreDropFill() => _drop.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "ThemeIslandIdleBg");

    private void ApplyBrush()
    {
        if (!_active || _frame == null) return;
        var source = PresentationSource.FromVisual(_window);
        if (source?.CompositionTarget == null) return;
        var toDevice = source.CompositionTarget.TransformToDevice;

        FrameworkElement target = _useDropShape() ? _drop : _island;
        if (!_window.IsAncestorOf(target) || target.ActualWidth <= 0 && target is not WpfPath) return;

        Rect local = target is WpfPath { Data: { } geometry } ? geometry.Bounds : new Rect(target.RenderSize);
        if (local.IsEmpty || local.Width < 1 || local.Height < 1) return;

        // Persegi target di koordinat jendela (DIP) -> piksel layar -> piksel salinan kecil
        var inWindow = target.TransformToAncestor(_window).TransformBounds(local);
        var px = Rect.Transform(inWindow, toDevice);
        var viewbox = new Rect(px.X / Downscale, px.Y / Downscale, px.Width / Downscale, px.Height / Downscale);

        var brush = BuildBrush(_frame, viewbox, _frameLuminance);
        if (ReferenceEquals(target, _drop))
        {
            _drop.Fill = brush;
            _island.ClearValue(System.Windows.Controls.Border.BackgroundProperty);
        }
        else
        {
            _island.SetValue(System.Windows.Controls.Border.BackgroundProperty, brush);
            RestoreDropFill();
        }
    }

    /// <summary>
    /// Kuas kaca: salinan ter-blur + warna kaca tipis + sorotan di tepi atas. Warna kaca menyesuaikan terang latar
    /// (latar terang -> sedikit lebih gelap) supaya teks putih tetap terbaca, mirip "vibrancy" macOS.
    /// </summary>
    public static Brush BuildBrush(BitmapSource frame, Rect viewbox, double luminance)
    {
        var group = new DrawingGroup();
        var area = new RectangleGeometry(viewbox);
        group.Children.Add(new ImageDrawing(frame, new Rect(0, 0, frame.PixelWidth, frame.PixelHeight)));
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromArgb(GlassOptions.TintAlpha(luminance, GlassOptions.TintPercent, GlassOptions.AdaptiveTint), 0x12, 0x12, 0x14)), null, area));

        var sheen = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        sheen.GradientStops.Add(new GradientStop(Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF), 0.0));
        sheen.GradientStops.Add(new GradientStop(Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF), 0.18));
        sheen.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 0.5));
        group.Children.Add(new GeometryDrawing(sheen, null, area));
        group.ClipGeometry = area;

        var brush = new DrawingBrush(group)
        {
            Viewbox = viewbox,
            ViewboxUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.Fill
        };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.Linear);
        brush.Freeze();
        return brush;
    }

    public static double AverageLuminance(byte[] bgra, int width, int height, int stride)
    {
        if (width <= 0 || height <= 0) return 0;
        double sum = 0;
        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            for (var x = 0; x < width; x++)
            {
                var i = row + x * 4;
                sum += 0.0722 * bgra[i] + 0.7152 * bgra[i + 1] + 0.2126 * bgra[i + 2];
            }
        }
        return sum / (width * height * 255.0);
    }

    /// <summary>Box blur horizontal lalu vertikal (BGRA, alpha diabaikan). Dua lintasan mendekati blur Gauss.</summary>
    public static void BoxBlur(byte[] px, int width, int height, int stride, int radius)
    {
        if (radius <= 0) return;
        var temp = new byte[px.Length];
        Pass(px, temp, width, height, stride, radius, horizontal: true);
        Pass(temp, px, width, height, stride, radius, horizontal: false);
    }

    private static void Pass(byte[] src, byte[] dst, int width, int height, int stride, int radius, bool horizontal)
    {
        int lines = horizontal ? height : width, length = horizontal ? width : height;
        for (var line = 0; line < lines; line++)
        {
            for (var c = 0; c < 3; c++)
            {
                int sum = 0;
                for (var k = -radius; k <= radius; k++) sum += src[Index(line, Math.Clamp(k, 0, length - 1))];
                for (var i = 0; i < length; i++)
                {
                    dst[Index(line, i)] = (byte)(sum / (2 * radius + 1));
                    var add = Math.Min(i + radius + 1, length - 1);
                    var remove = Math.Max(i - radius, 0);
                    sum += src[Index(line, add)] - src[Index(line, remove)];
                }

                int Index(int l, int i) => horizontal ? l * stride + i * 4 + c : i * stride + l * 4 + c;
            }

            // alpha tetap pekat
            for (var i = 0; i < length; i++)
            {
                var idx = horizontal ? line * stride + i * 4 + 3 : i * stride + line * 4 + 3;
                dst[idx] = 255;
            }
        }
    }

    private const uint WdaNone = 0x0;
    private const uint WdaExcludeFromCapture = 0x11;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
}
