using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Momonga.Animation;

public static class CharacterSprites
{
    private static readonly Dictionary<string, BitmapSource[]> cache = new();
    private static readonly Dictionary<string, double> widths = new();
    private static readonly Dictionary<string, BitmapSource> portraits = new();
    private static readonly Dictionary<string, BitmapSource> emblems = new();
    public static BitmapSource Emblem
    {
        get
        {
            if (emblems.TryGetValue(Current, out var existing)) return existing;
            var box = Current switch
            {
                "chiikawa" => new Int32Rect(1,0,323,359), "hachiware" => new Int32Rect(344,0,326,370),
                "usagi" => new Int32Rect(699,0,310,457), "momonga" => new Int32Rect(1032,0,406,388),
                "kurimanju" => new Int32Rect(1458,0,307,352), "rakko" => new Int32Rect(2290,0,337,360),
                "shisa" => new Int32Rect(2636,0,398,355), "kani" => new Int32Rect(3463,0,303,409), _ => new Int32Rect()
            };
            if (box.IsEmpty || box.Width == 0)
            {
                if (Current is "mymelody" or "kuromi")
                {
                    var official = new BitmapImage(new Uri($"pack://application:,,,/Assets/references/{Current}-official.png")); official.Freeze(); return emblems[Current] = official;
                }
                return Portrait(Current);
            }
            var source = new CroppedBitmap(new BitmapImage(new Uri("pack://application:,,,/Assets/official-anime-models.png")),box); source.Freeze();
            return emblems[Current] = source;
        }
    }
    public static BitmapSource Portrait(string id)
    {
        if (!portraits.TryGetValue(id, out var portrait))
            portraits[id] = portrait = id == "momonga" ? PetAnimator.LoadFrames("momonga-idle", "momonga-idle-frames", 12)[0] : PetAnimator.LoadFrames(id + "-atlas", id + "-atlas-frames", 80)[2];
        return portrait;
    }
    public static string Current { get; private set; } = "momonga";
    public static double FaceY => Current switch { "usagi" => .62, "mymelody" or "kuromi" => .60, "ode" => .25, "anoko" => .43, "dekatsuyo" or "kurimanju" => .48, "rilakkuma" or "korilakkuma" => .46, _ => .55 };
    public static void Select(string id) { if (id != "momonga") _ = Atlas(id); Current = id; }
    private static BitmapSource[] Atlas(string id)
    {
        if (cache.TryGetValue(id, out var existing)) return existing;
        var crops = PetAnimator.LoadFrames(id + "-atlas", id + "-atlas-frames", 80);
        // One scale for the whole sheet preserves body size across sitting, sleeping and walking.
        var scale = Math.Min(228 / crops.Max(f => f.Width), 228 / crops.Max(f => f.Height));
        var result = crops.Select(f =>
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) dc.DrawImage(f, new Rect((240 - f.Width * scale) / 2, 235 - f.Height * scale, f.Width * scale, f.Height * scale));
            var padded = new RenderTargetBitmap(240, 240, 96, 96, PixelFormats.Pbgra32); padded.Render(visual); padded.Freeze(); return (BitmapSource)padded;
        }).ToArray();
        for (var row = 0; row < 3 && id is not ("mymelody" or "kuromi"); row++)
        {
            var rearRight = new TransformedBitmap(result[row * 8 + 5], new ScaleTransform(-1, 1));
            rearRight.Freeze(); result[row * 8 + 7] = rearRight;
        }
        cache[id] = result; return result;
    }
    public static BitmapSource[] Frames(string id, int start, int count) => Atlas(id).Skip(start).Take(count).ToArray();
    public static BitmapSource Frame(int index) => Atlas(Current)[index];
    public static double BodyWidth
    {
        get
        {
            if (widths.TryGetValue(Current, out var width)) return width;
            var frame = Frame(2); var pixels = new byte[240 * 240 * 4]; frame.CopyPixels(pixels, 240 * 4, 0);
            var left = 240; var right = 0;
            for (var y = 0; y < 240; y++) for (var x = 0; x < 240; x++)
                if (pixels[(y * 240 + x) * 4 + 3] > 100) { left = Math.Min(left, x); right = Math.Max(right, x); }
            return widths[Current] = (right - left + 1) / 240d;
        }
    }
}
