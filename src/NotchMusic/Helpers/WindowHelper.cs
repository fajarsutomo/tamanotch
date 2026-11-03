using Size = System.Windows.Size;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NotchMusic.Helpers;

public static class WindowHelper
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public static void SetClickThrough(Window window, bool clickThrough)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        if (clickThrough)
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
        }
        else
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
        }
    }

    public static void SetToolWindow(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TOOLWINDOW);
    }

    public static void EnsureInVisibleScreen(Window window)
    {
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)window.Left, (int)window.Top));
        var workingArea = screen.WorkingArea;

        // Convert WinForms pixels to WPF DIPs
        var source = PresentationSource.FromVisual(window);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        double areaLeft = workingArea.Left / dpiX;
        double areaTop = workingArea.Top / dpiY;
        double areaWidth = workingArea.Width / dpiX;
        double areaHeight = workingArea.Height / dpiY;

        if (window.Left < areaLeft || window.Left + window.ActualWidth > areaLeft + areaWidth)
        {
            window.Left = areaLeft + (areaWidth - window.ActualWidth) / 2.0;
        }

        if (window.Top < areaTop || window.Top + window.ActualHeight > areaTop + areaHeight)
        {
            window.Top = areaTop;
        }
    }

    public static void DockToTopBezel(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var screen = hwnd != IntPtr.Zero
            ? System.Windows.Forms.Screen.FromHandle(hwnd)
            : System.Windows.Forms.Screen.PrimaryScreen;
        if (screen == null) return;

        var bounds = screen.Bounds;

        var source = PresentationSource.FromVisual(window);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        double screenLeft = bounds.Left / dpiX;
        double screenTop = bounds.Top / dpiY;
        double screenWidth = bounds.Width / dpiX;

        if (window.Left < screenLeft || window.Left + window.ActualWidth > screenLeft + screenWidth)
        {
            window.Left = CalculateLeft(screenLeft, screenWidth, window.ActualWidth, HorizontalOffsetPercent);
        }

        window.Top = screenTop;
    }

    /// <summary>Ukuran area kerja (di luar taskbar) layar tempat jendela berada, dalam satuan WPF.</summary>
    public static Size GetWorkAreaSize(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var screen = hwnd != IntPtr.Zero
            ? System.Windows.Forms.Screen.FromHandle(hwnd)
            : System.Windows.Forms.Screen.PrimaryScreen;
        if (screen == null) return new Size(1280, 720);

        var source = PresentationSource.FromVisual(window);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
        return new Size(screen.WorkingArea.Width / dpiX, screen.WorkingArea.Height / dpiY);
    }

    public static void CenterAtTopBezel(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var screen = hwnd != IntPtr.Zero
            ? System.Windows.Forms.Screen.FromHandle(hwnd)
            : System.Windows.Forms.Screen.PrimaryScreen;
        if (screen == null) return;

        var bounds = screen.Bounds;

        var source = PresentationSource.FromVisual(window);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        double screenLeft = bounds.Left / dpiX;
        double screenTop = bounds.Top / dpiY;
        double screenWidth = bounds.Width / dpiX;
        double width = window.ActualWidth > 0 ? window.ActualWidth : (double.IsNaN(window.Width) ? 0 : window.Width);

        window.Left = CalculateLeft(screenLeft, screenWidth, width, HorizontalOffsetPercent);
        window.Top = screenTop;
    }

    /// <summary>Geser horizontal notch dari tengah layar, dalam persen lebar layar (negatif = kiri, positif = kanan).</summary>
    public static double HorizontalOffsetPercent { get; set; }

    public const double MaxHorizontalOffsetPercent = 50;

    /// <summary>Posisi kiri jendela: tengah layar digeser <paramref name="offsetPercent"/> persen lebar layar, tetap di dalam layar.</summary>
    public static double CalculateLeft(double screenLeft, double screenWidth, double windowWidth, double offsetPercent)
    {
        double offset = Math.Clamp(offsetPercent, -MaxHorizontalOffsetPercent, MaxHorizontalOffsetPercent);
        double left = screenLeft + (screenWidth - windowWidth) / 2.0 + screenWidth * offset / 100.0;
        double maxLeft = screenLeft + Math.Max(0, screenWidth - windowWidth);
        return Math.Clamp(left, screenLeft, maxLeft);
    }
}
