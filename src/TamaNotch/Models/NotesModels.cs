namespace TamaNotch.Models;

/// <summary>Akun yang disimpan di sticky note (mis. login SAP). Disimpan sebagai teks biasa di notes.json.</summary>
public class NoteAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

/// <summary>Catatan teks bebas di sticky note.</summary>
public class NoteText
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

/// <summary>Isi berkas notes.json.</summary>
public class NotesData
{
    public List<NoteAccount> Accounts { get; set; } = new();
    public List<NoteText> Notes { get; set; } = new();
}
