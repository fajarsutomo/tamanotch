using TamaNotch.Models;

namespace TamaNotch.ViewModels;

/// <summary>Baris akun di sticky note; membungkus <see cref="NoteAccount"/> agar bisa diikat dan disamarkan.</summary>
public sealed class NoteAccountItem : ViewModelBase
{
    public NoteAccountItem(NoteAccount model, bool revealed = false)
    {
        Model = model;
        _isRevealed = revealed;
    }

    public NoteAccount Model { get; }

    public string Title
    {
        get => Model.Title;
        set { if (Model.Title != value) { Model.Title = value; OnPropertyChanged(); } }
    }

    public string User
    {
        get => Model.User;
        set { if (Model.User != value) { Model.User = value; OnPropertyChanged(); } }
    }

    public string Password
    {
        get => Model.Password;
        set { if (Model.Password != value) { Model.Password = value; OnPropertyChanged(); OnPropertyChanged(nameof(PasswordMasked)); } }
    }

    public string Url
    {
        get => Model.Url;
        set { if (Model.Url != value) { Model.Url = value; OnPropertyChanged(); } }
    }

    public string Note
    {
        get => Model.Note;
        set { if (Model.Note != value) { Model.Note = value; OnPropertyChanged(); } }
    }

    private bool _isRevealed;

    /// <summary>Password terlihat (kotak teks) atau disamarkan (titik).</summary>
    public bool IsRevealed
    {
        get => _isRevealed;
        set
        {
            if (SetProperty(ref _isRevealed, value))
            {
                OnPropertyChanged(nameof(IsMasked));
                OnPropertyChanged(nameof(RevealIconKind));
            }
        }
    }

    public bool IsMasked => !_isRevealed;

    public string RevealIconKind => _isRevealed ? "EyeOff" : "Eye";

    public string PasswordMasked => string.IsNullOrEmpty(Model.Password) ? "(kosong)" : new string('•', Math.Min(Model.Password.Length, 12));
}

/// <summary>Baris catatan teks bebas di sticky note.</summary>
public sealed class NoteTextItem : ViewModelBase
{
    public NoteTextItem(NoteText model) => Model = model;

    public NoteText Model { get; }

    public string Title
    {
        get => Model.Title;
        set { if (Model.Title != value) { Model.Title = value; OnPropertyChanged(); } }
    }

    public string Text
    {
        get => Model.Text;
        set { if (Model.Text != value) { Model.Text = value; OnPropertyChanged(); } }
    }
}
