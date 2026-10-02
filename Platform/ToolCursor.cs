using System;
using System.Runtime.InteropServices;
namespace Momonga.Platform;
public static class ToolCursor
{
    private static bool changed;
    public static bool Active => changed;
    private static readonly string marker = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "momonga-cursor-active");
    public static void Recover() { if (System.IO.File.Exists(marker)) { changed = true; Restore(); } }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadCursorFromFile(string file);
    [DllImport("user32.dll")] private static extern bool SetSystemCursor(IntPtr cursor, uint id);
    [DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint action, uint parameter, IntPtr value, uint flags);
    public static void Show(string path)
    {
        Restore();
        System.IO.File.WriteAllText(marker, "active");
        foreach (var id in new uint[] {32512,32513,32514,32515,32516,32642,32643,32644,32645,32646,32648,32649,32650,32651})
        {
            var cursor = LoadCursorFromFile(path);
            if (cursor == IntPtr.Zero || !SetSystemCursor(cursor,id)) { Restore(); return; }
            changed = true;
        }
    }
    public static void Restore() { if (changed) SystemParametersInfo(0x57,0,IntPtr.Zero,0); changed = false; if (System.IO.File.Exists(marker)) System.IO.File.Delete(marker); }
}
