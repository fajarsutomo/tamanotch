using System;
using System.Collections.Generic;
using TamaNotch.Models;
using TamaNotch.Services.Quran;
using Xunit;

namespace TamaNotch.Tests;

/// <summary>Pemutaran yang dijeda Auto-Pause saat adzan dilanjutkan setelah adzan selesai.</summary>
public class AdhanResumeTests
{
    private sealed class FakeQuran : IQuranService
    {
        private readonly List<QuranSurah> _surahs = new() { new QuranSurah { Number = 1, NameSimple = "Al-Fatihah" } };
        public IReadOnlyList<QuranSurah> Surahs => _surahs;
        public QuranSurah? CurrentSurah { get; private set; }
        public QuranPlaybackState PlaybackState { get; private set; } = QuranPlaybackState.Stopped;
        public string StatusMessage => string.Empty;
        public TimeSpan Position => TimeSpan.Zero;
        public TimeSpan Duration => TimeSpan.Zero;
        public double Volume { get; set; } = 1;
        public bool IsMuted { get; set; }
        public QuranAfterPlayback AfterPlayback { get; set; }
        public int PlaySurahCalls { get; private set; }
        public event Action? StateChanged;
        public event Action? ProgressChanged { add { } remove { } }

        public void PlaySurah(QuranSurah surah) { PlaySurahCalls++; CurrentSurah = surah; Set(QuranPlaybackState.Playing); }
        public void TogglePlayPause() => Set(PlaybackState == QuranPlaybackState.Playing ? QuranPlaybackState.Paused : QuranPlaybackState.Playing);
        public void Stop() => Set(QuranPlaybackState.Stopped);
        public void Next() { }
        public void Previous() { }
        public void PlayRandom() { }
        public void SeekToFraction(double fraction) { }
        public void Dispose() { }
        private void Set(QuranPlaybackState s) { PlaybackState = s; StateChanged?.Invoke(); }
    }

    private static PrayerNotificationEvent Adhan(bool autoPause = true) => new()
    {
        PrayerName = "Dzuhur",
        PrayerTimeFormatted = "11:58",
        Type = PrayerReminderType.AtPrayerTime,
        Title = "🕌 Waktu Sholat Dzuhur Telah Tiba!",
        Message = "Pukul 11:58.",
        AutoPauseRequested = autoPause
    };

    [Fact]
    public void Quran_IsPausedByAdhan_AndResumedFromSamePositionAfterwards()
    {
        var quran = new FakeQuran();
        var vm = ViewModelTestFactory.CreateMain(quran: quran);
        quran.PlaySurah(quran.Surahs[0]);

        vm.ShowPrayerNotification(Adhan());
        Assert.Equal(QuranPlaybackState.Paused, quran.PlaybackState);

        vm.ResumePlaybackAfterAdhan();
        Assert.Equal(QuranPlaybackState.Playing, quran.PlaybackState);
        Assert.Equal(1, quran.PlaySurahCalls); // dilanjutkan, bukan diputar ulang dari awal
    }

    [Fact]
    public void AutoResumeOff_LeavesQuranPaused()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        settings.CurrentSettings.PrayerAutoResumeAfterAdhan = false;
        var quran = new FakeQuran();
        var vm = ViewModelTestFactory.CreateMain(settings, quran: quran);
        quran.PlaySurah(quran.Surahs[0]);

        vm.ShowPrayerNotification(Adhan());
        vm.ResumePlaybackAfterAdhan();

        Assert.Equal(QuranPlaybackState.Paused, quran.PlaybackState);
    }

    [Fact]
    public void Resume_IsSkipped_WhenUserStartedPlaybackDuringAdhan()
    {
        var quran = new FakeQuran();
        var vm = ViewModelTestFactory.CreateMain(quran: quran);
        quran.PlaySurah(quran.Surahs[0]);
        vm.ShowPrayerNotification(Adhan());

        quran.TogglePlayPause(); // pengguna melanjutkan sendiri selama adzan
        vm.ResumePlaybackAfterAdhan();

        Assert.Equal(QuranPlaybackState.Playing, quran.PlaybackState);
    }

    [Fact]
    public void ResumeOnlyHappensOnce()
    {
        var quran = new FakeQuran();
        var vm = ViewModelTestFactory.CreateMain(quran: quran);
        quran.PlaySurah(quran.Surahs[0]);
        vm.ShowPrayerNotification(Adhan());
        vm.ResumePlaybackAfterAdhan();

        quran.TogglePlayPause(); // pengguna menjeda lagi
        vm.ResumePlaybackAfterAdhan();

        Assert.Equal(QuranPlaybackState.Paused, quran.PlaybackState);
    }
}
