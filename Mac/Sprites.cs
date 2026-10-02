using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Text.Json;

namespace Momonga.Animation;

public enum PetPose { Happy, Eat, Sleep, Annoyed, Sulk, Startled, Dragged, Play }
public static class CharacterSprites
{
    public static string Current { get; set; } = "momonga";
    public static double FaceY => Current switch { "usagi" => .62, "mymelody" or "kuromi" => .60, "ode" => .25, "anoko" => .43, _ => .55 };
}

public sealed record Sprite(Bitmap Atlas, Rect Crop, Rect Draw, int[][] Cuts);
public static class Sprites
{
    private static readonly Dictionary<string, Sprite[]> cache = new();
    private static readonly Dictionary<string,int[][][]> exclusions=LoadExclusions();
    private static readonly Dictionary<string,double[][]> bowlAnchors=LoadBowlAnchors();
    private static Dictionary<string,double[][]> LoadBowlAnchors()
    {
        using var stream=AssetLoader.Open(new Uri("avares://Chiikawa.Companion/Assets/bowl-anchors.json"));
        return JsonSerializer.Deserialize<Dictionary<string,double[][]>>(stream)!;
    }
    private static Dictionary<string,int[][][]> LoadExclusions()
    {
        using var stream=AssetLoader.Open(new Uri("avares://Chiikawa.Companion/Assets/sprite-exclusions.json"));
        return JsonSerializer.Deserialize<Dictionary<string,int[][][]>>(stream)!;
    }
    public static void Draw(DrawingContext context,Sprite sprite,Rect draw)
    {
        if(sprite.Cuts.Length==0){context.DrawImage(sprite.Atlas,sprite.Crop,draw);return;}
        var clip=new GeometryGroup {FillRule=FillRule.EvenOdd};clip.Children.Add(new RectangleGeometry(draw));
        foreach(var cut in sprite.Cuts)clip.Children.Add(new RectangleGeometry(new Rect(draw.X+cut[0]*draw.Width/sprite.Crop.Width,draw.Y+cut[1]*draw.Height/sprite.Crop.Height,cut[2]*draw.Width/sprite.Crop.Width,cut[3]*draw.Height/sprite.Crop.Height)));
        using(context.PushGeometryClip(clip))context.DrawImage(sprite.Atlas,sprite.Crop,draw);
    }
    public static Sprite[] Load(string image, string metadata, bool normalize = true)
    {
        var key=image+":"+metadata+":"+normalize;
        if(cache.TryGetValue(key,out var result)) return result;
        using var stream=AssetLoader.Open(new Uri("avares://Chiikawa.Companion/Assets/"+image+".png"));
        var bitmap=new Bitmap(stream);
        using var json=AssetLoader.Open(new Uri("avares://Chiikawa.Companion/Assets/"+metadata+".json"));
        var boxes=JsonSerializer.Deserialize<int[][]>(json) ?? throw new InvalidDataException("Missing sprite crops");
        if(boxes.Any(b=>b.Length!=4 || b[0]<0 || b[1]<0 || b[2]<=0 || b[3]<=0 || b[0]+b[2]>bitmap.PixelSize.Width || b[1]+b[3]>bitmap.PixelSize.Height)) throw new InvalidDataException("Invalid sprite crop: "+metadata);
        var scale=Math.Min(228d/boxes.Max(b=>b[2]),228d/boxes.Max(b=>b[3]));
        result=boxes.Select((b,i)=>new Sprite(bitmap,new Rect(b[0],b[1],b[2],b[3]),normalize ? new Rect((240-b[2]*scale)/2,235-b[3]*scale,b[2]*scale,b[3]*scale) : new Rect(0,0,240,240d*b[3]/b[2]),exclusions.TryGetValue(metadata,out var masks)?masks[i]:Array.Empty<int[]>())).ToArray();
        if(bowlAnchors.TryGetValue(image,out var anchors))
            for(var i=0;i<result.Length;i++)
            {
                var a=anchors[i];var s=280/(a[1]-a[0]);var b=boxes[i];
                result[i]=result[i] with {Draw=new Rect((180-(a[0]+a[1])*s/2)*240/360,(355-a[2]*s)*240/360,b[2]*s*240/360,b[3]*s*240/360)};
            }
        return cache[key]=result;
    }
    public static Sprite Frame(string id,int index) => id=="momonga" ? Load("momonga-sprites","momonga-frames")[index] : Load(id+"-atlas",id+"-atlas-frames")[index];
    public static Sprite Idle(string id,int index) => id=="momonga" ? Load("momonga-idle","momonga-idle-frames")[index] : Frame(id,32+index);
    public static Sprite Actor(string id,int index) => id=="momonga" ? Load("momonga-scene-actors","momonga-scene-actor-frames")[index] : Frame(id,56+index);
    private static readonly string[] drinkIds={"chiikawa","hachiware","usagi","kurimanju","shisa","kani","rakko","anoko","goblin","chiikabu","ode","rilakkuma","korilakkuma","mymelody","kuromi","dekatsuyo"};
    public static Sprite Drink(string id,int beat)
    {
        if(id=="momonga") return Actor(id,6+beat);
        var i=Array.IndexOf(drinkIds,id); return Load("drinking-"+i/4,"drinking-"+i/4+"-frames")[i%4*2+beat];
    }
    public static Sprite Meal(string id,string food,int beat)
    {
        var i=food switch {"furikake-rice"=>2,"curry-rice"=>4,"jiro-ramen"=>6,"nuts"=>8,"fruit"=>10,_=>0};
        return Load(id+"-meals",id+"-meal-frames")[i+beat];
    }
    public static Sprite Snack(string id,int index) => id is "mymelody" or "kuromi" ? Load(id+"-meals",id+"-snack-frames")[index] : id=="momonga" ? Load("momonga-food-actions","momonga-food-actions-frames")[index] : Frame(id,64+index);
    public static Sprite Furniture(string id) => id switch
    {
        "ball"=>Load("furniture-variants","furniture-variants-frames",false)[0],
        "doll"=>Load("furniture-variants","furniture-variants-frames",false)[1],
        "beanbag"=>Load("furniture-variants","furniture-variants-frames",false)[2],
        "nest"=>Load("furniture-variants","furniture-variants-frames",false)[3],
        _=>Load("furniture","furniture-frames",false)[0]
    };
    public static Sprite Bowl(string food,double portion,bool eating=false)
    {
        var i=portion<=0 ? 12 : 2*(food switch {"furikake-rice"=>1,"curry-rice"=>2,"jiro-ramen"=>3,"nuts"=>4,"fruit"=>5,_=>0})+(portion<.99999?1:0);
        return Load(eating?"meal-bowls-eating":"meal-bowls",eating?"meal-bowl-eating-frames":"meal-bowl-frames",false)[i];
    }
    public static Sprite Water(double water) => Load("meal-bowls","meal-bowl-frames",false)[water<=0?15:water<=60?14:13];
}

public sealed class SpriteView : Control
{
    private Sprite? sprite;
    public Sprite? Sprite { get=>sprite; set { sprite=value; InvalidateVisual(); } }
    public double CheekPull { get; set; }
    public bool LeftCheek { get; set; }
    public override void Render(DrawingContext context)
    {
        base.Render(context); if(sprite==null) return;
        var scale=Bounds.Width/240;
        var draw=new Rect(sprite.Draw.X*scale,sprite.Draw.Y*scale,sprite.Draw.Width*scale,sprite.Draw.Height*scale);
        if(CheekPull<=0) { Sprites.Draw(context,sprite,draw); return; }
        var cheek=new Rect(Bounds.Width*(LeftCheek?.24:.60),Bounds.Height*(CharacterSprites.FaceY-.08),Bounds.Width*.17,Bounds.Height*.16);
        // Only the grabbed cheek stretches; the rest of the sprite keeps its original geometry.
        foreach(var region in new[]{new Rect(0,0,Bounds.Width,cheek.Top),new Rect(0,cheek.Bottom,Bounds.Width,Math.Max(0,Bounds.Height-cheek.Bottom)),new Rect(0,cheek.Top,cheek.Left,cheek.Height),new Rect(cheek.Right,cheek.Top,Math.Max(0,Bounds.Width-cheek.Right),cheek.Height)})
            using(context.PushClip(region)) Sprites.Draw(context,sprite,draw);
        using(context.PushClip(new Rect(LeftCheek?cheek.Left-CheekPull:cheek.Left,cheek.Top,cheek.Width+CheekPull,cheek.Height)))
        using(context.PushTransform(Matrix.CreateTranslation(-cheek.Left,0)*Matrix.CreateScale(1+CheekPull/cheek.Width,1)*Matrix.CreateTranslation(cheek.Left-(LeftCheek?CheekPull:0),0))) Sprites.Draw(context,sprite,draw);
    }
}
