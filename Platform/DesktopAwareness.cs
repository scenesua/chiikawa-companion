using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Text;

namespace Momonga.Platform;

public static class DesktopAwareness
{
    [StructLayout(LayoutKind.Sequential)] private struct LastInput { public uint Size, Tick; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public uint Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LastInput input);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int count);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    // Only the elapsed time since anonymous input is read. No keyboard/mouse hooks or content inspection.
    public static bool IsActive()
    {
        var input = new LastInput { Size = (uint)Marshal.SizeOf<LastInput>() };
        return GetLastInputInfo(ref input) && unchecked((uint)Environment.TickCount - input.Tick) < 30_000;
    }
    public static bool IsFullscreen()
    {
        var window = GetForegroundWindow();
        var name = new StringBuilder(256); GetClassName(window, name, name.Capacity);
        if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;
        if (window == IntPtr.Zero || !GetWindowRect(window, out var rect)) return false;
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfo(MonitorFromWindow(window, 2), ref info) && rect.Left <= info.Monitor.Left &&
            rect.Top <= info.Monitor.Top && rect.Right >= info.Monitor.Right && rect.Bottom >= info.Monitor.Bottom;
    }

    public static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) key.SetValue("MomongaDesktopCompanion", "\"" + Environment.ProcessPath + "\"");
        else key.DeleteValue("MomongaDesktopCompanion", false);
    }
}
