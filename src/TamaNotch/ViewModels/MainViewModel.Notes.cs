using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using TamaNotch.Helpers;
using TamaNotch.Models;
using TamaNotch.Services.Notes;

namespace TamaNotch.ViewModels;

/// <summary>Sticky note: akun (mis. SAP) dan catatan teks, tampil sebagai panel tertanam di island.</summary>
public partial class MainViewModel
{
    public const string PanelNotes = "Notes";
    public const string NotesTabAccounts = "Akun";
    public const string NotesTabTexts = "Catatan";

    private INotesService _notesService = null!;
    private string _notesTab = NotesTabAccounts;
    private string _notesStatus = string.Empty;

    public ObservableCollection<NoteAccountItem> NoteAccounts { get; } = new();

    public ObservableCollection<NoteTextItem> NoteTexts { get; } = new();

    public ICommand OpenNotesCommand { get; private set; } = null!;
    public ICommand AddNoteAccountCommand { get; private set; } = null!;
    public ICommand RemoveNoteAccountCommand { get; private set; } = null!;
    public ICommand AddNoteTextCommand { get; private set; } = null!;
    public ICommand RemoveNoteTextCommand { get; private set; } = null!;
    public ICommand ToggleNoteRevealCommand { get; private set; } = null!;
    public ICommand CopyNoteValueCommand { get; private set; } = null!;
    public ICommand SelectNotesTabCommand { get; private set; } = null!;

    /// <summary>Muat/simpan lewat <see cref="INotesService"/>; dipanggil sekali dari konstruktor.</summary>
    private void InitializeNotes(INotesService? service)
    {
        _notesService = service ?? new NotesService(_settingsService);

        var data = _notesService.Load();
        foreach (var account in data.Accounts) NoteAccounts.Add(new NoteAccountItem(account));
        foreach (var note in data.Notes) NoteTexts.Add(new NoteTextItem(note));

        OpenNotesCommand = new RelayCommand(() => OpenEmbeddedPanel(PanelNotes));
        AddNoteAccountCommand = new RelayCommand(() =>
        {
            // Akun baru langsung terlihat agar password bisa diketik.
            NoteAccounts.Add(new NoteAccountItem(new NoteAccount { Title = "Akun baru" }, revealed: true));
            NotesTab = NotesTabAccounts;
            SaveNotes();
        });
        RemoveNoteAccountCommand = new RelayCommand(p =>
        {
            if (p is NoteAccountItem item && NoteAccounts.Remove(item)) SaveNotes();
        });
        AddNoteTextCommand = new RelayCommand(() =>
        {
            NoteTexts.Add(new NoteTextItem(new NoteText { Title = "Catatan baru" }));
            NotesTab = NotesTabTexts;
            SaveNotes();
        });
        RemoveNoteTextCommand = new RelayCommand(p =>
        {
            if (p is NoteTextItem item && NoteTexts.Remove(item)) SaveNotes();
        });
        ToggleNoteRevealCommand = new RelayCommand(p =>
        {
            if (p is NoteAccountItem item) item.IsRevealed = !item.IsRevealed;
        });
        CopyNoteValueCommand = new RelayCommand(p => CopyNoteValue(p as string));
        SelectNotesTabCommand = new RelayCommand(p => NotesTab = p as string == NotesTabTexts ? NotesTabTexts : NotesTabAccounts);
    }

    public bool EnableNotes
    {
        get => Settings.EnableNotes;
        set
        {
            if (Settings.EnableNotes == value) return;
            Settings.EnableNotes = value;
            _settingsService.Save();
            if (!value && EmbeddedPanel == PanelNotes) CloseEmbeddedPanel();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowNotesBadge));
        }
    }

    /// <summary>Ikon sticky note tampil di baris tombol island saat island terbuka dan fitur aktif.</summary>
    public bool ShowNotesBadge => ShowComponentIcons && Settings.EnableNotes;

    public string NotesTab
    {
        get => _notesTab;
        set
        {
            if (SetProperty(ref _notesTab, value))
            {
                OnPropertyChanged(nameof(IsNotesAccountsTab));
                OnPropertyChanged(nameof(IsNotesTextsTab));
            }
        }
    }

    public bool IsNotesAccountsTab => _notesTab == NotesTabAccounts;

    public bool IsNotesTextsTab => _notesTab == NotesTabTexts;

    public string NotesStatus
    {
        get => _notesStatus;
        private set => SetProperty(ref _notesStatus, value);
    }

    /// <summary>Dipanggil setelah Pengaturan ditutup: ikon dan panel mengikuti EnableNotes.</summary>
    public void SyncNotesWithSettings()
    {
        if (!Settings.EnableNotes && EmbeddedPanel == PanelNotes) CloseEmbeddedPanel();
        OnPropertyChanged(nameof(EnableNotes));
        OnPropertyChanged(nameof(ShowNotesBadge));
    }

    public bool HasNoteAccounts => NoteAccounts.Count > 0;

    public bool HasNoteTexts => NoteTexts.Count > 0;

    /// <summary>Simpan seluruh akun dan catatan; dipanggil setelah tambah, hapus, dan saat kotak teks kehilangan fokus.</summary>
    public void SaveNotes()
    {
        var data = new NotesData
        {
            Accounts = NoteAccounts.Select(a => a.Model).ToList(),
            Notes = NoteTexts.Select(n => n.Model).ToList()
        };
        try
        {
            _notesService.Save(data);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            NotesStatus = "Gagal menyimpan catatan: " + ex.Message;
        }

        OnPropertyChanged(nameof(HasNoteAccounts));
        OnPropertyChanged(nameof(HasNoteTexts));
    }

    private void CopyNoteValue(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        try
        {
            System.Windows.Clipboard.SetText(value);
            NotesStatus = "Disalin ke clipboard.";
        }
        catch (Exception)
        {
            NotesStatus = "Clipboard sedang dipakai aplikasi lain; coba lagi.";
        }
    }
}
