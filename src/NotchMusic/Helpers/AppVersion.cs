using System.Reflection;

namespace NotchMusic.Helpers;

/// <summary>
/// Versi aplikasi (sumber kebenaran: &lt;Version&gt; di NotchMusic.csproj). Skema 0.x.y: x naik untuk fitur atau
/// elemen baru yang signifikan, y naik untuk perbaikan bug dan kembali ke 0 setiap x naik.
/// </summary>
public static class AppVersion
{
    public const string Fallback = "0.1.0";

    public static string Current
    {
        get
        {
            var asm = Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                return info.Split('+')[0].Trim();
            }

            var ver = asm.GetName().Version;
            return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : Fallback;
        }
    }

    /// <summary>Token produk untuk header User-Agent, mis. "TAMANOTCH/0.1".</summary>
    public static string UserAgentToken
    {
        get
        {
            VersionHelper.TryParseCleanVersion(Current, out var v);
            return $"TAMANOTCH/{v.Major}.{v.Minor}";
        }
    }
}
