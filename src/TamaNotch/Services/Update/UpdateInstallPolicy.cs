namespace TamaNotch.Services.Update;

/// <summary>
/// Kapan pembaruan otomatis boleh dipasang. Memasang menutup TAMANOTCH beberapa detik, jadi hanya dilakukan saat
/// tidak ada yang sedang diputar, adzan tidak berjalan, island tertutup, dan pengguna sudah lama tidak menyentuh PC.
/// </summary>
public static class UpdateInstallPolicy
{
    public static readonly TimeSpan RequiredUserIdle = TimeSpan.FromMinutes(5);

    public static bool IsGoodMomentToInstall(bool playbackActive, bool adhanActive, bool islandOpen, TimeSpan userIdle)
        => !playbackActive && !adhanActive && !islandOpen && userIdle >= RequiredUserIdle;
}
