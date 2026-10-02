using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Momonga.Platform;

public static class WindowPolicy
{
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    public static bool IsBelow(Window furniture, Window pet)
    {
        var item = new WindowInteropHelper(furniture).Handle; var actor = new WindowInteropHelper(pet).Handle;
        for (var i = 0; i < 200 && item != IntPtr.Zero; i++) { item = GetWindow(item, 3); if (item == actor) return true; }
        return false;
    }
    public static void Top(Window window) { var h = new WindowInteropHelper(window).Handle; if (h != IntPtr.Zero) SetWindowPos(h, new IntPtr(-1), 0,0,0,0,0x13); }
    public static void Behind(Window furniture, Window pet)
    {
        var item = new WindowInteropHelper(furniture).Handle; var actor = new WindowInteropHelper(pet).Handle;
        if (item != IntPtr.Zero && actor != IntPtr.Zero) SetWindowPos(item, actor, 0, 0, 0, 0, 0x13);
    }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetStyle(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetStyle(IntPtr window, int index, int value);
    public static void Passive(Window window, bool clickThrough)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var style = GetStyle(handle, -20) | 0x08000000 | 0x00000080;
        style = clickThrough ? style | 0x20 : style & ~0x20;
        SetStyle(handle, -20, style);
    }
}
