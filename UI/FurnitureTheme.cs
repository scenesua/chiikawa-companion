using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Momonga.Animation;

namespace Momonga.UI;

public static class FurnitureTheme
{
    private static readonly Dictionary<(string, BitmapSource, bool), ImageSource> cache = new();
    public static ImageSource Frame(BitmapSource original, bool cushion)
    {
        var key = (CharacterSprites.Current, original, cushion);
        if (cache.TryGetValue(key, out var existing)) return existing;
        // Live palette rendering; original artwork and food/water colors remain intact.
        var source = new FormatConvertedBitmap(original, PixelFormats.Bgra32, null, 0);
        var width = source.PixelWidth; var height = source.PixelHeight;
        var pixels = new byte[width * height * 4]; source.CopyPixels(pixels, width * 4, 0);
        var color = ((SolidColorBrush)Theme.Brush("Soft")).Color;
        for (var p = 0; p < pixels.Length; p += 4)
        {
            var b = pixels[p]; var g = pixels[p+1]; var r = pixels[p+2];
            if (pixels[p+3] == 0 || r < 95 || r <= g + 8 || b <= g + 8) continue;
            var shade = Math.Clamp((r + g + b) / (3d * 225), .5, 1.08);
            pixels[p] = (byte)Math.Clamp(color.B * shade,0,255); pixels[p+1] = (byte)Math.Clamp(color.G * shade,0,255); pixels[p+2] = (byte)Math.Clamp(color.R * shade,0,255);
        }
        var tinted = BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4); tinted.Freeze();
        var group = new DrawingGroup();
        var accent = new SolidColorBrush(((SolidColorBrush)Theme.Brush("Accent")).Color); accent.Freeze();
        using (var dc = group.Open())
        {
            dc.DrawImage(tinted, new Rect(0,0,width,height));
            var mark = cushion ? new Rect(width*.42,height*.42,width*.16,height*.35) : new Rect(width*.28,height*.83,width*.10,height*.12);
            dc.PushOpacity(.55); dc.PushOpacityMask(new ImageBrush(CharacterSprites.Emblem) { Stretch = Stretch.Uniform });
            dc.DrawRectangle(accent,null,mark); dc.Pop(); dc.Pop();
        }
        var result = new DrawingImage(group); result.Freeze(); return cache[key] = result;
    }
}
