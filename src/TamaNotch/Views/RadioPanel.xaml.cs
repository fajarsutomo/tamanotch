using System.Windows;
using UserControl = System.Windows.Controls.UserControl;
using System.Windows.Input;
using TamaNotch.ViewModels;

namespace TamaNotch.Views;

/// <summary>Isi jendela Radio; dipakai jendela sendiri dan island (panel tertanam).</summary>
public partial class RadioPanel : UserControl
{
    public event EventHandler? CloseRequested;

    public RadioPanel()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

}