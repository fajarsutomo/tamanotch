using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using Border = System.Windows.Controls.Border;
using Panel = System.Windows.Controls.Panel;
using ContentControl = System.Windows.Controls.ContentControl;

namespace TamaNotch.Services.Theme;

/// <summary>
/// Blur desktop asli untuk tema Liquid Glass.
/// <list type="bullet">
/// <item>Jendela berlapis (<c>AllowsTransparency</c>: island, Radio, Al-Qur'an, ...): "accent blur" lewat
/// <c>SetWindowCompositionAttribute</c>. Blur itu selalu mengisi seluruh persegi jendela (wilayah jendela tidak
/// memotongnya), jadi selama Glass aktif kartu dibuat mengisi penuh jendela (margin bayangan dinolkan) dan sudutnya
/// disamakan dengan pembulatan DWM Windows 11 (<see cref="GlassCornerRadius"/>), yang ikut membulatkan blur.</item>
/// <item>Jendela biasa (Pengaturan, jendela web): backdrop Acrylic DWM (Windows 11 build 22621+).</item>
/// </list>
/// Di tema lain semua dilepas dan nilai asli dipulihkan.
/// </summary>
public static class GlassBackdrop
{
    /// <summary>Radius sudut DWM "round" Windows 11 (8 px pada 100%).</summary>
    public const double GlassCornerRadius = 8;

    private static readonly List<Tracker> Trackers = new();
    private static bool _classHandlerRegistered;

    public static bool IsGlassActive => ThemeService.Current == ThemeService.Glass && GlassOptions.WindowBlur;

    /// <summary>Daftarkan semua jendela aplikasi secara otomatis saat dimuat.</summary>
    public static void RegisterClassHandler()
    {
        if (_classHandlerRegistered) return;
        _classHandlerRegistered = true;
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((s, _) => { if (s is Window w and not MainWindow) Attach(w); })); // island: IslandGlass
        ThemeService.Changed += _ => RefreshAll();
        GlassOptions.Changed += RefreshAll;
    }

    /// <summary>
    /// Pasang blur pada jendela. <paramref name="shape"/> = kartu yang menjadi bentuk kaca (default: kartu akar);
    /// <paramref name="isAllowed"/> = kapan blur boleh menyala (mis. island hanya saat terbuka).
    /// </summary>
    public static void Attach(Window window, FrameworkElement? shape = null, Func<bool>? isAllowed = null)
    {
        if (!window.AllowsTransparency)
        {
            SystemBackdrop.Attach(window);
            return;
        }

        var existing = Trackers.FirstOrDefault(t => ReferenceEquals(t.Window, window));
        if (existing != null)
        {
            if (shape != null && !ReferenceEquals(shape, existing.Shape))
            {
                existing.TurnOff();
                existing.Shape = shape;
            }
            if (isAllowed != null) existing.IsAllowed = isAllowed;
            existing.Update(force: true);
            return;
        }

        shape ??= FindRootCard(window);
        if (shape == null) return;

        var tracker = new Tracker(window, shape, isAllowed);
        Trackers.Add(tracker);
        window.Closed += (_, _) => { tracker.Detach(); Trackers.Remove(tracker); };
        tracker.Update(force: true);
    }

    /// <summary>Evaluasi ulang (mis. setelah status island berubah).</summary>
    public static void Refresh(Window window)
        => Trackers.FirstOrDefault(t => ReferenceEquals(t.Window, window))?.Update(force: false);

    private static void RefreshAll()
    {
        foreach (var tracker in Trackers.ToList()) tracker.Update(force: true);
        SystemBackdrop.RefreshAll();
    }

    /// <summary>Kartu akar: Border pertama yang berlatar (bukan transparan) di bawah konten jendela.</summary>
    internal static FrameworkElement? FindRootCard(Window window)
    {
        if (window.FindName("MainIslandBorder") is FrameworkElement island) return island;
        return FindCard(window.Content as DependencyObject, depth: 0);
    }

    private static FrameworkElement? FindCard(DependencyObject? node, int depth)
    {
        if (node == null || depth > 3) return null;
        if (node is Border border && HasVisibleBackground(border.Background)) return border;
        if (node is Border plain) return FindCard(plain.Child, depth + 1);
        if (node is Panel panel)
        {
            foreach (UIElement child in panel.Children)
            {
                var found = FindCard(child, depth + 1);
                if (found != null) return found;
            }
        }
        if (node is ContentControl cc) return FindCard(cc.Content as DependencyObject, depth + 1);
        return null;
    }

    private static bool HasVisibleBackground(System.Windows.Media.Brush? brush)
        => brush switch
        {
            null => false,
            SolidColorBrush solid => solid.Color.A > 0,
            _ => true
        };

    private sealed class Tracker
    {
        public Window Window { get; }
        public FrameworkElement Shape { get; set; }
        public Func<bool>? IsAllowed { get; set; }

        private bool _on;
        private readonly List<(DependencyObject Element, DependencyProperty Property, object Value)> _saved = new();

        public Tracker(Window window, FrameworkElement shape, Func<bool>? isAllowed)
        {
            Window = window;
            Shape = shape;
            IsAllowed = isAllowed;
        }

        public void Detach() => _saved.Clear();

        public void Update(bool force)
        {
            var hwnd = new WindowInteropHelper(Window).Handle;
            if (hwnd == IntPtr.Zero) return;

            var want = IsGlassActive && Shape.IsVisible && (IsAllowed?.Invoke() ?? true);
            if (want == _on && !force) return;

            if (want) TurnOn(hwnd);
            else TurnOff();
        }

        private void Override(DependencyObject element, DependencyProperty property, object value)
        {
            _saved.Add((element, property, element.ReadLocalValue(property)));
            element.SetValue(property, value);
        }

        private void TurnOn(IntPtr hwnd)
        {
            if (!_on)
            {
                // Kartu mengisi penuh jendela: nolkan margin dari kartu sampai akar jendela
                for (DependencyObject? node = Shape; node != null && node is not System.Windows.Window; node = VisualTreeHelper.GetParent(node))
                {
                    if (node is FrameworkElement fe && fe.Margin != default) Override(fe, FrameworkElement.MarginProperty, default(Thickness));
                }

                if (Shape is Border border)
                {
                    // Kaca gaya macOS: isi netral tipis dengan sorotan di atas, tepi berkilau (specular rim)
                    Override(border, Border.CornerRadiusProperty, new CornerRadius(GlassCornerRadius));
                    Override(border, Border.BackgroundProperty, GlassStyle.Fill);
                    Override(border, Border.BorderBrushProperty, GlassStyle.Rim);
                    Override(border, Border.BorderThicknessProperty, new Thickness(1.2));
                    Override(border, UIElement.EffectProperty, null!);
                }
            }

            SetCorner(hwnd, CornerRound);
            SetBorderColor(hwnd, DwmColorNone); // garis bingkai DWM diganti tepi kaca sendiri
            SetAccent(hwnd, AccentState.EnableBlurBehind);
            _on = true;
        }

        public void TurnOff()
        {
            var hwnd = new WindowInteropHelper(Window).Handle;
            if (hwnd != IntPtr.Zero)
            {
                SetAccent(hwnd, AccentState.Disabled);
                SetCorner(hwnd, CornerDoNotRound);
                SetBorderColor(hwnd, DwmColorDefault);
            }

            for (var i = _saved.Count - 1; i >= 0; i--) Restore(_saved[i].Element, _saved[i].Property, _saved[i].Value);
            _saved.Clear();
            _on = false;
        }

        private static void Restore(DependencyObject element, DependencyProperty property, object value)
        {
            if (value == DependencyProperty.UnsetValue) element.ClearValue(property);
            else if (value is BindingExpressionBase binding) BindingOperations.SetBinding(element, property, binding.ParentBindingBase);
            else element.SetValue(property, value);
        }
    }

    /// <summary>
    /// Kuas kaca gaya macOS (Liquid Glass): tidak berwarna (grafit netral, bukan biru) dan cukup bening agar blur
    /// latar terlihat; sedikit lebih terang di tepi atas (pantulan cahaya); tepi putih yang paling terang di kiri
    /// atas lalu memudar dan menyala lagi di bawah, meniru pembiasan cahaya di pinggir kaca.
    /// </summary>
    public static class GlassStyle
    {
        public static readonly System.Windows.Media.Brush Fill = Freeze(new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(0, 1),
            GradientStops =
            {
                new GradientStop(System.Windows.Media.Color.FromArgb(0x70, 0x5A, 0x5A, 0x60), 0.0),
                new GradientStop(System.Windows.Media.Color.FromArgb(0x52, 0x2E, 0x2E, 0x33), 0.10),
                new GradientStop(System.Windows.Media.Color.FromArgb(0x4A, 0x1C, 0x1C, 0x20), 0.55),
                new GradientStop(System.Windows.Media.Color.FromArgb(0x57, 0x16, 0x16, 0x19), 1.0)
            }
        });

        public static readonly System.Windows.Media.Brush Rim = Freeze(new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1),
            GradientStops =
            {
                new GradientStop(System.Windows.Media.Color.FromArgb(0xD9, 0xFF, 0xFF, 0xFF), 0.0),
                new GradientStop(System.Windows.Media.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF), 0.22),
                new GradientStop(System.Windows.Media.Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF), 0.55),
                new GradientStop(System.Windows.Media.Color.FromArgb(0x4D, 0xFF, 0xFF, 0xFF), 0.85),
                new GradientStop(System.Windows.Media.Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF), 1.0)
            }
        });

        private static System.Windows.Media.Brush Freeze(System.Windows.Media.Brush brush)
        {
            brush.Freeze();
            return brush;
        }
    }

    private const uint DwmColorNone = 0xFFFFFFFE;
    private const uint DwmColorDefault = 0xFFFFFFFF;

    private static void SetBorderColor(IntPtr hwnd, uint color)
    {
        try
        {
            var value = unchecked((int)color);
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DwmwaBorderColor, ref value, sizeof(int));
        }
        catch
        {
            // Windows 10: atribut tidak ada.
        }
    }

    private const int CornerDoNotRound = 1;
    private const int CornerRound = 2;

    private enum AccentState
    {
        Disabled = 0,
        EnableBlurBehind = 3
    }

    private static void SetCorner(IntPtr hwnd, int preference)
    {
        try
        {
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DwmwaWindowCornerPreference, ref preference, sizeof(int));
        }
        catch
        {
            // Windows 10: tidak ada pembulatan DWM; blur tetap persegi.
        }
    }

    private static void SetAccent(IntPtr hwnd, AccentState state)
    {
        var accent = new NativeMethods.AccentPolicy { AccentState = (int)state };
        var size = Marshal.SizeOf(accent);
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new NativeMethods.WindowCompositionAttributeData
            {
                Attribute = NativeMethods.WcaAccentPolicy,
                Data = ptr,
                SizeOfData = size
            };
            NativeMethods.SetWindowCompositionAttribute(hwnd, ref data);
        }
        catch
        {
            // API tidak terdokumentasi; bila tidak tersedia, tema Glass tetap jalan tanpa blur.
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    /// <summary>
    /// Jendela biasa (bukan berlapis), mis. Pengaturan: backdrop Acrylic DWM Windows 11 (build 22621+). Latar WPF dibuat
    /// transparan dan bingkai DWM diperluas ke seluruh area klien supaya akrilik terlihat di balik isi jendela.
    /// </summary>
    private static class SystemBackdrop
    {
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaSystemBackdropType = 38;
        private const int BackdropNone = 1;
        private const int BackdropAcrylic = 3;

        private static readonly List<Window> Windows = new();
        private static readonly Dictionary<Window, System.Windows.Media.Color> OriginalColors = new();
        private static readonly Dictionary<Window, object> OriginalBackgrounds = new();

        /// <summary>
        /// Lapisan di atas akrilik: akrilik Windows mengikuti mode terang sistem, sedangkan teks tema Glass putih.
        /// Gradasi: sedikit lebih terang di tepi atas (pantulan cahaya) lalu grafit netral yang cukup bening.
        /// </summary>
        private static readonly System.Windows.Media.Brush GlassTint = CreateTint();

        private static System.Windows.Media.Brush CreateTint()
        {
            var brush = new LinearGradientBrush { StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(0, 1) };
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0x8A, 0x3A, 0x3A, 0x40), 0.0));
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0x7A, 0x1E, 0x1E, 0x22), 0.12));
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0x76, 0x16, 0x16, 0x19), 1.0));
            brush.Freeze();
            return brush;
        }

        private static bool Supported => Environment.OSVersion.Version.Build >= 22621;

        public static void Attach(Window window)
        {
            if (!Supported || Windows.Contains(window)) return;
            Windows.Add(window);
            window.Closed += (_, _) => { Windows.Remove(window); OriginalColors.Remove(window); OriginalBackgrounds.Remove(window); };
            Apply(window);
        }

        public static void RefreshAll()
        {
            foreach (var window in Windows.ToList()) Apply(window);
        }

        private static void Apply(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero || HwndSource.FromHwnd(hwnd) is not { CompositionTarget: { } target }) return;

            try
            {
                if (IsGlassActive)
                {
                    if (!OriginalColors.ContainsKey(window)) OriginalColors[window] = target.BackgroundColor;
                    if (!OriginalBackgrounds.ContainsKey(window)) OriginalBackgrounds[window] = window.ReadLocalValue(System.Windows.Controls.Control.BackgroundProperty);
                    window.Background = GlassTint;
                    target.BackgroundColor = Colors.Transparent;
                    var margins = new NativeMethods.Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
                    NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);
                    int dark = 1, backdrop = BackdropAcrylic;
                    NativeMethods.DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
                    NativeMethods.DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
                    SetBorderColor(hwnd, 0x00D9D9D9); // tepi kaca terang (COLORREF 0x00BBGGRR)
                }
                else if (OriginalColors.TryGetValue(window, out var original))
                {
                    SetBorderColor(hwnd, DwmColorDefault);
                    int backdrop = BackdropNone;
                    NativeMethods.DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
                    var margins = new NativeMethods.Margins();
                    NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);
                    target.BackgroundColor = original;
                    OriginalColors.Remove(window);
                    if (OriginalBackgrounds.Remove(window, out var background))
                    {
                        if (background == DependencyProperty.UnsetValue) window.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
                        else window.SetValue(System.Windows.Controls.Control.BackgroundProperty, background);
                    }
                }
            }
            catch
            {
                // DWM tidak tersedia: tema Glass tetap jalan tanpa akrilik.
            }
        }
    }

    private static class NativeMethods
    {
        public const int WcaAccentPolicy = 19;
        public const int DwmwaWindowCornerPreference = 33;
        public const int DwmwaBorderColor = 34;

        [StructLayout(LayoutKind.Sequential)]
        public struct AccentPolicy
        {
            public int AccentState;
            public int AccentFlags;
            public uint GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Margins
        {
            public int Left, Right, Top, Bottom;
        }

        [DllImport("user32.dll")]
        public static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [DllImport("dwmapi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
