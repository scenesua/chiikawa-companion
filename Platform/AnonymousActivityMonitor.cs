using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace Momonga.Platform;
// Only event types are inspected. Key codes, text and coordinates are never read.
public sealed class AnonymousActivityMonitor : IDisposable
{
    private delegate IntPtr Hook(int code, IntPtr message, IntPtr unused);
    private readonly Hook keyboardCallback, mouseCallback;
    private IntPtr keyboard, mouse;
    private int keys, clicks;
    public AnonymousActivityMonitor()
    {
        keyboardCallback = (code, message, unused) => { if (code >= 0 && ((long)message == 0x100 || (long)message == 0x104)) keys = Math.Min(keys + 1, 10000); return CallNextHookEx(IntPtr.Zero, code, message, unused); };
        mouseCallback = (code, message, unused) => { if (code >= 0 && ((long)message is 0x201 or 0x204 or 0x207 or 0x20B)) clicks = Math.Min(clicks + 1, 10000); return CallNextHookEx(IntPtr.Zero, code, message, unused); };
        keyboard = SetWindowsHookEx(13, keyboardCallback, GetModuleHandle(null), 0);
        mouse = SetWindowsHookEx(14, mouseCallback, GetModuleHandle(null), 0);
        if (keyboard == IntPtr.Zero || mouse == IntPtr.Zero) { var error = Marshal.GetLastWin32Error(); Dispose(); throw new Win32Exception(error); }
    }
    public (int Keys, int Clicks) Drain() { var result = (keys, clicks); keys = clicks = 0; return result; }
    public void Dispose() { if (keyboard != IntPtr.Zero) UnhookWindowsHookEx(keyboard); if (mouse != IntPtr.Zero) UnhookWindowsHookEx(mouse); keyboard = mouse = IntPtr.Zero; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, Hook callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr unused);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
}
