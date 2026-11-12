using System.Globalization;

namespace TamaNotch.Services.ClaudeUsage;

/// <summary>Teks tampilan untuk jendela pemakaian Claude (bahasa Indonesia, zona waktu lokal).</summary>
public static class ClaudeUsageFormat
{
    private static readonly CultureInfo Id = CultureInfo.GetCultureInfo("id-ID");

    /// <summary>Durasi singkat: "3j 30m", "2h 4j" (h = hari), "5m", "kurang dari 1m".</summary>
    public static string Duration(TimeSpan span)
    {
        if (span <= TimeSpan.Zero) return "sebentar lagi";
        if (span.TotalMinutes < 1) return "kurang dari 1m";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours}j {span.Minutes}m";
        return $"{(int)span.TotalDays}h {span.Hours}j";
    }

    /// <summary>"Reset dalam 3j 30m (14:00)" untuk jendela dalam 24 jam, selain itu "Reset Kamis 10:00 (dalam 6h 7j)".</summary>
    public static string Reset(DateTimeOffset? resetsAt, DateTimeOffset now)
    {
        if (resetsAt is not { } reset) return "Waktu reset tidak diketahui";
        var local = reset.ToLocalTime();
        var remaining = reset - now;
        if (remaining <= TimeSpan.Zero) return "Segera direset";
        return remaining.TotalHours < 24
            ? $"Reset dalam {Duration(remaining)} ({local.ToString("HH\\:mm", Id)})"
            : $"Reset {local.ToString("dddd HH\\:mm", Id)} (dalam {Duration(remaining)})";
    }

    /// <summary>"Diperbarui baru saja" / "Diperbarui 5 menit lalu" / "... (data lama)" bila sudah basi.</summary>
    public static string Updated(DateTime? fetchedAt, DateTime now)
    {
        if (fetchedAt is not { } at) return string.Empty;
        var age = now - at;
        var text = age < TimeSpan.FromMinutes(1) ? "Diperbarui baru saja"
            : age < TimeSpan.FromHours(1) ? $"Diperbarui {(int)age.TotalMinutes} menit lalu"
            : $"Diperbarui {at.ToString("HH\\:mm", Id)}";
        return age > ClaudeUsageService.StaleAfter ? text + " (data lama)" : text;
    }

    public static string PlanName(string? subscriptionType)
    {
        if (string.IsNullOrWhiteSpace(subscriptionType)) return string.Empty;
        var s = subscriptionType.Trim();
        return "Claude " + char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();
    }

    public static string Status(ClaudeUsageStatus status, DateTimeOffset? retryAt) => status switch
    {
        ClaudeUsageStatus.NoCredentials => "Login Claude Code tidak ditemukan. Pasang dan login di Claude Code (perintah: claude auth login).",
        ClaudeUsageStatus.TokenExpired => "Token Claude Code sudah kedaluwarsa. Buka Claude Code sebentar agar token diperbarui; TAMANOTCH tidak memperbaruinya sendiri.",
        ClaudeUsageStatus.Unauthorized => "Akses ditolak oleh Claude. Login ulang di Claude Code.",
        ClaudeUsageStatus.RateLimited => retryAt is { } t
            ? $"Terlalu sering meminta data; mencoba lagi pukul {t.ToLocalTime().ToString("HH\\:mm", Id)}."
            : "Terlalu sering meminta data; akan dicoba lagi nanti.",
        ClaudeUsageStatus.Error => "Gagal mengambil data pemakaian. Akan dicoba lagi otomatis.",
        _ => string.Empty
    };
}
