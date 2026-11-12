using System.Windows;
using UserControl = System.Windows.Controls.UserControl;
using System.Windows.Threading;
using TamaNotch.ViewModels;

namespace TamaNotch.Views;

/// <summary>Isi jendela Pemakaian Claude; dipakai jendela sendiri dan island (panel tertanam).</summary>
public partial class ClaudeUsagePanel : UserControl
{
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(30) };

    public event EventHandler? CloseRequested;

    public ClaudeUsagePanel()
    {
        InitializeComponent();

        // Hitung mundur reset dan "diperbarui X menit lalu" ikut berjalan selama panel terlihat.
        _clock.Tick += (s, e) => (DataContext as MainViewModel)?.RaiseClaudeUsageChanged();
        IsVisibleChanged += (s, e) =>
        {
            if (IsVisible)
            {
                (DataContext as MainViewModel)?.RaiseClaudeUsageChanged();
                _clock.Start();
            }
            else
            {
                _clock.Stop();
            }
        };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}