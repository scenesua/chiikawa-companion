using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.IO;
namespace Momonga.UI;
public static class Icons
{
    private static readonly BitmapSource[] images = Load();
    public static readonly string BubbleWandPath = Path.Combine(Path.GetTempPath(), "momonga-bubble-wand.cur");
    public static readonly Cursor BubbleWand = CreateCursor(new BitmapImage(new Uri("pack://application:,,,/Assets/bubble-wand.png")), BubbleWandPath, 9, 9);
    private static readonly BitmapSource[] hands = Momonga.Animation.PetAnimator.LoadFrames("hand-cursors", "hand-cursors-frames", 4);
    public static readonly string FlickCursorPath = Path.Combine(Path.GetTempPath(), "momonga-flick.cur");
    public static readonly Cursor FlickCursor = CreateCursor(Momonga.Animation.PetAnimator.LoadFrames("flick-cursor", "flick-cursor-frames", 1)[0], FlickCursorPath, 22, 19);
    public static readonly string PokeCursorPath = Path.Combine(Path.GetTempPath(), "momonga-poke.cur");
    public static readonly Cursor PokeCursor = CreateCursor(images[3], PokeCursorPath);
    public static string HandCursorPath(int index) => Path.Combine(Path.GetTempPath(), "momonga-hand-" + index + ".cur");
    private static readonly Cursor[] handTools = { CreateCursor(hands[0], HandCursorPath(0)), CreateCursor(hands[1], HandCursorPath(1)), CreateCursor(hands[2], HandCursorPath(2)) };
    public static Cursor HandCursor(int index) => handTools[index];
    private static Cursor CreateCursor(ImageSource source, string? path = null, ushort hotspotX = 2, ushort hotspotY = 16)
    {
        var scale = Math.Min(32 / source.Width, 32 / source.Height);
        var width = source.Width * scale; var height = source.Height * scale;
        var visual = new DrawingVisual(); using (var drawing = visual.RenderOpen()) drawing.DrawImage(source, new Rect((32-width)/2, (32-height)/2, width, height));
        var bitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var rgba = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[32 * 32 * 4]; rgba.CopyPixels(pixels, 128, 0);
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
        writer.Write((ushort)0); writer.Write((ushort)2); writer.Write((ushort)1);
        writer.Write((byte)32); writer.Write((byte)32); writer.Write((byte)0); writer.Write((byte)0);
        writer.Write(hotspotX); writer.Write(hotspotY); writer.Write(40 + pixels.Length + 128); writer.Write(22);
        writer.Write(40); writer.Write(32); writer.Write(64); writer.Write((ushort)1); writer.Write((ushort)32);
        writer.Write(0); writer.Write(pixels.Length + 128); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
        for (var row = 31; row >= 0; row--) writer.Write(pixels, row * 128, 128);
        writer.Write(new byte[128]); writer.Flush(); if (path != null) File.WriteAllBytes(path, stream.ToArray()); stream.Position = 0; return new Cursor(stream);
    }
    public static Image WaterBowl(double size = 64) => new() { Source = new CroppedBitmap(new BitmapImage(new Uri("pack://application:,,,/Assets/water-bowl.png")), new Int32Rect(104, 341, 1045, 617)), Width = size, Height = size, Stretch = Stretch.Uniform };
    private static BitmapSource[] Load()
    {
        var sheet = new BitmapImage(new Uri("pack://application:,,,/Assets/ui-icons.png"));
        var result = new BitmapSource[20];
        int[][] bounds = {
            new[]{58,86,216,181}, new[]{316,88,236,175}, new[]{600,78,197,197}, new[]{836,91,256,171}, new[]{1123,79,224,193},
            new[]{51,320,247,217}, new[]{329,397,214,77}, new[]{600,343,204,186}, new[]{868,325,200,209}, new[]{1123,347,240,195},
            new[]{73,583,193,228}, new[]{355,595,160,217}, new[]{608,586,171,224}, new[]{857,611,228,192}, new[]{1140,607,193,186},
            new[]{50,849,222,224}, new[]{310,888,251,170}, new[]{589,862,261,205}, new[]{886,882,179,175}, new[]{1139,885,218,160}
        };
        for (var i = 0; i < result.Length; i++)
        {
            var b = bounds[i];
            var crop = new CroppedBitmap(sheet, new Int32Rect(b[0], b[1], b[2], b[3])); crop.Freeze(); result[i] = crop;
        }
        return result;
    }
    public static Image Image(int index, double size = 24) => new() { Source = images[index], Width = size, Height = size, Stretch = Stretch.Uniform };
    public static StackPanel Label(int index, string text)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(Image(index)); var label = Theme.Text(text, 12, true); label.Margin = new Thickness(4, 0, 0, 0);
        label.VerticalAlignment = VerticalAlignment.Center; panel.Children.Add(label); return panel;
    }
}
