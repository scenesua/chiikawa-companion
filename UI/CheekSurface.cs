using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Momonga.UI;

public sealed class CheekSurface : FrameworkElement
{
    public ImageSource? Source { get; set; }
    public bool LeftCheek { get; set; }
    public double Pull { get; set; }
    private ImageSource? cachedSource;
    private byte[] pixels = Array.Empty<byte>();
    private int width, height;
    private double imageLeft, imageTop, imageWidth, imageHeight;
    internal static double MapX(double x, double tip, double inner, double pull, bool left)
    {
        if (left)
            return x >= inner ? x : x <= tip - pull ? x + pull : inner - (inner - x) * (inner - tip) / (inner - tip + pull);
        return x <= inner ? x : x >= tip + pull ? x - pull : inner + (x - inner) * (tip - inner) / (tip - inner + pull);
    }
    protected override void OnRender(DrawingContext drawing)
    {
        if (Source == null || ActualWidth <= 0 || ActualHeight <= 0) return;
        var w = (int)Math.Ceiling(ActualWidth * 2); var h = (int)Math.Ceiling(ActualHeight * 2);
        if (cachedSource != Source || w != width || h != height)
        {
            cachedSource = Source; width = w; height = h;
            var scale = Math.Min(w / Source.Width, h / Source.Height);
            imageWidth = Source.Width * scale; imageHeight = Source.Height * scale;
            imageLeft = (w - imageWidth) / 2; imageTop = (h - imageHeight) / 2;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) dc.DrawImage(Source, new Rect(imageLeft, imageTop, imageWidth, imageHeight));
            var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
            pixels = new byte[w * h * 4]; bitmap.CopyPixels(pixels, w * 4, 0);
        }
        var pad = (int)Math.Ceiling(w * .26); var outputWidth = w + pad * 2;
        var output = new byte[outputWidth * h * 4];
        var momonga = Animation.CharacterSprites.Current == "momonga";
        var tip = imageLeft + imageWidth * (LeftCheek ? .22 : (momonga ? .65 : .78));
        var inner = imageLeft + imageWidth * (LeftCheek ? (momonga ? .32 : .36) : (momonga ? .54 : .64));
        for (var y = 0; y < h; y++)
        {
            var dy = (y - imageTop - imageHeight * (momonga ? .56 : Animation.CharacterSprites.FaceY + .06)) / (imageHeight * (momonga ? .035 : .055));
            var pull = Pull * 2 * Math.Pow(Math.Max(0, 1 - dy * dy), 2);
            for (var x = 0; x < outputWidth; x++)
            {
                var target = x - pad;
                var sx = MapX(target, tip, inner, pull, LeftCheek);
                var ix = (int)Math.Floor(sx); var fraction = sx - ix;
                if (ix < 0 || ix + 1 >= w) continue;
                var offset = (y * w + ix) * 4;
                // Keep the blue tail fixed while the white cheek stretches over it.
                if (momonga && pixels[offset] > pixels[offset + 2] + 15 && target >= 0 && target < w)
                { offset = (y * w + target) * 4; fraction = 0; }
                var next = Math.Min(offset + 4, pixels.Length - 4);
                for (var channel = 0; channel < 4; channel++)
                    output[(y * outputWidth + x) * 4 + channel] = (byte)(pixels[offset + channel] * (1 - fraction) + pixels[next + channel] * fraction);
            }
        }
        var result = BitmapSource.Create(outputWidth, h, 96, 96, PixelFormats.Pbgra32, null, output, outputWidth * 4);
        drawing.DrawImage(result, new Rect(-pad / 2.0, 0, outputWidth / 2.0, h / 2.0));
    }
}
