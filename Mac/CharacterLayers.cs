using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Momonga.Character;
using System.Runtime.InteropServices;

namespace Momonga.Animation;

public static class CharacterLayers
{
    private sealed record Imported(LayeredFrame Frame,Dictionary<CharacterPart,WriteableBitmap> Images,WriteableBitmap Composite);
    private static readonly Dictionary<Sprite,Imported> cache=new();
    private static readonly LinkedList<Sprite> order=new();
    private static readonly Dictionary<Sprite,LinkedListNode<Sprite>> nodes=new();
    private sealed class Portrait : Control
    {
        public required Sprite Sprite;
        public override void Render(DrawingContext context)=>Sprites.DrawRaw(context,Sprite,Sprite.Draw);
    }
    private static Imported Import(Sprite sprite)
    {
        if(cache.TryGetValue(sprite,out var existing)){order.Remove(nodes[sprite]);order.AddLast(nodes[sprite]);return existing;}
        var pose=sprite.Pose??new CharacterPose(CharacterSprites.Current,"unregistered",0,-1);
        var w=Math.Max(240,(int)Math.Ceiling(sprite.Draw.Right));var h=Math.Max(240,(int)Math.Ceiling(sprite.Draw.Bottom));
        var portrait=new Portrait {Sprite=sprite,Width=w,Height=h};portrait.Measure(new Size(w,h));portrait.Arrange(new Rect(0,0,w,h));
        using var raster=new RenderTargetBitmap(new PixelSize(w,h));raster.Render(portrait);
        using var bitmap=new WriteableBitmap(new PixelSize(w,h),new Vector(96,96),PixelFormat.Bgra8888,AlphaFormat.Premul);
        var pixels=new byte[w*h*4];
        using(var locked=bitmap.Lock())
        {raster.CopyPixels(locked,AlphaFormat.Premul);for(var y=0;y<h;y++)Marshal.Copy(locked.Address+y*locked.RowBytes,pixels,y*w*4,w*4);}
        var model=LayeredFrame.Import(pose.CharacterId,pixels,w,h,new Rect(0,0,w/240d,h/240d),pose);
        var images=new Dictionary<CharacterPart,WriteableBitmap>();
        foreach(var part in model.Parts)
        {
            var image=new WriteableBitmap(new PixelSize(part.Width,part.Height),new Vector(96,96),PixelFormat.Bgra8888,AlphaFormat.Premul);
            using(var locked=image.Lock())for(var y=0;y<part.Height;y++)Marshal.Copy(part.Pixels,y*part.Width*4,locked.Address+y*locked.RowBytes,part.Width*4);
            images[part.Part]=image;
        }
        var result=new Imported(model,images,Bitmap(model.Width,model.Height,model.Compose()));cache[sprite]=result;nodes[sprite]=order.AddLast(sprite);
        // Match the Windows import-cache ceiling; dispose native bitmaps when a pose leaves it.
        while(order.Count>64)
        {
            var key=order.First!.Value;order.RemoveFirst();nodes.Remove(key);
            if(cache.Remove(key,out var old)){foreach(var image in old.Images.Values)image.Dispose();old.Composite.Dispose();}
        }
        return result;
    }
    public static LayeredFrame Model(Sprite sprite)=>Import(sprite).Frame;
    private static WriteableBitmap Bitmap(int width,int height,byte[] pixels)
    {
        var bitmap=new WriteableBitmap(new PixelSize(width,height),new Vector(96,96),PixelFormat.Bgra8888,AlphaFormat.Premul);
        using(var locked=bitmap.Lock())for(var y=0;y<height;y++)Marshal.Copy(pixels,y*width*4,locked.Address+y*locked.RowBytes,width*4);
        return bitmap;
    }
    private sealed class PartsPortrait : Control
    {
        public required Imported Rig;
        public CharacterPart? Only;
        public bool Chewing,Sipping;
        public double? Time;
        public override void Render(DrawingContext context)
        {
            var seconds=Time??Environment.TickCount64/1000d;
            foreach(var part in Rig.Frame.Parts)
            {
                if(Only.HasValue&&Only!=part.Part)continue;
                var motion=Chewing?PartMotion.Chew(part.Part,seconds):Sipping?PartMotion.Sip(part.Part,seconds):PartMotion.Still;
                var w=part.Width;var h=part.Height;
                context.DrawImage(Rig.Images[part.Part],new Rect(part.X+motion.X*Rig.Frame.Width+w*(1-motion.ScaleX)/2,part.Y+motion.Y*Rig.Frame.Height+h*(1-motion.ScaleY)/2,w*motion.ScaleX,h*motion.ScaleY));
            }
        }
    }
    public static LayeredFrame ExpressionModel(Sprite body,Sprite face)
    {
        var baseline=Model(body);return baseline.TryExpression(Model(face),out var combined)?combined:baseline;
    }
    public static void Draw(DrawingContext context,Sprite sprite,Rect target,CharacterPart? only=null,bool chewing=false,double? time=null)
    {
        var rig=Import(sprite);var sx=target.Width/sprite.Draw.Width;var sy=target.Height/sprite.Draw.Height;
        target=new Rect(target.X-sprite.Draw.X*sx,target.Y-sprite.Draw.Y*sy,rig.Frame.Width*sx,rig.Frame.Height*sy);
        var sipping=sprite.Pose?.Clip is "drink" or "beer";
        if(!only.HasValue&&!chewing&&!sipping){context.DrawImage(rig.Composite,target);return;}
        var portrait=new PartsPortrait {Rig=rig,Only=only,Chewing=chewing,Sipping=sipping,Time=time,Width=rig.Frame.Width,Height=rig.Frame.Height};
        portrait.Measure(new Size(rig.Frame.Width,rig.Frame.Height));portrait.Arrange(new Rect(0,0,rig.Frame.Width,rig.Frame.Height));
        using var raster=new RenderTargetBitmap(new PixelSize(rig.Frame.Width,rig.Frame.Height));raster.Render(portrait);
        context.DrawImage(raster,target);
    }
}
