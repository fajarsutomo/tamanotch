using System.Windows;
using UserControl = System.Windows.Controls.UserControl;
using System.Windows.Input;
using TamaNotch.ViewModels;

namespace TamaNotch.Views;

/// <summary>Isi jendela Quran; dipakai jendela sendiri dan island (panel tertanam).</summary>
public partial class QuranPanel : UserControl
{
    public event EventHandler? CloseRequested;

    public QuranPanel()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void ProgressBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (QuranProgressBar.ActualWidth <= 0 || DataContext is not MainViewModel vm) return;
        vm.SeekQuran(e.GetPosition(QuranProgressBar).X / QuranProgressBar.ActualWidth);
        e.Handled = true;
    }
}