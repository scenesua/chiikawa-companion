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
    public static double FaceY => Current switch { "usagi" => .62, "mymelody" or "kuromi" => .60, "ode" => .25, "anoko" => .43, "dekatsuyo" or "kurimanju" => .48, "rilakkuma" or "korilakkuma" => .46, _ => .55 };
}

public sealed record Sprite(Bitmap Atlas, Rect Crop, Rect Draw, int[][] Cuts)
{
    public Momonga.Character.CharacterPose? Pose { get; init; }
}
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
    public static Sprite Character(Sprite sprite,string id,string clip="unregistered",int frame=0,int facing=-1) => sprite.Pose!=null&&clip=="unregistered" ? sprite : sprite with {Pose=new Momonga.Character.CharacterPose(id,clip,frame,facing)};
    public static void Draw(DrawingContext context,Sprite sprite,Rect draw,double? time=null)
    {
        if(sprite.Pose!=null){CharacterLayers.Draw(context,sprite,draw,chewing:sprite.Pose.Clip is "meal" or "snack",time:time);return;}
        DrawRaw(context,sprite,draw);
    }
    internal static void DrawRaw(DrawingContext context,Sprite sprite,Rect draw)
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
    public static Sprite Frame(string id,int index) => Character(id=="momonga" ? Load("momonga-sprites","momonga-frames")[index] : Load(id+"-atlas",id+"-atlas-frames")[index],id,"atlas",index,index<24?index%8:index is >=44 and <56?2:-1);
    public static Sprite Idle(string id,int index) => Character(id=="momonga" ? Load("momonga-idle","momonga-idle-frames")[index] : Frame(id,32+index),id,"idle",index,index is 0 or 1?2:-1);
    public static Sprite Actor(string id,int index) => Character(id=="momonga" ? Load("momonga-scene-actors","momonga-scene-actor-frames")[index] : Frame(id,56+index),id,"scene",index,-1);
    private static readonly string[] drinkIds={"chiikawa","hachiware","usagi","kurimanju","shisa","kani","rakko","anoko","goblin","chiikabu","ode","rilakkuma","korilakkuma","mymelody","kuromi","dekatsuyo"};
    public static Sprite Drink(string id,int beat)
    {
        if(id=="momonga") return Character(Actor(id,6+beat),id,"drink",beat,2);
        var i=Array.IndexOf(drinkIds,id); return Character(Load("drinking-"+i/4,"drinking-"+i/4+"-frames")[i%4*2+beat],id,"drink",beat,2);
    }
    public static Sprite Meal(string id,string food,int beat)
    {
        var i=food switch {"furikake-rice"=>2,"curry-rice"=>4,"jiro-ramen"=>6,"nuts"=>8,"fruit"=>10,_=>0};
        return Character(Load(id+"-meals",id+"-meal-frames")[i+beat],id,"meal",i+beat,2);
    }
    public static Sprite Snack(string id,int index) => Character(id is "mymelody" or "kuromi" ? Load(id+"-meals",id+"-snack-frames")[index] : id=="momonga" ? Load("momonga-food-actions","momonga-food-actions-frames")[index] : Frame(id,64+index),id,"snack",index,2);
    public static Sprite Beer(double seconds) => Character(Load("kurimanju-beer","kurimanju-beer-frames",false)[seconds<.4?0:seconds<1.5?1:seconds<2.8?2:3],"kurimanju","beer",0,2);
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
    public double? MotionTime;
    private Sprite? sprite;
    public Sprite? Sprite { get=>sprite; set { sprite=value; InvalidateVisual(); } }
    public override void Render(DrawingContext context)
    {
        base.Render(context); if(sprite==null) return;
        var scale=Bounds.Width/240;
        var draw=new Rect(sprite.Draw.X*scale,sprite.Draw.Y*scale,sprite.Draw.Width*scale,sprite.Draw.Height*scale);
        Sprites.Draw(context,Sprites.Character(sprite,CharacterSprites.Current),draw,MotionTime);
    }
}

public sealed class CheekView : Control, IDisposable
{
    public Momonga.Input.CheekDrag Drag { get; set; } = new();
    public double Now { get; set; }
    private string cachedKey="";
    private byte[] pixels=Array.Empty<byte>();
    private Momonga.Character.LayeredFrame? model;
    private WriteableBitmap? output;
    public override void Render(DrawingContext context)
    {
        const int w=240,h=240;
        if(Bounds.Width<=0||Bounds.Height<=0)return;
        var id=CharacterSprites.Current;var expression=Drag.Expression(Now);var key=$"{id}:{expression}";
        if(key!=cachedKey)
        {
            cachedKey=key;Sprite body,face;
            if(id=="momonga")
            {
                var frames=Sprites.Load("momonga-affection","momonga-affection-frames",false);
                Sprite Center(Sprite sprite)
                {
                    var scale=240/Math.Max(sprite.Crop.Width,sprite.Crop.Height);var width=sprite.Crop.Width*scale;var height=sprite.Crop.Height*scale;
                    return sprite with {Draw=new Rect((240-width)/2,(240-height)/2,width,height)};
                }
                body=Sprites.Character(Center(frames[0]),id,"affection",0,2);
                face=Sprites.Character(Center(frames[expression]),id,"affection",expression,2);
            }
            else
            {
                var frame=id=="ode"?expression switch {4=>0,5=>1,6=>7,_=>expression}:id is "mymelody" or "kuromi"?expression switch {4=>0,5=>6,_=>expression}:expression;
                body=Sprites.Character(Sprites.Frame(id,44),id,"affection",0,2);
                face=Sprites.Character(Sprites.Frame(id,44+frame),id,"affection",frame,2);
            }
            model=CharacterLayers.ExpressionModel(body,face);pixels=model.Compose();
        }
        if(output?.PixelSize!=new PixelSize(w*2,h*2)){output?.Dispose();output=new WriteableBitmap(new PixelSize(w*2,h*2),new Vector(96,96),PixelFormat.Bgra8888,AlphaFormat.Premul);}
        var warped=Drag.Warp(pixels,w,h,model);
        using(var locked=output.Lock())for(var y=0;y<h*2;y++)System.Runtime.InteropServices.Marshal.Copy(warped,y*w*8,locked.Address+y*locked.RowBytes,w*8);
        context.DrawImage(output,new Rect(0,0,Bounds.Width,Bounds.Height));
    }
    public void Dispose()=>output?.Dispose();
}
