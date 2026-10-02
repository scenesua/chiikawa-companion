using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace Momonga.Platform;

public static class MonitorService
{
    public static (string Id, Rect Work)[] Areas(Window window)
    {
        var transform = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        return Forms.Screen.AllScreens.Select(s => (s.DeviceName, new MatrixTransform(transform).TransformBounds(new Rect(s.WorkingArea.X, s.WorkingArea.Y,
            s.WorkingArea.Width, s.WorkingArea.Height)))).ToArray();
    }
    public static (string Id, Rect Work) At(Window window, Point point)
    {
        var areas = Areas(window);
        return areas.OrderBy(a => a.Work.Contains(point) ? 0 : (new Point(a.Work.X + a.Work.Width / 2, a.Work.Y + a.Work.Height / 2) - point).Length + 1).First();
    }
    public static Rect MovementBounds(Window window)
    {
        var point = new Point(double.IsFinite(window.Left) ? window.Left + window.Width / 2 : 0,
            double.IsFinite(window.Top) ? window.Top + window.Height / 2 : 0);
        var work = At(window, point).Work;
        return new Rect(work.Left, work.Top, Math.Max(0, work.Width - window.Width), Math.Max(0, work.Height - window.Height));
    }
    public static void PlacePopup(Window popup, Window pet)
    {
        var work = At(pet, new Point(pet.Left, pet.Top)).Work;
        popup.Left = Math.Clamp(pet.Left + pet.Width / 2 - popup.Width / 2, work.Left, Math.Max(work.Left, work.Right - popup.Width));
        popup.Top = Math.Clamp(pet.Top - popup.Height - 12, work.Top, Math.Max(work.Top, work.Bottom - popup.Height));
    }
}
