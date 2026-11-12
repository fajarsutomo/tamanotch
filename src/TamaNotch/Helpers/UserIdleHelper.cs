using System.Runtime.InteropServices;

namespace TamaNotch.Helpers;

/// <summary>Lama sejak input keyboard/mouse terakhir (Windows).</summary>
public static class UserIdleHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public static TimeSpan GetIdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;

        // Environment.TickCount64 lebih lebar dari dwTime (32-bit); bandingkan dalam 32-bit agar wrap-around aman.
        uint now = unchecked((uint)Environment.TickCount64);
        uint idleMs = unchecked(now - info.dwTime);
        return TimeSpan.FromMilliseconds(idleMs);
    }
}
