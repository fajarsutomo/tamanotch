namespace TamaNotch.Helpers;

/// <summary>User-Agent Chrome Android agar situs web memakai layout mobile-nya di jendela pemutar.</summary>
public static class MobileUserAgent
{
    public const string FallbackChromeMajor = "130";

    /// <param name="browserVersion">Versi Edge/Chromium WebView2, mis. "130.0.2849.80".</param>
    public static string Build(string? browserVersion)
    {
        var major = FallbackChromeMajor;
        if (!string.IsNullOrWhiteSpace(browserVersion))
        {
            var first = browserVersion.Trim().Split('.')[0];
            if (first.Length > 0 && first.All(char.IsDigit)) major = first;
        }

        return $"Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{major}.0.0.0 Mobile Safari/537.36";
    }
}
