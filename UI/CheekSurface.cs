using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Momonga.Input;

namespace Momonga.UI;

public sealed class CheekSurface : FrameworkElement
{
    public ImageSource? Source { get; set; }
    public CheekDrag Drag { get; set; } = new();
    private ImageSource? cachedSource;
    private byte[] pixels = Array.Empty<byte>();
    private int width, height;
    protected override void OnRender(DrawingContext drawing)
    {
        if (Source == null || ActualWidth <= 0 || ActualHeight <= 0) return;
        // Keep the imported 240px layers intact; resampling the merged portrait loses ownership.
        const int w=240,h=240;
        if (cachedSource != Source || w != width || h != height)
        {
            cachedSource = Source; width = w; height = h;
            var scale = Math.Min(w / Source.Width, h / Source.Height);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) CharacterLayers.Draw(dc,Source, new Rect((w - Source.Width * scale) / 2, (h - Source.Height * scale) / 2, Source.Width * scale, Source.Height * scale));
            var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
            pixels = new byte[w * h * 4]; bitmap.CopyPixels(pixels, w * 4, 0);
        }
        var output = Drag.Warp(pixels,w,h,CharacterLayers.Model(Source));
        var result = BitmapSource.Create(w * 2, h * 2, 96, 96, PixelFormats.Pbgra32, null, output, w * 8);
        drawing.DrawImage(result, new Rect(0, 0, ActualWidth, ActualHeight));
    }
}
