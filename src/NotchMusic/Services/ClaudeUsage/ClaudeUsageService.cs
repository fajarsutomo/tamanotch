using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows.Threading;
using NotchMusic.Helpers;

namespace NotchMusic.Services.ClaudeUsage;

/// <summary>Satu jendela kuota (sesi 5 jam, mingguan, atau mingguan per model).</summary>
public sealed record ClaudeUsageWindow(string Key, string Label, double Percent, DateTimeOffset? ResetsAt, TimeSpan Period)
{
    /// <summary>Bagian periode yang sudah berjalan (0..1); null bila waktu reset tidak diketahui.</summary>
    public double? ElapsedFraction(DateTimeOffset now)
    {
        if (ResetsAt is not { } reset || Period <= TimeSpan.Zero) return null;
        var remaining = reset - now;
        return Math.Clamp(1 - remaining / Period, 0, 1);
    }
}

/// <summary>Porsi pemakaian mingguan per sumber (Claude Code, Chats, Cowork, ...).</summary>
public sealed record ClaudeUsageShare(string Name, double Percent);

public sealed record ClaudeUsageSnapshot(
    string? Plan,
    IReadOnlyList<ClaudeUsageWindow> Windows,
    IReadOnlyList<ClaudeUsageShare> Shares,
    DateTime FetchedAt);

public enum ClaudeUsageStatus
{
    Idle,
    Ok,
    NoCredentials,
    TokenExpired,
    Unauthorized,
    RateLimited,
    Error
}

public enum ClaudeUsageLevel { Normal, Warning, Critical }

/// <summary>Token OAuth Claude Code yang dibaca dari berkas kredensialnya. Hanya dibaca, tidak pernah ditulis.</summary>
public sealed record ClaudeCredentials(string AccessToken, DateTimeOffset? ExpiresAt, string? SubscriptionType);

public interface IClaudeUsageService : IDisposable
{
    ClaudeUsageSnapshot? Current { get; }

    ClaudeUsageStatus Status { get; }

    /// <summary>Kapan permintaan berikutnya boleh dikirim (saat kena batas laju); null bila tidak ditahan.</summary>
    DateTimeOffset? NextAllowedAt { get; }

    event EventHandler? Updated;

    void Start();

    void Stop();

    Task RefreshAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Pemakaian kuota langganan Claude (sesi 5 jam dan mingguan) dari endpoint usage milik Claude Code.
/// Memakai token OAuth yang sudah disimpan Claude Code (hanya dibaca; tidak pernah di-refresh atau ditulis ulang).
/// Endpoint ini tidak terdokumentasi resmi, jadi semua pembacaan harus tahan terhadap field yang hilang.
/// </summary>
public sealed class ClaudeUsageService : IClaudeUsageService
{
    public const string Endpoint = "https://api.anthropic.com/api/oauth/usage";

    public static readonly TimeSpan SessionPeriod = TimeSpan.FromHours(5);
    public static readonly TimeSpan WeeklyPeriod = TimeSpan.FromDays(7);

    /// <summary>Data yang lebih tua dari ini ditandai basi di tampilan.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    private static readonly HttpClient SharedClient = CreateClient();

    private readonly Func<ClaudeCredentials?> _credentials;
    private readonly Func<bool> _isEnabled;
    private readonly Func<TimeSpan> _interval;
    private readonly Func<DateTimeOffset> _now;
    private readonly HttpClient _http;
    private readonly DispatcherTimer? _timer;
    private int _refreshing;
    private int _rateLimitAttempts;

    public ClaudeUsageService(
        Func<bool> isEnabled,
        Func<TimeSpan> interval,
        Func<ClaudeCredentials?>? credentials = null,
        HttpClient? http = null,
        Func<DateTimeOffset>? now = null,
        bool useTimer = true)
    {
        _isEnabled = isEnabled;
        _interval = interval;
        _credentials = credentials ?? (() => ReadCredentials(DefaultCredentialsPath()));
        _http = http ?? SharedClient;
        _now = now ?? (() => DateTimeOffset.Now);

        if (useTimer)
        {
            _timer = new DispatcherTimer { Interval = ClampInterval(_interval()) };
            _timer.Tick += async (s, e) => await RefreshAsync();
        }
    }

    public ClaudeUsageSnapshot? Current { get; private set; }

    public ClaudeUsageStatus Status { get; private set; } = ClaudeUsageStatus.Idle;

    public DateTimeOffset? NextAllowedAt { get; private set; }

    public event EventHandler? Updated;

    public void Start()
    {
        if (_timer != null)
        {
            _timer.Interval = ClampInterval(_interval());
            _timer.Start();
        }

        _ = RefreshAsync();
    }

    public void Stop()
    {
        _timer?.Stop();
        Current = null;
        Status = ClaudeUsageStatus.Idle;
        NextAllowedAt = null;
        _rateLimitAttempts = 0;
        Updated?.Invoke(this, EventArgs.Empty);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!_isEnabled()) return;
        // Satu permintaan pada satu waktu; yang menyusul dibuang karena hasilnya sama.
        if (Interlocked.Exchange(ref _refreshing, 1) == 1) return;

        try
        {
            if (_timer != null) _timer.Interval = ClampInterval(_interval());

            var now = _now();
            if (NextAllowedAt is { } hold && hold > now)
            {
                // Masih dalam masa tahan: jangan menembak endpoint lagi, cukup perbarui tampilan.
                Updated?.Invoke(this, EventArgs.Empty);
                return;
            }

            var credentials = _credentials();
            if (credentials == null || string.IsNullOrWhiteSpace(credentials.AccessToken))
            {
                SetStatus(ClaudeUsageStatus.NoCredentials);
                return;
            }

            if (credentials.ExpiresAt is { } expires && expires <= now)
            {
                // Claude Code yang memperbarui tokennya; kita tidak menyentuhnya.
                SetStatus(ClaudeUsageStatus.TokenExpired);
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
            request.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            switch (response.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                case HttpStatusCode.Forbidden:
                    SetStatus(ClaudeUsageStatus.Unauthorized);
                    return;
                case HttpStatusCode.TooManyRequests:
                    ApplyRateLimit(response, now);
                    SetStatus(ClaudeUsageStatus.RateLimited);
                    return;
            }

            if (!response.IsSuccessStatusCode)
            {
                SetStatus(ClaudeUsageStatus.Error);
                return;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var snapshot = Parse(json, DateTime.Now, credentials.SubscriptionType);
            if (snapshot == null)
            {
                SetStatus(ClaudeUsageStatus.Error);
                return;
            }

            _rateLimitAttempts = 0;
            NextAllowedAt = null;
            Current = snapshot;
            Status = ClaudeUsageStatus.Ok;
            Updated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            // Jaringan mati atau layanan bermasalah: angka lama dipertahankan (ditandai basi) sampai pulih.
            SetStatus(ClaudeUsageStatus.Error);
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    private void SetStatus(ClaudeUsageStatus status)
    {
        Status = status;
        Updated?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyRateLimit(HttpResponseMessage response, DateTimeOffset now)
    {
        _rateLimitAttempts = Math.Min(_rateLimitAttempts + 1, 10);
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            NextAllowedAt = now + delta;
            return;
        }

        if (retryAfter?.Date is { } date && date > now)
        {
            NextAllowedAt = date;
            return;
        }

        NextAllowedAt = now.AddSeconds(Math.Min(900, 30 * Math.Pow(2, _rateLimitAttempts - 1)));
    }

    public static TimeSpan ClampInterval(TimeSpan value)
        => value < TimeSpan.FromMinutes(2) ? TimeSpan.FromMinutes(2) : value > TimeSpan.FromMinutes(30) ? TimeSpan.FromMinutes(30) : value;

    /// <summary>Lokasi berkas kredensial Claude Code: CLAUDE_CONFIG_DIR bila diset, selain itu ~/.claude.</summary>
    public static string DefaultCredentialsPath()
    {
        var dir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        if (string.IsNullOrWhiteSpace(dir))
            dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        return Path.Combine(dir, ".credentials.json");
    }

    public static ClaudeCredentials? ReadCredentials(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return ParseCredentials(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static ClaudeCredentials? ParseCredentials(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("claudeAiOauth", out var oauth) || oauth.ValueKind != JsonValueKind.Object)
            return null;

        var token = oauth.TryGetProperty("accessToken", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(token)) return null;

        DateTimeOffset? expires = null;
        if (oauth.TryGetProperty("expiresAt", out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var ms))
            expires = DateTimeOffset.FromUnixTimeMilliseconds(ms);

        var plan = oauth.TryGetProperty("subscriptionType", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
        return new ClaudeCredentials(token, expires, plan);
    }

    /// <summary>
    /// Membaca respons usage. Jendela "five_hour" dan "seven_day" serta setiap "seven_day_*" yang berisi
    /// angka "utilization" ikut ditampilkan; yang null dilewati. Null bila tidak ada satu pun jendela.
    /// </summary>
    public static ClaudeUsageSnapshot? Parse(string json, DateTime fetchedAt, string? plan = null)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;

        var windows = new List<ClaudeUsageWindow>();
        AddWindow(root, "five_hour", "Sesi (5 jam)", SessionPeriod, windows);
        AddWindow(root, "seven_day", "Mingguan (7 hari)", WeeklyPeriod, windows);

        foreach (var property in root.EnumerateObject())
        {
            var name = property.Name;
            if (!name.StartsWith("seven_day_", StringComparison.Ordinal) || name == "seven_day_breakdown") continue;
            AddWindow(root, name, ScopedLabel(name["seven_day_".Length..]), WeeklyPeriod, windows);
        }

        if (windows.Count == 0) return null;

        var shares = new List<ClaudeUsageShare>();
        if (root.TryGetProperty("seven_day_breakdown", out var breakdown) && breakdown.ValueKind == JsonValueKind.Object
            && breakdown.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object) continue;
                var label = row.TryGetProperty("display_name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(label) || !TryGetNumber(row, "percent", out var percent)) continue;
                shares.Add(new ClaudeUsageShare(label, Math.Clamp(percent, 0, 100)));
            }
        }

        return new ClaudeUsageSnapshot(plan, windows, shares, fetchedAt);
    }

    private static void AddWindow(JsonElement root, string key, string label, TimeSpan period, List<ClaudeUsageWindow> into)
    {
        if (!root.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Object) return;
        if (!TryGetNumber(el, "utilization", out var utilization)) return;

        DateTimeOffset? reset = null;
        if (el.TryGetProperty("resets_at", out var r) && r.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(r.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            reset = parsed;

        into.Add(new ClaudeUsageWindow(key, label, Math.Clamp(utilization, 0, 100), reset, period));
    }

    private static string ScopedLabel(string suffix) => suffix switch
    {
        "opus" => "Mingguan Opus",
        "sonnet" => "Mingguan Sonnet",
        "cowork" => "Mingguan Cowork",
        _ => "Mingguan " + char.ToUpperInvariant(suffix[0]) + suffix[1..].Replace('_', ' ')
    };

    private static bool TryGetNumber(JsonElement obj, string name, out double value)
    {
        value = 0;
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Number) return false;
        value = el.GetDouble();
        return true;
    }

    /// <summary>
    /// Tingkat peringatan: kritis mulai 90%; peringatan mulai 75% atau saat pemakaian jauh mendahului waktu
    /// yang sudah berjalan (mis. 40% terpakai padahal baru 10% periode lewat).
    /// </summary>
    public static ClaudeUsageLevel Classify(double percent, double? elapsedFraction)
    {
        if (percent >= 90) return ClaudeUsageLevel.Critical;
        if (percent >= 75) return ClaudeUsageLevel.Warning;
        if (elapsedFraction is { } e && percent >= 30 && percent > e * 100 + 20) return ClaudeUsageLevel.Warning;
        return ClaudeUsageLevel.Normal;
    }

    public void Dispose() => _timer?.Stop();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppVersion.UserAgentToken} (Windows; Claude usage monitor)");
        return client;
    }
}
