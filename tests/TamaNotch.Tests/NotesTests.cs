using System;
using System.IO;
using System.Linq;
using TamaNotch.Models;
using TamaNotch.Services.Notes;
using TamaNotch.ViewModels;
using Xunit;

namespace TamaNotch.Tests;

public class NotesTests : IDisposable
{
    private readonly ViewModelTestFactory.MemorySettings _settings = new();
    private readonly string _dir;

    public NotesTests()
    {
        _dir = _settings.GetAppDataPath();
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }

    private sealed class FixedPathSettings : TamaNotch.Services.Settings.ISettingsService
    {
        private readonly string _dir;
        public FixedPathSettings(string dir) => _dir = dir;
        public AppSettings CurrentSettings { get; } = new();
        public void Save() { }
        public void SetStartWithWindows(bool enable) { }
        public string GetAppDataPath() => _dir;
        public string GetLyricsCachePath() => _dir;
        public void ClearLyricsCache() { }
    }

    private NotesService CreateService() => new(new FixedPathSettings(_dir));

    [Fact]
    public void Load_WithoutFile_ReturnsEmptyData()
    {
        var data = CreateService().Load();

        Assert.Empty(data.Accounts);
        Assert.Empty(data.Notes);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAccountsAndNotes()
    {
        var service = CreateService();
        service.Save(new NotesData
        {
            Accounts = { new NoteAccount { Title = "SAP PRD", User = "u123", Password = "p@ss" } },
            Notes = { new NoteText { Title = "Server", Text = "baris 1\nbaris 2" } }
        });

        var loaded = CreateService().Load();

        var account = Assert.Single(loaded.Accounts);
        Assert.Equal("SAP PRD", account.Title);
        Assert.Equal("u123", account.User);
        Assert.Equal("p@ss", account.Password);
        Assert.Equal("baris 1\nbaris 2", Assert.Single(loaded.Notes).Text);
        Assert.False(File.Exists(Path.Combine(_dir, "notes.json.tmp")));
    }

    [Fact]
    public void Load_CorruptFile_ReturnsEmpty_AndKeepsACopy()
    {
        var path = Path.Combine(_dir, "notes.json");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(path, "{ bukan json");

        var data = CreateService().Load();

        Assert.Empty(data.Accounts);
        Assert.True(File.Exists(path + ".bad"));
        Assert.Equal("{ bukan json", File.ReadAllText(path + ".bad"));
    }

    private (MainViewModel Vm, NotesService Service) CreateVm()
    {
        var service = CreateService();
        var vm = ViewModelTestFactory.CreateMain(notes: service);
        return (vm, service);
    }

    [Fact]
    public void AddAccount_AppearsRevealed_AndIsSavedImmediately()
    {
        var (vm, service) = CreateVm();

        vm.AddNoteAccountCommand.Execute(null);

        var item = Assert.Single(vm.NoteAccounts);
        Assert.True(item.IsRevealed);
        Assert.True(vm.HasNoteAccounts);
        Assert.Single(service.Load().Accounts);
    }

    [Fact]
    public void EditingAnAccount_IsPersistedBySaveNotes()
    {
        var (vm, service) = CreateVm();
        vm.AddNoteAccountCommand.Execute(null);
        var item = vm.NoteAccounts[0];

        item.Title = "SAP DEV";
        item.User = "budi";
        item.Password = "rahasia";
        vm.SaveNotes();

        var saved = Assert.Single(service.Load().Accounts);
        Assert.Equal(("SAP DEV", "budi", "rahasia"), (saved.Title, saved.User, saved.Password));
    }

    [Fact]
    public void Password_IsMaskedUntilRevealed()
    {
        var (vm, _) = CreateVm();
        vm.AddNoteAccountCommand.Execute(null);
        var item = vm.NoteAccounts[0];
        item.Password = "abcd";

        vm.ToggleNoteRevealCommand.Execute(item);
        Assert.False(item.IsRevealed);
        Assert.True(item.IsMasked);
        Assert.DoesNotContain("abcd", item.PasswordMasked);
        Assert.Equal(4, item.PasswordMasked.Length);

        vm.ToggleNoteRevealCommand.Execute(item);
        Assert.True(item.IsRevealed);
        Assert.Equal("EyeOff", item.RevealIconKind);
    }

    [Fact]
    public void RemoveAccountAndNote_UpdatesCollectionsAndFile()
    {
        var (vm, service) = CreateVm();
        vm.AddNoteAccountCommand.Execute(null);
        vm.AddNoteTextCommand.Execute(null);
        Assert.Equal(MainViewModel.NotesTabTexts, vm.NotesTab);

        vm.RemoveNoteAccountCommand.Execute(vm.NoteAccounts[0]);
        vm.RemoveNoteTextCommand.Execute(vm.NoteTexts[0]);

        Assert.Empty(vm.NoteAccounts);
        Assert.Empty(vm.NoteTexts);
        Assert.False(vm.HasNoteAccounts);
        var data = service.Load();
        Assert.Empty(data.Accounts);
        Assert.Empty(data.Notes);
    }

    [Fact]
    public void ExistingFile_IsLoadedIntoTheViewModel()
    {
        CreateService().Save(new NotesData { Accounts = { new NoteAccount { Title = "A" }, new NoteAccount { Title = "B" } } });

        var (vm, _) = CreateVm();

        Assert.Equal(new[] { "A", "B" }, vm.NoteAccounts.Select(a => a.Title));
        Assert.All(vm.NoteAccounts, a => Assert.False(a.IsRevealed));
    }

    [Fact]
    public void Tabs_SwitchBetweenAccountsAndNotes()
    {
        var (vm, _) = CreateVm();
        Assert.True(vm.IsNotesAccountsTab);

        vm.SelectNotesTabCommand.Execute("Catatan");

        Assert.True(vm.IsNotesTextsTab);
        Assert.False(vm.IsNotesAccountsTab);
    }

    [Fact]
    public void NotesPanel_OpensOnlyWhileIslandOpen_AndClosesWhenFeatureDisabled()
    {
        var (vm, _) = CreateVm();
        vm.OpenNotesCommand.Execute(null);
        Assert.Null(vm.EmbeddedPanel); // island tertutup

        vm.OnMouseEnter();
        Assert.True(vm.ShowNotesBadge);
        vm.OpenNotesCommand.Execute(null);
        Assert.Equal(MainViewModel.PanelNotes, vm.EmbeddedPanel);

        vm.EnableNotes = false;

        Assert.Null(vm.EmbeddedPanel);
        Assert.False(vm.ShowNotesBadge);
    }

    [Fact]
    public void SettingsViewModel_EnableNotes_PersistsSetting()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        var vm = ViewModelTestFactory.CreateSettings(settings);
        Assert.True(vm.EnableNotes);

        vm.EnableNotes = false;

        Assert.False(settings.CurrentSettings.EnableNotes);
        Assert.EndsWith("notes.json", vm.NotesFilePath);
    }
}
