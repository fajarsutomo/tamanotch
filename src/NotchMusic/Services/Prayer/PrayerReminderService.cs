using System;
using System.Collections.Generic;
using System.Windows.Threading;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.Settings;

namespace NotchMusic.Services.Prayer;

public interface IPrayerReminderService : IDisposable
{
    event Action<PrayerNotificationEvent>? ReminderTriggered;
    event Action<PrayerSchedule>? ScheduleUpdated;

    PrayerSchedule CurrentSchedule { get; }
    void Start();
    void Stop();
    void RefreshSchedule();

    /// <summary>Satu putaran pemeriksaan (dipanggil timer; dibuka agar bisa dites).</summary>
    void Tick();
}

public class PrayerReminderService : IPrayerReminderService
{
    private readonly IPrayerService _prayerService;
    private readonly ISettingsService _settingsService;
    private readonly DispatcherTimer _timer;
    private readonly Func<DateTime> _now;
    private readonly HashSet<string> _firedKeys = new();
    private DateTime _lastFiredDate = DateTime.MinValue;
    private PrayerSchedule _currentSchedule;
    private bool _isDisposed;

    public event Action<PrayerNotificationEvent>? ReminderTriggered;
    public event Action<PrayerSchedule>? ScheduleUpdated;

    public PrayerSchedule CurrentSchedule => _currentSchedule;

    public PrayerReminderService(IPrayerService prayerService, ISettingsService settingsService, Func<DateTime>? now = null)
    {
        _now = now ?? (() => DateTime.Now);
        _prayerService = prayerService;
        _settingsService = settingsService;
        _currentSchedule = _prayerService.GetTodaySchedule();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _timer.Tick += (s, e) => Tick();
    }

    public void Start()
    {
        RefreshSchedule();
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    public void Stop()
    {
        if (_timer.IsEnabled)
        {
            _timer.Stop();
        }
    }

    public void RefreshSchedule()
    {
        _currentSchedule = _prayerService.GetTodaySchedule();
        ScheduleUpdated?.Invoke(_currentSchedule);
    }

    public void Tick()
    {
        var now = _now();

        // Reset fired keys if day changed
        if (now.Date != _lastFiredDate)
        {
            _firedKeys.Clear();
            _lastFiredDate = now.Date;
        }

        // Refresh schedule regularly
        _currentSchedule = _prayerService.GetTodaySchedule();
        ScheduleUpdated?.Invoke(_currentSchedule);

        var settings = _settingsService.CurrentSettings;
        if (!settings.EnablePrayerTimes)
        {
            return;
        }

        CheckReminders(now, settings);
    }

    private void CheckReminders(DateTime now, AppSettings settings)
    {
        string datePrefix = now.ToString("yyyyMMdd");
        bool fastingDay = FastingHelper.IsFastingDay(settings, now);

        if (fastingDay) CheckSahurReminder(now, settings, datePrefix);

        foreach (var item in _currentSchedule.Items)
        {
            if (!item.IsMandatoryPrayer) continue; // Only for Subuh, Dzuhur, Ashar, Maghrib, Isya

            var prayerDateTime = now.Date + item.Time;

            // 1. PENGINGAT SEBELUM SHOLAT
            if (settings.RemindBeforePrayer && settings.RemindBeforeMinutes > 0)
            {
                var beforeTarget = prayerDateTime.AddMinutes(-settings.RemindBeforeMinutes);
                string beforeKey = $"{datePrefix}_{item.Name}_Before";

                if (now >= beforeTarget && now < beforeTarget.AddMinutes(2) && !_firedKeys.Contains(beforeKey))
                {
                    _firedKeys.Add(beforeKey);

                    var evt = new PrayerNotificationEvent
                    {
                        PrayerName = item.Name,
                        PrayerTimeFormatted = item.FormattedTime,
                        Type = PrayerReminderType.Before,
                        Minutes = settings.RemindBeforeMinutes,
                        Title = $"🕌 {settings.RemindBeforeMinutes} Menit Menuju Waktu {item.Name}",
                        Message = $"Waktu sholat {item.Name} pukul {item.FormattedTime}. Mari persiapkan wudhu dan sholat berjamaah.",
                        AutoPauseRequested = false
                    };

                    if (settings.PrayerSoundAlert)
                    {
                        PrayerAudioHelper.PlayPrayerNotification(settings, evt);
                    }

                    ReminderTriggered?.Invoke(evt);
                }
            }

            // 2. PENGINGAT SAAT WAKTU SHOLAT TIBA (ADZAN)
            if (settings.RemindAtPrayerTime)
            {
                string exactKey = $"{datePrefix}_{item.Name}_Exact";

                if (now >= prayerDateTime && now < prayerDateTime.AddMinutes(2) && !_firedKeys.Contains(exactKey))
                {
                    _firedKeys.Add(exactKey);

                    var evt = new PrayerNotificationEvent
                    {
                        PrayerName = item.Name,
                        PrayerTimeFormatted = item.FormattedTime,
                        Type = PrayerReminderType.AtPrayerTime,
                        Minutes = 0,
                        Title = fastingDay && item.Name == "Maghrib"
                            ? "🌙 Waktunya Berbuka Puasa!"
                            : $"🕌 Waktu Sholat {item.Name} Telah Tiba!",
                        Message = fastingDay && item.Name == "Maghrib"
                            ? $"Maghrib pukul {item.FormattedTime}. Selamat berbuka, lalu tunaikan sholat Maghrib."
                            : $"Pukul {item.FormattedTime}. Hayya 'alash-Shalah — Mari tunaikan ibadah sholat.",
                        AutoPauseRequested = settings.PrayerAutoPauseMusic
                    };

                    if (settings.PrayerSoundAlert)
                    {
                        PrayerAudioHelper.PlayPrayerNotification(settings, evt);
                    }

                    ReminderTriggered?.Invoke(evt);
                }
            }

            // 3. PENGINGAT SESUDAH WAKTU SHOLAT
            if (settings.RemindAfterPrayer && settings.RemindAfterMinutes > 0)
            {
                var afterTarget = prayerDateTime.AddMinutes(settings.RemindAfterMinutes);
                string afterKey = $"{datePrefix}_{item.Name}_After";

                if (now >= afterTarget && now < afterTarget.AddMinutes(2) && !_firedKeys.Contains(afterKey))
                {
                    _firedKeys.Add(afterKey);

                    var evt = new PrayerNotificationEvent
                    {
                        PrayerName = item.Name,
                        PrayerTimeFormatted = item.FormattedTime,
                        Type = PrayerReminderType.After,
                        Minutes = settings.RemindAfterMinutes,
                        Title = $"⏰ Pengingat Sholat: Waktu {item.Name}",
                        Message = $"Waktu {item.Name} telah berlalu {settings.RemindAfterMinutes} menit. Sudahkah Anda menunaikan sholat {item.Name}?",
                        AutoPauseRequested = false
                    };

                    if (settings.PrayerSoundAlert)
                    {
                        PrayerAudioHelper.PlayPrayerNotification(settings, evt);
                    }

                    ReminderTriggered?.Invoke(evt);
                }
            }
        }
    }

    /// <summary>Pengingat sahur N menit sebelum Imsak pada hari puasa.</summary>
    private void CheckSahurReminder(DateTime now, AppSettings settings, string datePrefix)
    {
        var imsakItem = _currentSchedule.Items.FirstOrDefault(i => i.Name == "Imsak");
        if (imsakItem == null || settings.FastingSahurMinutes <= 0) return;

        var target = (now.Date + imsakItem.Time).AddMinutes(-settings.FastingSahurMinutes);
        string key = $"{datePrefix}_Imsak_Sahur";
        if (now < target || now >= target.AddMinutes(2) || _firedKeys.Contains(key)) return;

        _firedKeys.Add(key);
        var evt = new PrayerNotificationEvent
        {
            PrayerName = "Imsak",
            PrayerTimeFormatted = imsakItem.FormattedTime,
            Type = PrayerReminderType.Sahur,
            Minutes = settings.FastingSahurMinutes,
            Title = "🌙 Waktunya Sahur",
            Message = $"Imsak pukul {imsakItem.FormattedTime} ({settings.FastingSahurMinutes} menit lagi). Segera sahur dan niatkan puasa.",
            AutoPauseRequested = false
        };

        if (settings.PrayerSoundAlert) PrayerAudioHelper.PlayPrayerNotification(settings, evt);
        ReminderTriggered?.Invoke(evt);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _timer.Stop();
    }
}
