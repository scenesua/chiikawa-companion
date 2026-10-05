using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Momonga.Animation;
using Momonga.Character;
using Momonga.Input;

namespace Momonga.UI;

public sealed class LayeredSprite : FrameworkElement
{
    public static readonly DependencyProperty SourceProperty=DependencyProperty.Register(nameof(Source),typeof(ImageSource),typeof(LayeredSprite),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public ImageSource? Source { get => (ImageSource?)GetValue(SourceProperty); set => SetValue(SourceProperty,value); }
    public Stretch Stretch { get; set; }=Stretch.Uniform;
    public bool IsCharacter { get; set; }=true;
    public bool Chewing { get; set; }
    public bool Sipping { get; set; }
    public double? MotionTime { get; set; }
    public CharacterPart? OnlyPart { get; set; }
    protected override void OnRender(DrawingContext dc)
    {
        if(Source==null||ActualWidth<=0||ActualHeight<=0)return;
        var scale=Math.Min(ActualWidth/Source.Width,ActualHeight/Source.Height);
        var rect=new Rect((ActualWidth-Source.Width*scale)/2,(ActualHeight-Source.Height*scale)/2,Source.Width*scale,Source.Height*scale);
        if(!IsCharacter){dc.DrawImage(Source,rect);return;}
        CharacterLayers.Draw(dc,Source,rect,OnlyPart,Chewing,Sipping,MotionTime);
    }
}

public static class CharacterLayers
{
    private sealed record Imported(LayeredFrame Frame,Dictionary<CharacterPart,BitmapSource> Images,BitmapSource Composite);
    private static readonly Dictionary<(ImageSource,string),Imported> cache=new();
    private static readonly Queue<(ImageSource,string)> order=new();
    private static readonly ConditionalWeakTable<ImageSource,CharacterPose> poses=new();
    private static readonly ConditionalWeakTable<ImageSource,LayeredFrame> expressionModels=new();
    private static readonly Dictionary<(ImageSource,ImageSource,string),ImageSource> expressions=new();
    public static void Register(ImageSource source,string id,string clip,int frame,int facing)
    {
        var pose=new CharacterPose(id,clip,frame,facing);
        if(poses.TryGetValue(source,out var old)&&old==pose)return;
        poses.Remove(source);poses.Add(source,pose);cache.Remove((source,id));
    }
    private static Imported Import(ImageSource source)
    {
        var id=poses.TryGetValue(source,out var registered)?registered.CharacterId:CharacterSprites.Current;var key=(source,id);
        if(cache.TryGetValue(key,out var existing))return existing;
        const int width=240,height=240;
        var fit=240/Math.Max(source.Width,source.Height);
        var sourceRect=new Rect((240-source.Width*fit)/2,(240-source.Height*fit)/2,source.Width*fit,source.Height*fit);
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawImage(source,sourceRect);
        var raster=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);raster.Render(visual);
        var pixels=new byte[width*height*4];raster.CopyPixels(pixels,width*4,0);
        var bounds=new Rect(0,0,1,1);
        var model=expressionModels.TryGetValue(source,out var preserved)?preserved:LayeredFrame.Import(id,pixels,width,height,bounds,poses.TryGetValue(source,out var pose)?pose:null);
        var images=new Dictionary<CharacterPart,BitmapSource>();
        foreach(var part in model.Parts)
        {var image=BitmapSource.Create(part.Width,part.Height,96,96,PixelFormats.Pbgra32,null,part.Pixels,part.Width*4);image.Freeze();images[part.Part]=image;}
        var composite=BitmapSource.Create(width,height,96,96,PixelFormats.Pbgra32,null,model.Compose(),width*4);composite.Freeze();
        var result=new Imported(model,images,composite);cache.Add(key,result);order.Enqueue(key);
        // ponytail: retain 64 imported poses; raise the bound if measured animation churn warrants it.
        while(order.Count>64)cache.Remove(order.Dequeue());
        return result;
    }
    public static LayeredFrame Model(ImageSource source)=>Import(source).Frame;
    public static void RunChecks()
    {
        var previous=CharacterSprites.Current;
        try
        {
            foreach(var id in CharacterDefinition.Ids)
            {
                CharacterSprites.Select(id);var source=ActionSprites.Frame(0);
                var fit=240/Math.Max(source.Width,source.Height);
                var rect=new Rect((240-source.Width*fit)/2,(240-source.Height*fit)/2,source.Width*fit,source.Height*fit);
                var raw=new DrawingVisual();using(var dc=raw.RenderOpen())dc.DrawImage(source,rect);
                var original=new RenderTargetBitmap(240,240,96,96,PixelFormats.Pbgra32);original.Render(raw);
                var originalPixels=new byte[240*240*4];original.CopyPixels(originalPixels,960,0);
                foreach(var expressionFrame in new[]{4,5,6})
                {
                    var mixed=Expression(source,ActionSprites.Frame(expressionFrame));var rendered=Model(mixed).Compose();
                    for(var i=0;i<rendered.Length;i+=4)
                    {
                        if(originalPixels[i+3]>=245&&rendered[i+3]<240)throw new InvalidOperationException("Expression left a transparent hole: "+id+" / "+expressionFrame);
                        for(var ch=0;ch<3;ch++)if(rendered[i+ch]>rendered[i+3])throw new InvalidOperationException("Expression color exceeds alpha: "+id);
                    }
                }
                foreach(var size in new[]{128,240})
                {
                    var expected=new DrawingVisual();using(var dc=expected.RenderOpen())dc.DrawImage(original,new Rect(0,0,size,size));
                    var actual=new DrawingVisual();using(var dc=actual.RenderOpen())Draw(dc,source,new Rect(rect.X*size/240,rect.Y*size/240,rect.Width*size/240,rect.Height*size/240));
                    var a=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);a.Render(expected);
                    var b=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);b.Render(actual);
                    var ap=new byte[size*size*4];var bp=new byte[ap.Length];a.CopyPixels(ap,size*4,0);b.CopyPixels(bp,size*4,0);
                    for(var i=0;i<ap.Length;i++)if(ap[i]!=bp[i])throw new InvalidOperationException("Layer rendering changed resting pixels: "+id+" at "+size);
                }
            }
        }
        finally{CharacterSprites.Select(previous);}
    }
    public static CharacterPose? Pose(ImageSource? source)=>source!=null&&poses.TryGetValue(source,out var pose)?pose:null;
    public static ImageSource Expression(ImageSource body,ImageSource face)
    {
        var key=(body,face,CharacterSprites.Current);
        if(expressions.TryGetValue(key,out var image))return image;
        var baseline=Import(body);var expression=Import(face);
        if(!baseline.Frame.TryExpression(expression.Frame,out var combined))return body;
        var result=BitmapSource.Create(combined.Width,combined.Height,96,96,PixelFormats.Pbgra32,null,combined.Compose(),combined.Width*4);result.Freeze();
        Register(result,combined.Pose.CharacterId,combined.Pose.Clip,expression.Frame.Pose.Frame,combined.Pose.Facing);
        expressionModels.Add(result,combined);expressions[key]=result;return result;
    }
    public static void Draw(DrawingContext dc,ImageSource source,Rect target,CharacterPart? only=null,bool chewing=false,bool sipping=false,double? time=null)
    {
        var rig=Import(source);
        var fit=240/Math.Max(source.Width,source.Height);var cw=source.Width*fit;var ch=source.Height*fit;
        var sx=target.Width/cw;var sy=target.Height/ch;
        target=new Rect(target.X-(240-cw)*sx/2,target.Y-(240-ch)*sy/2,240*sx,240*sy);
        if(!only.HasValue&&!chewing&&!sipping){dc.DrawImage(rig.Composite,target);return;}
        // Recombine at source resolution before scaling. Filtering each cut-out
        // independently exposes backing paint along the artificial part boundaries.
        var visual=new DrawingVisual();
        using(var layerContext=visual.RenderOpen()) DrawParts(layerContext);
        var composed=new RenderTargetBitmap(240,240,96,96,PixelFormats.Pbgra32);composed.Render(visual);
        dc.DrawImage(composed,target);
        void DrawParts(DrawingContext layerContext)
        {
        foreach(var layer in rig.Frame.Parts)
            if(!only.HasValue||only==layer.Part)
            {
                var seconds=time??Environment.TickCount64/1000d;
                var motion=chewing?PartMotion.Chew(layer.Part,seconds):sipping?PartMotion.Sip(layer.Part,seconds):PartMotion.Still;
                var w=layer.Width;var h=layer.Height;
                layerContext.DrawImage(rig.Images[layer.Part],new Rect(layer.X+motion.X*240+w*(1-motion.ScaleX)/2,layer.Y+motion.Y*240+h*(1-motion.ScaleY)/2,w*motion.ScaleX,h*motion.ScaleY));
            }
        }
    }
}
