using System.Windows;
using UserControl = System.Windows.Controls.UserControl;

namespace TamaNotch.Views;

/// <summary>Panel monitor sistem (grafik CPU, memori, disk, jaringan, GPU) di island.</summary>
public partial class MonitorPanel : UserControl
{
    public event EventHandler? CloseRequested;

    public MonitorPanel()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}
