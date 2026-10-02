using System;
using System.Drawing;
using System.Windows.Forms;

namespace Momonga.Platform;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon icon;
    private readonly ContextMenuStrip menu = new();
    private readonly Icon petIcon;
    public bool IsVisible => icon.Visible;
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);

    public TrayService(Action show, Action hide, Action exit, System.Windows.Media.ImageSource portrait, string name)
    {
        var visual = new System.Windows.Media.DrawingVisual();
        using (var drawing = visual.RenderOpen()) drawing.DrawImage(portrait, new System.Windows.Rect(0, 0, 32, 32));
        var image = new System.Windows.Media.Imaging.RenderTargetBitmap(32, 32, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); image.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        using var bytes = new System.IO.MemoryStream(); encoder.Save(bytes); bytes.Position = 0;
        using var bitmap = new Bitmap(bytes); var handle = bitmap.GetHicon();
        try { petIcon = (Icon)Icon.FromHandle(handle).Clone(); } finally { DestroyIcon(handle); }
        menu.Items.Add("펫 불러오기", null, (_, _) => show());
        menu.Items.Add("잠시 숨기기", null, (_, _) => hide());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => exit());
        icon = new NotifyIcon
        {
            Text = name + " · 클릭하면 불러오기",
            Icon = petIcon,
            ContextMenuStrip = menu,
            Visible = true
        };
        icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) show(); };
    }

    public void EnsureVisible() { icon.Visible = false; icon.Visible = true; }
    public void Dispose() { icon.Visible = false; icon.Dispose(); menu.Dispose(); petIcon.Dispose(); }
    public void Add(string label, Action action) => menu.Items.Insert(0, new ToolStripMenuItem(label, null, (_, _) => action()));
}
