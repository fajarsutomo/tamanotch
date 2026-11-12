using MenuItem = System.Windows.Controls.MenuItem;
using Size = System.Windows.Size;
using System.Windows;
using System.Windows.Controls;

namespace TamaNotch.Helpers;

/// <summary>Ukuran layar HP (px CSS, potret) untuk jendela pemutar web (YouTube Music, Spotify).</summary>
public sealed record PhoneSize(string Name, double Width, double Height)
{
    public string Label => $"📱 {Name} ({Width:0} × {Height:0})";
}

public static class PhoneSizePresets
{
    public static readonly IReadOnlyList<PhoneSize> All = new[]
    {
        new PhoneSize("iPhone SE", 375, 667),
        new PhoneSize("Galaxy S / Android umum", 360, 800),
        new PhoneSize("iPhone 14 / 15", 390, 844),
        new PhoneSize("Pixel 7 / Android besar", 412, 915),
        new PhoneSize("iPhone Pro Max", 430, 932),
    };

    /// <summary>
    /// Ukuran jendela agar area web seukuran layar HP: ukuran HP ditambah bingkai jendela (judul-bar, banner),
    /// diperkecil proporsional bila melebihi area kerja layar, dan tidak di bawah ukuran minimum jendela.
    /// </summary>
    public static (double Width, double Height) ToWindowSize(
        PhoneSize phone, double chromeWidth, double chromeHeight,
        double maxWidth, double maxHeight, double minWidth, double minHeight)
    {
        double width = phone.Width + Math.Max(0, chromeWidth);
        double height = phone.Height + Math.Max(0, chromeHeight);

        double scale = Math.Min(1.0, Math.Min(maxWidth / width, maxHeight / height));
        if (scale < 1.0)
        {
            width *= scale;
            height *= scale;
        }

        return (Math.Round(Math.Max(minWidth, width)), Math.Round(Math.Max(minHeight, height)));
    }

    /// <summary>
    /// Submenu "Ukuran HP" untuk menu ukuran jendela. <paramref name="apply"/> menerima ukuran jendela akhir;
    /// <paramref name="getChrome"/> memberi selisih ukuran jendela terhadap area web; <paramref name="getWorkArea"/>
    /// memberi ukuran area kerja layar saat ini.
    /// </summary>
    public static MenuItem CreateMenu(
        System.Windows.Media.Brush foreground,
        Action<double, double> apply,
        Func<(double Width, double Height)> getChrome,
        Func<Size> getWorkArea,
        double minWidth, double minHeight,
        Action rotate)
    {
        var menu = new MenuItem
        {
            Header = "📱 Ukuran HP",
            Foreground = foreground,
            Padding = new Thickness(10, 6, 10, 6)
        };

        foreach (var phone in All)
        {
            var item = new MenuItem { Header = phone.Label, Foreground = foreground, Padding = new Thickness(8, 4, 8, 4) };
            item.Click += (s, e) =>
            {
                var chrome = getChrome();
                var area = getWorkArea();
                var (w, h) = ToWindowSize(phone, chrome.Width, chrome.Height, Math.Max(minWidth, area.Width - 20), Math.Max(minHeight, area.Height - 20), minWidth, minHeight);
                apply(w, h);
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        var rotateItem = new MenuItem { Header = "🔄 Putar (tukar lebar dan tinggi)", Foreground = foreground, Padding = new Thickness(8, 4, 8, 4) };
        rotateItem.Click += (s, e) => rotate();
        menu.Items.Add(rotateItem);
        return menu;
    }
}
