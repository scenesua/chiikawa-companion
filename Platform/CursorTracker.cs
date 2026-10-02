using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace Momonga.Platform;

public static class CursorTracker
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    // Cursor coordinates are transient; no hooks, key identities or input history.
    public static Point? RelativeTo(Visual visual)
    {
        if (!GetCursorPos(out var point)) return null;
        return visual.PointFromScreen(new Point(point.X, point.Y));
    }
}
