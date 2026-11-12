using TamaNotch.ViewModels;
using Xunit;

namespace TamaNotch.Tests;

public class EmbeddedPanelTests
{
    [Fact]
    public void OpenEmbeddedPanel_IsRefused_WhileTheIslandIsClosed()
    {
        var vm = ViewModelTestFactory.CreateMain();

        Assert.False(vm.OpenEmbeddedPanel(MainViewModel.PanelRadio));

        Assert.Null(vm.EmbeddedPanel);
        Assert.False(vm.ShowEmbeddedPanel);
    }

    [Fact]
    public void OpenEmbeddedPanel_ShowsThePanel_WhileTheIslandIsHovered()
    {
        var vm = ViewModelTestFactory.CreateMain();
        vm.OnMouseEnter();

        Assert.True(vm.OpenEmbeddedPanel(MainViewModel.PanelQuran));

        Assert.Equal(MainViewModel.PanelQuran, vm.EmbeddedPanel);
        Assert.True(vm.ShowEmbeddedPanel);
    }

    [Fact]
    public void OnlyOnePanelIsEmbeddedAtATime()
    {
        var vm = ViewModelTestFactory.CreateMain();
        vm.OnMouseEnter();
        vm.OpenEmbeddedPanel(MainViewModel.PanelRadio);

        vm.OpenEmbeddedPanel(MainViewModel.PanelPrayer);

        Assert.Equal(MainViewModel.PanelPrayer, vm.EmbeddedPanel);
    }

    [Theory]
    [InlineData(MainViewModel.PanelNotes, true)]
    [InlineData(MainViewModel.PanelMonitor, true)]
    [InlineData(MainViewModel.PanelRadio, false)]
    [InlineData(MainViewModel.PanelQuran, false)]
    public void NotesAndMonitor_UseTheFullIslandWidth(string panel, bool fullWidth)
    {
        var vm = ViewModelTestFactory.CreateMain();
        vm.OnMouseEnter();

        vm.OpenEmbeddedPanel(panel);

        Assert.Equal(fullWidth, vm.IsEmbeddedPanelFullWidth);
    }

    [Fact]
    public void CollapsingTheIsland_ClosesTheEmbeddedPanel()
    {
        var vm = ViewModelTestFactory.CreateMain();
        vm.OnMouseEnter();
        vm.OpenEmbeddedPanel(MainViewModel.PanelRadio);
        var changed = new List<string?>();
        vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

        vm.OnMouseLeave();
        vm.CollapseHoverIfIdle();

        Assert.Null(vm.EmbeddedPanel);
        Assert.False(vm.ShowEmbeddedPanel);
        Assert.Contains(nameof(MainViewModel.EmbeddedPanel), changed);
    }

    [Fact]
    public void ClosePanel_ClearsIt_AndIslandStaysOpen()
    {
        var vm = ViewModelTestFactory.CreateMain();
        vm.OnMouseEnter();
        vm.OpenEmbeddedPanel(MainViewModel.PanelClaude);

        vm.CloseEmbeddedPanel();

        Assert.Null(vm.EmbeddedPanel);
        Assert.True(vm.IsHovering);
    }

    [Theory]
    [InlineData("Island", true)]
    [InlineData("island", true)]
    [InlineData("Window", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsIslandMode_OnlyAcceptsIsland(string? mode, bool expected)
        => Assert.Equal(expected, MainViewModel.IsIslandMode(mode));

    [Fact]
    public void SettingsViewModel_DisplayModes_DefaultToWindow_AndPersistIsland()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        var vm = ViewModelTestFactory.CreateSettings(settings);
        Assert.Equal("Window", vm.RadioDisplayMode);

        vm.RadioDisplayMode = "Island";
        vm.QuranDisplayMode = "garbage";

        Assert.Equal("Island", settings.CurrentSettings.RadioDisplayMode);
        Assert.Equal("Window", vm.QuranDisplayMode);
        Assert.True(settings.SaveCount >= 1);
    }
}
