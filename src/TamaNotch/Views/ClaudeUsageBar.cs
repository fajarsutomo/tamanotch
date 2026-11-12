using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using System.Windows;
using System.Windows.Media;

namespace TamaNotch.Views;

/// <summary>
/// Bar kuota: jalur gelap, isian sebesar persen terpakai, dan penanda putih tipis yang menunjukkan
/// seberapa jauh periode sudah berjalan (bila isian melewati penanda, pemakaian lebih cepat dari waktu).
/// </summary>
public sealed class ClaudeUsageBar : FrameworkElement
{
    public static readonly DependencyProperty PercentProperty = DependencyProperty.Register(
        nameof(Percent), typeof(double), typeof(ClaudeUsageBar),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Bagian periode yang sudah lewat (0..1); null menyembunyikan penanda.</summary>
    public static readonly DependencyProperty ElapsedProperty = DependencyProperty.Register(
        nameof(Elapsed), typeof(double?), typeof(ClaudeUsageBar),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(ClaudeUsageBar),
        new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush Track = Freeze(new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x36)));
    private static readonly Brush Marker = Freeze(new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF8)));

    public ClaudeUsageBar()
    {
        Height = 8;
        SnapsToDevicePixels = true;
    }

    public double Percent
    {
        get => (double)GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    public double? Elapsed
    {
        get => (double?)GetValue(ElapsedProperty);
        set => SetValue(ElapsedProperty, value);
    }

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double radius = h / 2;
        dc.DrawRoundedRectangle(Track, null, new Rect(0, 0, w, h), radius, radius);

        double fillWidth = Math.Clamp(Percent, 0, 100) / 100 * w;
        if (fillWidth > 0.5)
        {
            // Isian minimal selebar tinggi bar supaya ujung membulatnya tetap terlihat rapi.
            dc.DrawRoundedRectangle(Fill, null, new Rect(0, 0, Math.Max(fillWidth, h), h), radius, radius);
        }

        if (Elapsed is { } elapsed)
        {
            double x = Math.Clamp(elapsed, 0, 1) * w;
            dc.DrawRectangle(Marker, null, new Rect(Math.Clamp(x - 1, 0, w - 2), -2, 2, h + 4));
        }
    }

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
