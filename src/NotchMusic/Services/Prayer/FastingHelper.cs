using NotchMusic.Models;

namespace NotchMusic.Services.Prayer;

/// <summary>Aturan hari dan jam puasa: dari Imsak sampai Maghrib pada hari yang dipilih pengguna.</summary>
public static class FastingHelper
{
    /// <summary>Puasa aktif hari ini: setiap hari (mis. Ramadan) atau hanya Senin dan Kamis.</summary>
    public static bool IsFastingDay(AppSettings settings, DateTime date)
    {
        if (!settings.EnableFasting) return false;
        if (!settings.FastingMondayThursdayOnly) return true;
        return date.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Thursday;
    }

    public static TimeSpan? ImsakTime(PrayerSchedule schedule) => TimeOf(schedule, "Imsak");

    public static TimeSpan? MaghribTime(PrayerSchedule schedule) => TimeOf(schedule, "Maghrib");

    /// <summary>Sedang di antara Imsak dan Maghrib pada hari puasa (waktu minum dan makan ditahan).</summary>
    public static bool IsFastingNow(AppSettings settings, PrayerSchedule schedule, DateTime now)
    {
        if (!IsFastingDay(settings, now)) return false;
        if (ImsakTime(schedule) is not { } imsak || MaghribTime(schedule) is not { } maghrib) return false;
        var t = now.TimeOfDay;
        return t >= imsak && t < maghrib;
    }

    private static TimeSpan? TimeOf(PrayerSchedule schedule, string name)
        => schedule.Items.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase))?.Time;
}
