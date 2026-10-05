using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Momonga.Animation;
using Momonga.Character;
using Momonga.Input;

namespace Momonga.UI;

public static class RigPreview
{
    public static void Run()
    {
        Exception? failure=null;
        var thread=new Thread(()=>{try{Capture();}catch(Exception error){failure=error;}});
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        if(failure!=null)throw new InvalidOperationException("Rig preview failed",failure);
    }
    private static void Capture()
    {
        _=new App();LayeredFrame.RunChecks();CharacterLayers.RunChecks();Momonga.Simulation.LifeChecks.CheckCheek();
        var directory=Path.GetFullPath("obj/rig-preview");Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"timing.txt"),"");
        var filter=Environment.GetEnvironmentVariable("MOMONGA_PREVIEW_CHARACTER");
        var ids=CharacterDefinition.Ids.Where(id=>string.IsNullOrEmpty(filter)||id==filter).ToArray();var row=0;
        var actions=new DrawingVisual();var pulls=new DrawingVisual();var eating=new DrawingVisual();
        using(var ac=actions.RenderOpen())using(var pc=pulls.RenderOpen())using(var ec=eating.RenderOpen())
        {
            foreach(var id in ids)
            {
                CharacterSprites.Select(id);
                var atlas=new DrawingVisual();var atlasRows=id=="momonga"?4:10;using(var dc=atlas.RenderOpen())
                {
                    ImageSource[] originals=id=="momonga"?PetAnimator.LoadFrames("momonga-sprites","momonga-frames",32):CharacterSprites.Frames(id,0,80);
                    for(var index=0;index<originals.Length;index++)
                    {
                        var sprite=originals[index];
                        var fit=240/Math.Max(sprite.Width,sprite.Height);var visual=new DrawingVisual();
                        using(var raw=visual.RenderOpen())raw.DrawImage(sprite,new Rect((240-sprite.Width*fit)/2,(240-sprite.Height*fit)/2,sprite.Width*fit,sprite.Height*fit));
                        var bitmap=new RenderTargetBitmap(240,240,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
                        var original=new byte[240*240*4];bitmap.CopyPixels(original,960,0);var composed=CharacterLayers.Model(sprite).Compose();
                        for(var pixel=0;pixel<original.Length;pixel++)if(original[pixel]!=composed[pixel])throw new InvalidOperationException($"Atlas reconstruction changed pixels: {id}/{index}");
                        CharacterLayers.Draw(dc,sprite,new Rect(index%8*150,index/8*170+20,150,150));Label(dc,index.ToString(),new Point(index%8*150,index/8*170));
                    }
                }
                Save(atlas,1200,atlasRows*170,id+"-atlas-review");
                var landmarks="";
                foreach(var f in new[]{0,4,5,6})
                {var a=CharacterLayers.Model(ActionSprites.Frame(f)).Anatomy;landmarks+=$"{f}: eyes {a.LeftEye.Center} {a.RightEye.Center}; nose {a.Nose.Center}; mouth {a.Mouth.Center}\n";}
                File.WriteAllText(Path.Combine(directory,id+"-landmarks.txt"),landmarks);
                var bodySource=ActionSprites.CheekFrame(6);var bodyRaster=Raster(bodySource,240);var bodyPixels=new byte[240*240*4];bodyRaster.CopyPixels(bodyPixels,960,0);
                var probe=new CheekDrag();probe.Begin(CharacterAnatomy.Current.LeftCheek.Center,true);probe.Move(new Vector(-.2,0));var bodyWarp=probe.Warp(bodyPixels,240,240,CharacterLayers.Model(bodySource));
                var watch=System.Diagnostics.Stopwatch.StartNew();
                for(var repeat=0;repeat<3;repeat++)probe.Warp(bodyPixels,240,240,CharacterLayers.Model(bodySource));
                watch.Stop();File.AppendAllText(Path.Combine(directory,"timing.txt"),$"{id}: {watch.Elapsed.TotalMilliseconds/3:0.0} ms per 240px pull frame\n");
                var bodyPart=CharacterLayers.Model(bodySource).Canvas(CharacterPart.Body);
                for(var y=200;y<240;y++)for(var x=0;x<240;x++)
                {
                    var i=(y*240+x)*4;if(bodyPart[i+3]<245)continue;var dest=((y+120)*480+x+120)*4;
                    for(var ch=0;ch<3;ch++)if(Math.Abs(bodyPixels[i+ch]-bodyWarp[dest+ch])>3)throw new InvalidOperationException("Cheek drag moved the lower body: "+id);
                }
                for(var i=0;i<bodyWarp.Length;i+=4)for(var ch=0;ch<3;ch++)if(bodyWarp[i+ch]>bodyWarp[i+3])throw new InvalidOperationException("Cheek output exceeds alpha: "+id);
                var angles=new DrawingVisual();using(var dc=angles.RenderOpen())
                {
                    for(var side=0;side<2;side++)for(var angle=0;angle<8;angle++)
                    {
                        var index=side*8+angle;var x=index%4*340;var y=index/4*300;
                        var drag=new CheekDrag();var area=side==0?CharacterAnatomy.Current.LeftCheek:CharacterAnatomy.Current.RightCheek;
                        drag.Begin(area.Center,side==0);drag.Move(new Vector((side==0?-1:1)*.20*Math.Cos(angle*Math.PI/4),.20*Math.Sin(angle*Math.PI/4)));
                        var source=ActionSprites.CheekFrame(drag.Expression(0));var raster=Raster(source,240);var data=new byte[240*240*4];raster.CopyPixels(data,960,0);
                        var image=BitmapSource.Create(480,480,96,96,PixelFormats.Pbgra32,null,drag.Warp(data,240,240,CharacterLayers.Model(source)),1920);
                        dc.PushClip(new RectangleGeometry(new Rect(x,y,340,300)));dc.DrawImage(image,new Rect(x-60,y-90,480,480));dc.Pop();
                        Label(dc,$"{(side==0?"left":"right")} / {angle*45}°",new Point(x+8,y+280));
                    }
                }
                Save(angles,1360,1200,id+"-pulls");
                var layers=new DrawingVisual();using(var dc=layers.RenderOpen())
                {
                    var model=CharacterLayers.Model(ActionSprites.CheekFrame(6));var column=0;
                    foreach(var part in new[]{CharacterPart.Head,CharacterPart.LeftCheek,CharacterPart.RightCheek,CharacterPart.LeftEye,CharacterPart.RightEye,CharacterPart.LeftBlush,CharacterPart.RightBlush,CharacterPart.Mouth,CharacterPart.Body,CharacterPart.Accessory})
                    {
                        var data=model.Canvas(part);var image=BitmapSource.Create(240,240,96,96,PixelFormats.Pbgra32,null,data,960);
                        dc.DrawImage(image,new Rect(column%5*240,column/5*260+20,240,240));Label(dc,part.ToString(),new Point(column%5*240,column/5*260));column++;
                    }
                }
                Save(layers,1200,520,id+"-parts");
                var skinVisual=new DrawingVisual();using(var dc=skinVisual.RenderOpen())
                {
                    var model=CharacterLayers.Model(ActionSprites.CheekFrame(6));var data=new byte[240*240*4];
                    foreach(var part in model.Parts)
                        if(part.Part is not (CharacterPart.LeftEye or CharacterPart.RightEye or CharacterPart.Nose or CharacterPart.Mouth or CharacterPart.LeftBlush or CharacterPart.RightBlush or CharacterPart.Muzzle or CharacterPart.ForeheadMark or CharacterPart.LeftBrow or CharacterPart.RightBrow))
                            for(var y=0;y<part.Height;y++)for(var x=0;x<part.Width;x++)
                            {var src=(y*part.Width+x)*4;if(part.Pixels[src+3]>0)Buffer.BlockCopy(part.Pixels,src,data,((y+part.Y)*240+x+part.X)*4,4);}
                    dc.DrawImage(BitmapSource.Create(240,240,96,96,PixelFormats.Pbgra32,null,data,960),new Rect(0,0,720,720));
                }
                Save(skinVisual,720,720,id+"-skin");
                for(var frame=0;frame<12;frame++)
                    CharacterLayers.Draw(ac,ActionSprites.Frame(frame),new Rect(frame*160,row*180+20,160,160));
                for(var side=0;side<2;side++)for(var angle=0;angle<4;angle++)
                {
                    var drag=new CheekDrag();var area=side==0?CharacterAnatomy.Current.LeftCheek:CharacterAnatomy.Current.RightCheek;
                    drag.Begin(area.Center,side==0);drag.Move(new Vector((side==0?-1:1)*.20*Math.Abs(Math.Cos(angle*Math.PI/4)),.20*Math.Sin(angle*Math.PI/4)));
                    var source=ActionSprites.CheekFrame(drag.Expression(0));var raster=Raster(source,240);
                    var data=new byte[240*240*4];raster.CopyPixels(data,960,0);var pixels=drag.Warp(data,240,240,CharacterLayers.Model(source));
                    var image=BitmapSource.Create(480,480,96,96,PixelFormats.Pbgra32,null,pixels,1920);
                    pc.DrawImage(image,new Rect((side*4+angle)*240,row*180-40,240,240));
                }
                for(var beat=0;beat<2;beat++)
                {
                    var source=InteractionSprites.MealFrame("white-rice",beat);
                    CharacterLayers.Draw(ec,source,new Rect(beat*240,row*250+20,240,240),chewing:true,time:beat*.4+.12);
                    var water=InteractionSprites.ActorFrame(6+beat);
                    CharacterLayers.Draw(ec,water,new Rect((beat+2)*240,row*250+20,240,240),sipping:true,time:beat*.4+.12);
                }
                var meal=new DrawingVisual();using(var dc=meal.RenderOpen())
                {
                    for(var beat=0;beat<2;beat++)
                    {
                        CharacterLayers.Draw(dc,InteractionSprites.MealFrame("white-rice",beat),new Rect(beat*240,10,240,240),chewing:true,time:beat*.4+.12);
                        CharacterLayers.Draw(dc,InteractionSprites.ActorFrame(6+beat),new Rect((beat+2)*240,10,240,240),sipping:true,time:beat*.4+.12);
                    }
                }
                Save(meal,960,250,id+"-meal-water");
                Label(ac,id,new Point(0,row*180));Label(pc,id,new Point(0,row*180));Label(ec,id,new Point(0,row*250));row++;
            }
        }
        Save(actions,1920,row*180,"actions");Save(pulls,1920,row*180,"cheeks");Save(eating,960,row*250,"eating-drinking");
        CharacterSprites.Select("momonga");
        var detail=new DrawingVisual();using(var dc=detail.RenderOpen())
        {
            for(var i=0;i<4;i++)CharacterLayers.Draw(dc,ActionSprites.Frame(i),new Rect(i*240,0,240,240));
            for(var i=0;i<4;i++)CharacterLayers.Draw(dc,InteractionSprites.MealFrame("white-rice",i%2),new Rect(i*240,240,240,240),chewing:true,time:i*.1);
        }
        Save(detail,960,480,"momonga-detail");
        var cheekDetail=new DrawingVisual();using(var dc=cheekDetail.RenderOpen())
        {
            for(var i=0;i<4;i++)
            {
                var drag=new CheekDrag();drag.Begin(CharacterAnatomy.Current.LeftCheek.Center,true);
                drag.Move(new Vector(i<2?-.20:0,i%2==0?0:.20));
                var source=ActionSprites.CheekFrame(drag.Expression(0));var raster=Raster(source,240);
                var data=new byte[240*240*4];raster.CopyPixels(data,960,0);
                var image=BitmapSource.Create(480,480,96,96,PixelFormats.Pbgra32,null,drag.Warp(data,240,240,CharacterLayers.Model(source)),1920);
                dc.DrawImage(image,new Rect(i*360-60,-90,480,480));
            }
        }
        Save(cheekDetail,1440,300,"momonga-cheeks");
        var partsDetail=new DrawingVisual();using(var dc=partsDetail.RenderOpen())
        {
            var source=ActionSprites.CheekFrame(6);var model=CharacterLayers.Model(source);var i=0;
            foreach(var part in new[]{CharacterPart.Head,CharacterPart.LeftCheek,CharacterPart.LeftEye,CharacterPart.LeftBlush,CharacterPart.Body,CharacterPart.Accessory})
            {
                var data=model.Canvas(part);var image=BitmapSource.Create(240,240,96,96,PixelFormats.Pbgra32,null,data,960);
                dc.DrawImage(image,new Rect(i*240,20,240,240));Label(dc,part.ToString(),new Point(i*240,0));i++;
            }
        }
        Save(partsDetail,1440,260,"momonga-parts");
        var recovery=new DrawingVisual();using(var dc=recovery.RenderOpen())
        {
            var drag=new CheekDrag();drag.Begin(CharacterAnatomy.Current.LeftCheek.Center,true);drag.Move(new Vector(-.20,0));drag.Release(0);
            var times=new[]{0,.08,.18,.3,.5,.85};
            for(var i=0;i<times.Length;i++)
            {
                drag.Update(times[i]);var source=ActionSprites.CheekFrame(drag.Expression(times[i]));var raster=Raster(source,240);var data=new byte[240*240*4];raster.CopyPixels(data,960,0);
                var image=BitmapSource.Create(480,480,96,96,PixelFormats.Pbgra32,null,drag.Warp(data,240,240,CharacterLayers.Model(source)),1920);
                dc.PushClip(new RectangleGeometry(new Rect(i*340,0,340,300)));dc.DrawImage(image,new Rect(i*340-60,-90,480,480));dc.Pop();
                Label(dc,$"release {times[i]:0.00}s",new Point(i*340+8,280));
            }
        }
        Save(recovery,2040,300,"momonga-release");
        File.WriteAllText(Path.Combine(directory,"checks.txt"),"All 17 profiles: layer reconstruction, premultiplied alpha, native resting equality, expression hole checks and cheek input/release checks passed. Selected preview characters also passed byte-exact atlas reconstruction (80 frames each; Momonga 32), lower-body isolation and output-alpha checks. Preview uses current source compiled in memory; no executable build or desktop launch.");
        Application.Current.Shutdown();
        void Save(DrawingVisual visual,int width,int height,string name)
        {
            var background=new DrawingVisual();using(var dc=background.RenderOpen())dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(216,210,228)),null,new Rect(0,0,width,height));
            var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(background);bitmap.Render(visual);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file=File.Create(Path.Combine(directory,name+".png"));encoder.Save(file);
        }
    }
    private static void Label(DrawingContext dc,string text,Point at)=>dc.DrawText(new FormattedText(text,System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),14,Brushes.Gray,1),at);
    private static BitmapSource Raster(ImageSource source,int size)
    {
        var fit=size/Math.Max(source.Width,source.Height);var visual=new DrawingVisual();
        using(var dc=visual.RenderOpen())CharacterLayers.Draw(dc,source,new Rect((size-source.Width*fit)/2,(size-source.Height*fit)/2,source.Width*fit,source.Height*fit));
        var bitmap=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);return bitmap;
    }
}
