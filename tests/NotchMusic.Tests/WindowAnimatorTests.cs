using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NotchMusic.Helpers;
using Xunit;

namespace NotchMusic.Tests;

public class WindowAnimatorTests
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
        if (failure != null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    /// <summary>Jalankan dispatcher thread ini selama <paramref name="ms"/> milidetik (animasi dan callback tertunda ikut jalan).</summary>
    private static void Pump(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (s, e) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static (Window Window, Grid Root) CreateWindow()
    {
        var root = new Grid { Width = 200, Height = 100, Background = System.Windows.Media.Brushes.Black };
        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            SizeToContent = SizeToContent.WidthAndHeight,
            ShowInTaskbar = false,
            Left = 300,
            Top = 300,
            Content = root
        };
        return (window, root);
    }

    private static bool IsClean(Grid root)
        => root.Opacity == 1.0 && (root.RenderTransform == null || root.RenderTransform.Value.IsIdentity);

    [Fact]
    public void ShowFromNotch_EndsFullyVisible_WithoutLeftoverTransform()
    {
        RunSta(() =>
        {
            WindowAnimator.NotchLocator = _ => new Point(600, 10);
            var (window, root) = CreateWindow();
            try
            {
                window.ShowFromNotch();
                Assert.True(window.IsVisible);

                Pump(700);

                Assert.True(IsClean(root), $"opacity={root.Opacity} transform={root.RenderTransform?.Value}");
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void HideToNotch_HidesTheWindow_AndRestoresItsContentForNextTime()
    {
        RunSta(() =>
        {
            WindowAnimator.NotchLocator = _ => new Point(600, 10);
            var (window, root) = CreateWindow();
            try
            {
                window.Show();
                Pump(100);

                window.HideToNotch();
                Assert.True(window.IsVisible);   // masih tampil selama animasi

                Pump(600);

                Assert.False(window.IsVisible);
                Assert.True(IsClean(root));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ShowFromNotch_WhileClosing_CancelsTheClose()
    {
        RunSta(() =>
        {
            WindowAnimator.NotchLocator = _ => new Point(600, 10);
            var (window, root) = CreateWindow();
            try
            {
                window.Show();
                Pump(100);

                window.HideToNotch();
                Pump(60);
                window.ShowFromNotch();
                Pump(600);

                Assert.True(window.IsVisible);
                Assert.True(IsClean(root));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void WithoutANotch_ShowFromNotchJustShows()
    {
        RunSta(() =>
        {
            WindowAnimator.NotchLocator = _ => null;
            var (window, root) = CreateWindow();
            try
            {
                window.ShowFromNotch();

                Assert.True(window.IsVisible);
                Assert.True(IsClean(root));
            }
            finally { window.Close(); }
        });
    }
}
