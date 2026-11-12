using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using TamaNotch.Helpers;
using TamaNotch.Models;
using Xunit;

namespace TamaNotch.Tests;

public class NotchStyleTests
{
    // --- NeonPalette ---

    [Theory]
    [InlineData("#35D6FF", 0x35, 0xD6, 0xFF)]
    [InlineData("35d6ff", 0x35, 0xD6, 0xFF)]
    [InlineData("  #000000 ", 0, 0, 0)]
    public void Parse_ValidHex_ReturnsColor(string hex, byte r, byte g, byte b)
    {
        Assert.True(NeonPalette.TryParse(hex, out var color));
        Assert.Equal(Color.FromRgb(r, g, b), color);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData("default")]
    public void Parse_InvalidHex_UsesFallback(string? hex)
    {
        Assert.False(NeonPalette.TryParse(hex, out _));
        Assert.Equal(Color.FromRgb(0x4F, 0xC3, 0xFF), NeonPalette.Parse(hex, NeonPalette.DefaultEyeHex));
    }

    [Fact]
    public void Lighten_BlendsTowardsWhite()
    {
        Assert.Equal(Color.FromRgb(0, 0, 0), NeonPalette.Lighten(Color.FromRgb(0, 0, 0), 0));
        Assert.Equal(Color.FromRgb(255, 255, 255), NeonPalette.Lighten(Color.FromRgb(0, 0, 0), 1));
        Assert.Equal(Color.FromRgb(128, 128, 128), NeonPalette.Lighten(Color.FromRgb(0, 0, 0), 0.5));
    }

    [Fact]
    public void Presets_AreValidUniqueNeonColors_AndHoverChoicesStartWithDefault()
    {
        Assert.True(NeonPalette.Presets.Count >= 8);
        Assert.All(NeonPalette.Presets, p => Assert.True(NeonPalette.TryParse(p.Hex, out _)));
        Assert.Equal(NeonPalette.Presets.Count, NeonPalette.Presets.Select(p => p.Hex.ToUpperInvariant()).Distinct().Count());

        Assert.True(NeonPalette.HoverChoices[0].IsDefault);
        Assert.Equal(NeonPalette.Presets.Count + 1, NeonPalette.HoverChoices.Count);
    }

    // --- MainViewModel: idle ---

    [Fact]
    public void Idle_DefaultSettings_AreAutoSizedWithNeonFrame()
    {
        var vm = ViewModelTestFactory.CreateMain();

        Assert.True(double.IsNaN(vm.IdleFrameWidth));
        Assert.True(double.IsNaN(vm.IdleFrameHeight));
        Assert.Equal(Color.FromRgb(0x4F, 0xC3, 0xFF), vm.IdleEyeColor);
        Assert.Equal(new CornerRadius(0, 0, 24, 24), vm.IdleFrameCornerRadius);

        var glow = Assert.IsType<DropShadowEffect>(vm.IdleGlowEffect);
        Assert.Equal(Color.FromRgb(0x35, 0xD6, 0xFF), glow.Color);
        Assert.True(glow.IsFrozen);
    }

    [Fact]
    public void Idle_ManualSizeAndRadii_AreClampedAndMappedToBottomCorners()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        var s = settings.CurrentSettings;
        s.IdleFrameAutoSize = false;
        s.IdleFrameWidth = 500;       // di atas batas
        s.IdleFrameHeight = 5;        // di bawah batas
        s.IdleFrameRadiusLeft = 10;
        s.IdleFrameRadiusRight = 99;  // di atas batas
        var vm = ViewModelTestFactory.CreateMain(settings);

        Assert.Equal(240, vm.IdleFrameWidth);
        Assert.Equal(20, vm.IdleFrameHeight);
        // Urutan CornerRadius: kiri-atas, kanan-atas, kanan-bawah, kiri-bawah
        Assert.Equal(new CornerRadius(0, 0, 60, 10), vm.IdleFrameCornerRadius);
    }

    [Fact]
    public void Idle_InvalidColors_FallBackToDefaults()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        settings.CurrentSettings.IdleEyeColor = "bukan-warna";
        settings.CurrentSettings.IdleFrameColor = "#12";
        var vm = ViewModelTestFactory.CreateMain(settings);

        Assert.Equal(Color.FromRgb(0x4F, 0xC3, 0xFF), vm.IdleEyeColor);
        Assert.Equal(Color.FromRgb(0x35, 0xD6, 0xFF), ((DropShadowEffect)vm.IdleGlowEffect).Color);
    }

    // --- MainViewModel: hover ---

    [Fact]
    public void Hover_Default_HasNoCustomBrushOrGlow()
    {
        var vm = ViewModelTestFactory.CreateMain();

        Assert.Null(vm.HoverFrameBrush);
        Assert.Null(vm.HoverGlowEffect);
        Assert.Null(vm.HoverRingBrush);
    }

    [Fact]
    public void Hover_CustomColors_ProduceBrushesAndGlow()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        settings.CurrentSettings.HoverFrameColor = "#B44CFF";
        settings.CurrentSettings.HoverRingColor = "#39FF14";
        var vm = ViewModelTestFactory.CreateMain(settings);

        Assert.NotNull(vm.HoverFrameBrush);
        Assert.Equal(Color.FromRgb(0xB4, 0x4C, 0xFF), ((DropShadowEffect)vm.HoverGlowEffect!).Color);
        Assert.Equal(Color.FromRgb(0x39, 0xFF, 0x14), ((SolidColorBrush)vm.HoverRingBrush!).Color);
    }

    [Fact]
    public void Hover_SameAsIdle_UsesIdleFrameColorForFrameAndRing()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        settings.CurrentSettings.IdleFrameColor = "#FF3DD0";
        settings.CurrentSettings.HoverFrameColor = "#39FF14";   // diabaikan saat "samakan"
        settings.CurrentSettings.HoverRingColor = "default";
        settings.CurrentSettings.HoverSameAsIdle = true;
        var vm = ViewModelTestFactory.CreateMain(settings);

        Assert.Equal(Color.FromRgb(0xFF, 0x3D, 0xD0), ((DropShadowEffect)vm.HoverGlowEffect!).Color);
        Assert.Equal(Color.FromRgb(0xFF, 0x3D, 0xD0), ((SolidColorBrush)vm.HoverRingBrush!).Color);
    }

    [Fact]
    public void ChangingSettings_RaisesAppearanceNotificationsOnRefresh()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        var vm = ViewModelTestFactory.CreateMain(settings);
        var raised = new List<string?>();
        vm.PropertyChanged += (s, e) => raised.Add(e.PropertyName);

        settings.CurrentSettings.IdleFrameColor = "#FF4D8D";
        vm.RefreshComponentLayout();

        Assert.Contains(nameof(vm.IdleFrameBrush), raised);
        Assert.Contains(nameof(vm.IdleGlowEffect), raised);
        Assert.Contains(nameof(vm.HoverRingBrush), raised);
        Assert.Contains(nameof(vm.IdleFrameCornerRadius), raised);
    }

    // --- SettingsViewModel ---

    [Fact]
    public void SettingsViewModel_ColorSelection_PersistsAndRaises()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        var vm = ViewModelTestFactory.CreateSettings(settings);
        var raised = new List<string?>();
        vm.PropertyChanged += (s, e) => raised.Add(e.PropertyName);

        vm.IdleEyeColor = "#B44CFF";
        vm.HoverFrameColor = "#FF8A1F";

        Assert.Equal("#B44CFF", settings.CurrentSettings.IdleEyeColor);
        Assert.Equal("#FF8A1F", settings.CurrentSettings.HoverFrameColor);
        Assert.True(settings.SaveCount >= 2);
        Assert.Contains(nameof(vm.IdleEyeColor), raised);
        Assert.Contains(nameof(vm.HoverFrameColor), raised);
    }

    [Fact]
    public void SettingsViewModel_FrameSizeAndRadii_AreRoundedAndClamped()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        var vm = ViewModelTestFactory.CreateSettings(settings);

        vm.IdleFrameWidth = 500.4;
        vm.IdleFrameHeight = 2;
        vm.IdleFrameRadiusLeft = 12.6;
        vm.IdleFrameRadiusRight = -5;

        Assert.Equal(240, settings.CurrentSettings.IdleFrameWidth);
        Assert.Equal(20, settings.CurrentSettings.IdleFrameHeight);
        Assert.Equal(13, settings.CurrentSettings.IdleFrameRadiusLeft);
        Assert.Equal(0, settings.CurrentSettings.IdleFrameRadiusRight);
        Assert.Equal("13 px", vm.IdleFrameRadiusLeftText);
    }

    [Fact]
    public void SettingsViewModel_TopRadii_AreClampedToTheWindowMargin_AndOnlyEditableInDropShape()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        var vm = ViewModelTestFactory.CreateSettings(settings);

        Assert.False(vm.IdleDropShape);
        Assert.False(vm.IsIdleTopRadiusEditable);

        vm.IdleDropShape = true;
        vm.IdleTopRadiusLeft = 40;
        vm.IdleTopRadiusRight = -3;

        Assert.Equal("Drop", settings.CurrentSettings.IdleShape);
        Assert.True(vm.IsIdleTopRadiusEditable);
        Assert.Equal(16, settings.CurrentSettings.IdleTopRadiusLeft);
        Assert.Equal(0, settings.CurrentSettings.IdleTopRadiusRight);
        Assert.Equal("16 px", vm.IdleTopRadiusLeftText);

        vm.IdleDropShape = false;
        Assert.Equal("Capsule", settings.CurrentSettings.IdleShape);
    }

    [Fact]
    public void MainViewModel_DropShape_OnlyInCompactMode_AndTopRadiiAreClamped()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        settings.CurrentSettings.IdleShape = "Drop";
        settings.CurrentSettings.IdleTopRadiusLeft = 99;
        settings.CurrentSettings.IdleTopRadiusRight = 7;
        var vm = ViewModelTestFactory.CreateMain(settings: settings);

        Assert.True(vm.ShowDropShape);
        Assert.Equal(16, vm.DropTopRadiusLeft);
        Assert.Equal(7, vm.DropTopRadiusRight);

        vm.SetModeCommand.Execute("Expanded");
        Assert.False(vm.ShowDropShape);
    }

    [Fact]
    public void DropShape_FollowsTheFrameOfWhicheverNotchIsShowing()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        settings.CurrentSettings.IdleShape = "Drop";
        var vm = ViewModelTestFactory.CreateMain(settings: settings);

        // IdleFrameBrush dibuat baru tiap dibaca, jadi bandingkan warnanya.
        Assert.Equal(((SolidColorBrush)vm.IdleFrameBrush).Color, ((SolidColorBrush)vm.DropFrameBrush!).Color);

        vm.ShowWellnessNotification(new WellnessNotificationEvent { Kind = WellnessReminderKind.Drink, Title = "t", Message = "m", PrimaryActionText = "ok" });
        Assert.True(vm.ShowDropShape);
        Assert.Same(vm.WellnessFrameBrush, vm.DropFrameBrush);
        Assert.Same(vm.WellnessGlowEffect, vm.DropGlowEffect);

        vm.ShowPrayerNotification(new PrayerNotificationEvent { PrayerName = "Dzuhur", Type = PrayerReminderType.AtPrayerTime, Title = "t", Message = "m" });
        Assert.Same(vm.PrayerFrameBrush, vm.DropFrameBrush);
    }

    [Fact]
    public void SettingsViewModel_Toggles_UpdateEditableFlags()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        var vm = ViewModelTestFactory.CreateSettings(settings);

        Assert.False(vm.IsIdleFrameSizeEditable);   // otomatis aktif secara default
        vm.IdleFrameAutoSize = false;
        Assert.True(vm.IsIdleFrameSizeEditable);

        Assert.True(vm.IsHoverColorEditable);
        vm.HoverSameAsIdle = true;
        Assert.False(vm.IsHoverColorEditable);
    }
}
