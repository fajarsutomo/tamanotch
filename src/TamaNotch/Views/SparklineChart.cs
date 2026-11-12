using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace TamaNotch.Views;

/// <summary>
/// Grafik garis kecil ala tab Performance Task Manager: area berisi di bawah garis, grid tipis, titik terbaru di
/// sisi kanan (grafik "tumbuh" dari kanan ke kiri sampai <see cref="Capacity"/> titik).
/// </summary>
public class SparklineChart : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(SparklineChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Skala atas. 0 = otomatis dari data (minimal <see cref="AutoMinimum"/>), mis. untuk laju jaringan.</summary>
    public static readonly DependencyProperty MaxValueProperty = DependencyProperty.Register(
        nameof(MaxValue), typeof(double), typeof(SparklineChart),
        new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AutoMinimumProperty = DependencyProperty.Register(
        nameof(AutoMinimum), typeof(double), typeof(SparklineChart),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CapacityProperty = DependencyProperty.Register(
        nameof(Capacity), typeof(int), typeof(SparklineChart),
        new FrameworkPropertyMetadata(60, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(SparklineChart),
        new FrameworkPropertyMetadata(System.Windows.Media.Brushes.DeepSkyBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(SparklineChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GridBrushProperty = DependencyProperty.Register(
        nameof(GridBrush), typeof(Brush), typeof(SparklineChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public double AutoMinimum
    {
        get => (double)GetValue(AutoMinimumProperty);
        set => SetValue(AutoMinimumProperty, value);
    }

    public int Capacity
    {
        get => (int)GetValue(CapacityProperty);
        set => SetValue(CapacityProperty, value);
    }

    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public Brush? GridBrush
    {
        get => (Brush?)GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    /// <summary>Titik grafik (x, y) dalam ukuran kanvas; titik terbaru menempel di tepi kanan. Dipisah agar bisa diuji.</summary>
    public static List<Point> ComputePoints(IReadOnlyList<double>? values, double width, double height, int capacity, double max)
    {
        var points = new List<Point>();
        if (values == null || values.Count == 0 || width <= 0 || height <= 0) return points;
        capacity = Math.Max(2, capacity);
        if (max <= 0) max = 1;

        var step = width / (capacity - 1);
        var count = Math.Min(values.Count, capacity);
        var offset = values.Count - count;
        for (var i = 0; i < count; i++)
        {
            var value = Math.Clamp(values[offset + i], 0, max);
            var x = width - (count - 1 - i) * step;
            var y = height - value / max * height;
            points.Add(new Point(x, y));
        }

        return points;
    }

    /// <summary>Skala atas yang dipakai: <paramref name="maxValue"/> bila &gt; 0, selain itu data tertinggi (minimal <paramref name="autoMinimum"/>).</summary>
    public static double ResolveMax(IReadOnlyList<double>? values, double maxValue, double autoMinimum)
    {
        if (maxValue > 0) return maxValue;
        var peak = values is { Count: > 0 } ? values.Max() : 0;
        return Math.Max(peak * 1.15, autoMinimum <= 0 ? 1 : autoMinimum);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 120 : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 40 : availableSize.Height;
        return new Size(Width > 0 ? Width : width, Height > 0 ? Height : height);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 1 || h <= 1) return;

        // Grid tipis horizontal (25/50/75%) plus garis dasar, seperti skala persen di Task Manager
        if (GridBrush is { } gridBrush)
        {
            var gridPen = new Pen(gridBrush, 1);
            gridPen.Freeze();
            for (var i = 1; i <= 4; i++)
            {
                var y = Math.Round(h * i / 4) - 0.5;
                dc.DrawLine(gridPen, new Point(0, y), new Point(w, y));
            }
        }

        var values = Values;
        var max = ResolveMax(values, MaxValue, AutoMinimum);
        var points = ComputePoints(values, w, h - 1, Capacity, max);
        if (points.Count == 0) return;

        if (points.Count == 1)
        {
            // satu titik: garis datar pendek di tepi kanan
            points.Insert(0, new Point(Math.Max(0, points[0].X - 4), points[0].Y));
        }

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(points[0], false, false);
            ctx.PolyLineTo(points.Skip(1).ToList(), true, true);
        }
        line.Freeze();

        if (Fill is { } fill)
        {
            var area = new StreamGeometry();
            using (var ctx = area.Open())
            {
                ctx.BeginFigure(new Point(points[0].X, h), true, true);
                ctx.LineTo(points[0], false, false);
                ctx.PolyLineTo(points.Skip(1).ToList(), false, false);
                ctx.LineTo(new Point(points[^1].X, h), false, false);
            }
            area.Freeze();
            dc.DrawGeometry(fill, null, area);
        }

        if (Stroke is { } stroke)
        {
            var pen = new Pen(stroke, 1.4) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze();
            dc.DrawGeometry(null, pen, line);
        }
    }
}
