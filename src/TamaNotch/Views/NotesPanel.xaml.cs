using System.Windows;
using TamaNotch.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace TamaNotch.Views;

/// <summary>Panel sticky note (akun + catatan) di island.</summary>
public partial class NotesPanel : UserControl
{
    public event EventHandler? CloseRequested;

    public NotesPanel()
    {
        InitializeComponent();

        // Simpan saat kotak teks mana pun kehilangan fokus atau panel ditutup, agar ketikan tidak hilang.
        AddHandler(LostFocusEvent, new RoutedEventHandler((s, e) => (DataContext as MainViewModel)?.SaveNotes()), handledEventsToo: true);
        Unloaded += (s, e) => (DataContext as MainViewModel)?.SaveNotes();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        (DataContext as MainViewModel)?.SaveNotes();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
