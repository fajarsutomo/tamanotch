using System.Windows;
using UserControl = System.Windows.Controls.UserControl;
using System.Windows.Input;
using TamaNotch.ViewModels;

namespace TamaNotch.Views;

/// <summary>Isi jendela Prayer; dipakai jendela sendiri dan island (panel tertanam).</summary>
public partial class PrayerPanel : UserControl
{
    public event EventHandler? CloseRequested;

    public PrayerPanel()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

}