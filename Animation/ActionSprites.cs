using System.Windows.Media.Imaging;
namespace Momonga.Animation;
public static class ActionSprites
{
    private static readonly BitmapSource[] frames = PetAnimator.LoadFrames("momonga-affection", "momonga-affection-frames", 12);
    private static readonly System.Windows.Media.ImageSource[] expressions = Build();
    private static System.Windows.Media.ImageSource[] Build()
    {
        var result = new System.Windows.Media.ImageSource[12];
        for (var i = 0; i < 12; i++)
        {
            if (i is 0 or 8 or 9) { result[i] = frames[i]; continue; }
            var group = new System.Windows.Media.DrawingGroup();
            using (var dc = group.Open())
            {
                var rect = new System.Windows.Rect(0,0,frames[0].Width,frames[0].Height);
                dc.DrawImage(frames[0],rect);
                dc.PushClip(new System.Windows.Media.EllipseGeometry(new System.Windows.Rect(rect.Width*.15,rect.Height*.29,rect.Width*.70,rect.Height*.42)));
                dc.DrawImage(frames[i],rect); dc.Pop();
            }
            var image = new System.Windows.Media.DrawingImage(group); image.Freeze(); result[i] = image;
        }
        return result;
    }
    private static readonly System.Collections.Generic.Dictionary<string, System.Windows.Media.ImageSource[]> characterExpressions = new();
    public static System.Windows.Media.ImageSource Frame(int index)
    {
        if (CharacterSprites.Current == "momonga") return expressions[index];
        if (!characterExpressions.TryGetValue(CharacterSprites.Current, out var result))
        {
            result = new System.Windows.Media.ImageSource[12];
            for (var i = 0; i < result.Length; i++)
            {
                if (i is 0 or 8 or 9) { result[i] = CharacterSprites.Frame(44 + i); continue; }
                var group = new System.Windows.Media.DrawingGroup();
                using (var dc = group.Open())
                {
                    var rect = new System.Windows.Rect(0, 0, 240, 240);
                    dc.DrawImage(CharacterSprites.Frame(44), rect);
                    dc.PushClip(new System.Windows.Media.EllipseGeometry(new System.Windows.Rect(240 * .16, 240 * (CharacterSprites.FaceY - .17), 240 * .68, 240 * .36)));
                    dc.DrawImage(CharacterSprites.Frame(44 + i), rect); dc.Pop();
                }
                var image = new System.Windows.Media.DrawingImage(group); image.Freeze(); result[i] = image;
            }
            characterExpressions[CharacterSprites.Current] = result;
        }
        return result[index];
    }
}
public static class BowlContents
{
    private static readonly BitmapSource[] frames = Normalize(PetAnimator.LoadFrames("meal-bowls", "meal-bowl-frames", 16));
    private static readonly BitmapSource[] eatingFrames = Normalize(PetAnimator.LoadFrames("meal-bowls-eating", "meal-bowl-eating-frames", 16));
    private static BitmapSource[] Normalize(BitmapSource[] sources)
    {
        return System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(sources, source =>
        {
            var image = new System.Windows.Media.Imaging.FormatConvertedBitmap(source,System.Windows.Media.PixelFormats.Bgra32,null,0);
            var pixels = new byte[image.PixelWidth*image.PixelHeight*4]; image.CopyPixels(pixels,image.PixelWidth*4,0);
            var left = image.PixelWidth; var right = 0; var bottom = 0;
            // Measure the bowl shell below the food/utensil; anchor every state to the same width and floor.
            for (var y=image.PixelHeight/2; y<image.PixelHeight; y++)
                for (var x=0; x<image.PixelWidth; x++)
                    if (pixels[(y*image.PixelWidth+x)*4+3]>100) { left=System.Math.Min(left,x); right=System.Math.Max(right,x); bottom=y+1; }
            var scale = 280d/(right-left+1);
            var visual = new System.Windows.Media.DrawingVisual();
            using(var dc=visual.RenderOpen()) dc.DrawImage(source,new System.Windows.Rect(180-(left+right+1)*scale/2,355-bottom*scale,source.Width*scale,source.Height*scale));
            var result = new RenderTargetBitmap(360,360,96,96,System.Windows.Media.PixelFormats.Pbgra32); result.Render(visual); result.Freeze(); return (BitmapSource)result;
        }));
    }
    public static BitmapSource Empty => frames[12];
    public static BitmapSource Water(double amount) => frames[amount <= 0 ? 15 : amount <= 60 ? 14 : 13];
    public static BitmapSource Frame(string food, bool half = false, bool eating = false) => (eating ? eatingFrames : frames)[2 * (food switch { "furikake-rice" => 1, "curry-rice" => 2, "jiro-ramen" => 3, "nuts" => 4, "fruit" => 5, _ => 0 }) + (half ? 1 : 0)];
}
public static class FurnitureSprites
{
    private static readonly BitmapSource[] frames = PetAnimator.LoadFrames("furniture", "furniture-frames", 1);
    private static readonly BitmapSource[] variants = PetAnimator.LoadFrames("furniture-variants", "furniture-variants-frames", 4);
    public static BitmapSource Frame(int index) => frames[index];
    public static BitmapSource Item(string id) => id switch { "ball" => variants[0], "doll" => variants[1], "beanbag" => variants[2], "nest" => variants[3], _ => frames[0] };
}
public static class FoodSprites
{
    private static readonly BitmapSource[] frames = PetAnimator.LoadFrames("momonga-food-actions", "momonga-food-actions-frames", 12);
    private static readonly System.Collections.Generic.Dictionary<string,BitmapSource[]> snacks = new();
    public static BitmapSource Frame(int index)
    {
        var id = CharacterSprites.Current;
        if (id is "mymelody" or "kuromi")
        {
            if (!snacks.TryGetValue(id,out var result))
            {
                var raw=PetAnimator.LoadFrames(id+"-meals",id+"-snack-frames",12);
                var scale=System.Math.Min(228/System.Linq.Enumerable.Max(raw,f=>f.Width),228/System.Linq.Enumerable.Max(raw,f=>f.Height));
                result=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(raw,source=>
                {
                    var visual=new System.Windows.Media.DrawingVisual();
                    using(var dc=visual.RenderOpen()) dc.DrawImage(source,new System.Windows.Rect((240-source.Width*scale)/2,235-source.Height*scale,source.Width*scale,source.Height*scale));
                    var padded=new RenderTargetBitmap(240,240,96,96,System.Windows.Media.PixelFormats.Pbgra32); padded.Render(visual); padded.Freeze(); return (BitmapSource)padded;
                }));
                snacks[id]=result;
            }
            return result[index];
        }
        return id == "momonga" ? frames[index] : CharacterSprites.Frame(64 + index);
    }
}
