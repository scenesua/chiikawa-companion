using System;
using System.Collections.Generic;
using Momonga.Input;
#if CROSS_PLATFORM
using Avalonia;
#else
using System.Windows;
#endif

namespace Momonga.Character;

public enum CharacterPart { Tail, Body, Head, LeftEar, RightEar, LeftEye, RightEye, Nose, Mouth, LeftCheek, RightCheek, Chin, Accessory, LeftBlush, RightBlush, Muzzle, ForeheadMark, LeftBrow, RightBrow }

public sealed record SpritePart(CharacterPart Part, int X, int Y, int Width, int Height, byte[] Pixels, bool Overlay=false);
public sealed record CharacterPose(string CharacterId, string Clip, int Frame, int Facing)
{
    public bool CanUseExpression(CharacterPose expression) => (Facing is >=0 and <=4)&&Facing==expression.Facing&&CharacterId==expression.CharacterId&&Clip==expression.Clip;
}

// Legacy atlases are import sources only. Rendering and interactions consume these
// independent cropped layers, with all origins retained in the same character canvas.
public sealed class LayeredFrame
{
    public int Width { get; }
    public int Height { get; }
    public string CharacterId { get; }
    public IReadOnlyList<SpritePart> Parts { get; }
    public CharacterPose Pose { get; }
    public CharacterAnatomy Anatomy { get; }
    private LayeredFrame(string id,int width,int height,List<SpritePart> parts,CharacterPose pose,CharacterAnatomy anatomy)
    {CharacterId=id;Width=width;Height=height;Parts=parts;Pose=pose;Anatomy=anatomy;}

    public static LayeredFrame Import(string id,byte[] pixels,int width,int height,Rect portraitBounds,CharacterPose? pose=null)
    {
        if(width<=0||height<=0||pixels.Length!=checked(width*height*4))throw new ArgumentException("Invalid character raster");
        if(pose!=null&&pose.CharacterId!=id)throw new ArgumentException("Pose belongs to another character");
        var anatomy=CharacterAnatomy.For(id).Fit(pixels,width,height,portraitBounds,pose);
        var outline=new bool[width*height];
        var features=ConnectedFeatures();
        var palette=new Dictionary<int,int>();
        for(var y=0;y<height;y++)for(var x=0;x<width;x++)
        {
            var i=(y*width+x)*4;var a=pixels[i+3];if(a<245)continue;
            var point=new Point(portraitBounds.X+x*portraitBounds.Width/width,portraitBounds.Y+y*portraitBounds.Height/height);
            if(!anatomy.Area("HeadOutline").Contains(point))continue;
            var key=(pixels[i]*255/a/16)<<8|(pixels[i+1]*255/a/16)<<4|pixels[i+2]*255/a/16;
            palette[key]=palette.TryGetValue(key,out var count)?count+1:1;
        }
        var dominant=0;var largest=0;foreach(var pair in palette)if(pair.Value>largest){dominant=pair.Key;largest=pair.Value;}
        var fillB=((dominant>>8)&15)*16+8;var fillG=((dominant>>4)&15)*16+8;var fillR=(dominant&15)*16+8;
        if(id is "mymelody" or "kuromi")fillB=fillG=fillR=248;
        var faceLeft=new int[height];var faceRight=new int[height];Array.Fill(faceLeft,width);Array.Fill(faceRight,-1);
        if(id is "mymelody" or "kuromi"&&pose?.Facing==2)
            for(var y=0;y<height;y++)for(var x=0;x<width;x++)
            {
                var q=(y*width+x)*4;var a=pixels[q+3];var nx=portraitBounds.X+x*portraitBounds.Width/width;
                if(Math.Abs(nx-anatomy.Nose.X)>.27||a<200)continue;
                if(pixels[q]>a*.86&&pixels[q+1]>a*.86&&pixels[q+2]>a*.86){faceLeft[y]=Math.Min(faceLeft[y],x);faceRight[y]=Math.Max(faceRight[y],x);}
            }
        var buffers=new byte[Enum.GetValues<CharacterPart>().Length][];
        for(var i=0;i<buffers.Length;i++)buffers[i]=new byte[pixels.Length];
        for(var y=0;y<height;y++)for(var x=0;x<width;x++)
        {
            var offset=(y*width+x)*4;if(pixels[offset+3]==0)continue;
            var point=new Point(portraitBounds.X+x*portraitBounds.Width/width,portraitBounds.Y+y*portraitBounds.Height/height);
            var part=Classify(anatomy,point,pose?.Facing??-1);
            if(id is "rilakkuma" or "korilakkuma" && anatomy.Area("Muzzle").Contains(point)&&pixels[offset]>pixels[offset+3]*.65&&pixels[offset+1]>pixels[offset+3]*.65&&pixels[offset+2]>pixels[offset+3]*.65)part=CharacterPart.Muzzle;
            if(features[y*width+x]>=0)part=(CharacterPart)features[y*width+x];
            if(part is CharacterPart.LeftEye or CharacterPart.RightEye or CharacterPart.Nose or CharacterPart.Mouth)
            {
                var a=pixels[offset+3];
                var contrast=Math.Max(Math.Abs(pixels[offset]*255d/a-fillB),Math.Max(Math.Abs(pixels[offset+1]*255d/a-fillG),Math.Abs(pixels[offset+2]*255d/a-fillR)));
                if(contrast<22||!Interior(x,y)||outline[y*width+x])part=CharacterPart.Head;
            }
            if(id=="dekatsuyo"&&part is CharacterPart.Head or CharacterPart.LeftCheek or CharacterPart.RightCheek or CharacterPart.Chin)
            {
                var a=pixels[offset+3];var white=pixels[offset]>a*.65&&pixels[offset+1]>a*.65&&pixels[offset+2]>a*.65;
                if(Math.Abs(point.X-anatomy.Nose.X)<.23&&point.Y>anatomy.LeftEye.Y-.055&&point.Y<anatomy.Mouth.Y+.09&&(white||NearWhite(x,y)))part=CharacterPart.Muzzle;
            }
            if(id=="anoko"&&point.Y<anatomy.LeftEye.Y-.045&&anatomy.InHeadSkin(point)&&pixels[offset+2]>pixels[offset]+25&&pixels[offset+1]>pixels[offset]+12&&Interior(x,y))part=CharacterPart.ForeheadMark;
            if(id=="ode")
            {
                if(part is CharacterPart.LeftEye or CharacterPart.RightEye or CharacterPart.Nose or CharacterPart.Mouth or CharacterPart.LeftBrow or CharacterPart.RightBrow)part=CharacterPart.Head;
                if(Interior(x,y))
                {
                    if(Math.Abs(point.X-anatomy.LeftEye.X)<.105&&point.Y>anatomy.LeftEye.Y-.095&&point.Y<anatomy.LeftEye.Y+(pose?.Frame==0?.065:.03))part=CharacterPart.LeftEye;
                    else if(Math.Abs(point.X-anatomy.Mouth.X)<(pose?.Frame==0?.24:.19)&&point.Y>=anatomy.LeftEye.Y+(pose?.Frame==0?.04:.03)&&point.Y<anatomy.LeftEye.Y+(pose?.Frame==7?.105:pose?.Frame==0?.165:.175))part=CharacterPart.Mouth;
                }
            }
            if(id=="momonga"&&anatomy.Nose.Distance(point)<2&&pixels[offset]>pixels[offset+2]+8&&pixels[offset+1]>pixels[offset+2]+4)part=CharacterPart.Nose;
            if(anatomy.HasTail&&anatomy.Area("Tail").Contains(point)&&pixels[offset]>pixels[offset+2]+8&&pixels[offset+1]>pixels[offset+2]+4)part=CharacterPart.Tail;
            if(id is "mymelody" or "kuromi" && part is CharacterPart.Head or CharacterPart.LeftCheek or CharacterPart.RightCheek)
            {
                var alpha=pixels[offset+3];var b=pixels[offset];var g=pixels[offset+1];var r=pixels[offset+2];
                var pink=r>g+15*alpha/255d&&b>g*.85;
                var clothing=id=="mymelody"?(r>g+60*alpha/255d&&b<g*.7):r+g+b<400*alpha/255d;
                if((id=="mymelody"&&pink)||(clothing&&!NearWhite(x,y)))part=CharacterPart.Accessory;
                // A continuous face opening keeps mixed hood/skin edge pixels together.
                // Per-pixel color classification otherwise leaves isolated white edge fragments.
                if(pose?.Facing==2&&point.Y>anatomy.LeftEye.Y-.10&&point.Y<anatomy.Mouth.Y+.035)
                {
                    if(faceRight[y]>=faceLeft[y])part=x>=faceLeft[y]-2&&x<=faceRight[y]+2?CharacterPart.Head:CharacterPart.Accessory;
                }
            }
            if(id is "mymelody" or "kuromi"&&anatomy.Nose.Contains(point)&&point.Y>(anatomy.Nose.Y+anatomy.Mouth.Y)/2)part=CharacterPart.Mouth;
            Buffer.BlockCopy(pixels,offset,buffers[(int)part],offset,4);
        }
        bool Interior(int x,int y)
        {
            var radius=Math.Max(3,width/60);
            for(var yy=Math.Max(0,y-radius);yy<=Math.Min(height-1,y+radius);yy++)for(var xx=Math.Max(0,x-radius);xx<=Math.Min(width-1,x+radius);xx++)
                if(pixels[(yy*width+xx)*4+3]<200)return false;
            return true;
        }
        bool NearWhite(int x,int y)
        {
            var radius=Math.Max(2,width/(id is "mymelody" or "kuromi"?24:48));
            for(var yy=Math.Max(0,y-radius);yy<=Math.Min(height-1,y+radius);yy++)for(var xx=Math.Max(0,x-radius);xx<=Math.Min(width-1,x+radius);xx++)
            {var i=(yy*width+xx)*4;var a=pixels[i+3];if(a>200&&pixels[i]>a*.8&&pixels[i+1]>a*.8&&pixels[i+2]>a*.8)return true;}
            return false;
        }
        int[] ConnectedFeatures()
        {
            var owners=new int[width*height];Array.Fill(owners,-1);
            if(pose?.Facing is 5 or 6 or 7)return owners;
            var visited=new bool[owners.Length];
            for(var at=0;at<owners.Length;at++)
            {
                if(visited[at]||!Dark(at))continue;
                var queue=new Queue<int>();var points=new List<int>();queue.Enqueue(at);visited[at]=true;
                double cx=0,cy=0;var left=width;var right=0;var top=height;var bottom=0;
                while(queue.Count>0)
                {
                    var p=queue.Dequeue();points.Add(p);var x=p%width;var y=p/width;cx+=x;cy+=y;
                    left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
                    if(x>0)Visit(p-1);if(x+1<width)Visit(p+1);if(y>0)Visit(p-width);if(y+1<height)Visit(p+width);
                    void Visit(int next){if(visited[next]||!Dark(next))return;visited[next]=true;queue.Enqueue(next);}
                }
                var exterior=false;
                foreach(var p in points)
                {
                    var xx=p%width;var yy=p/width;
                    for(var y=Math.Max(0,yy-2);y<=Math.Min(height-1,yy+2)&&!exterior;y++)for(var x=Math.Max(0,xx-2);x<=Math.Min(width-1,xx+2);x++)
                        if(pixels[(y*width+x)*4+3]<100){exterior=true;break;}
                    if(exterior)break;
                }
                if(exterior)foreach(var p in points)outline[p]=true;
                if(exterior||points.Count<2||points.Count>width*height*.03||right-left>width*(id=="ode"?.3:.18)||bottom-top>height*.23)continue;
                var center=new Point(portraitBounds.X+cx/points.Count*portraitBounds.Width/width,portraitBounds.Y+cy/points.Count*portraitBounds.Height/height);
                var l=anatomy.LeftEye.Distance(center);var r=anatomy.RightEye.Distance(center);
                var cheek=Math.Min(anatomy.LeftCheek.Distance(center),anatomy.RightCheek.Distance(center));
                var mouth=anatomy.Mouth.Distance(center);var nose=anatomy.Nose.Distance(center);
                var browLeft=Math.Abs(center.X-anatomy.LeftEye.X)<.09&&center.Y<anatomy.LeftEye.Y-.025&&center.Y>anatomy.LeftEye.Y-(id is "mymelody" or "kuromi"?.07:.16);
                var browRight=Math.Abs(center.X-anatomy.RightEye.X)<.09&&center.Y<anatomy.RightEye.Y-.025&&center.Y>anatomy.RightEye.Y-(id is "mymelody" or "kuromi"?.07:.16);
                var owner=browLeft?CharacterPart.LeftBrow:browRight?CharacterPart.RightBrow:nose<Math.Min(Math.Min(l,r),mouth)&&nose<1.3?CharacterPart.Nose:mouth<Math.Min(l,r)&&mouth<1.3?CharacterPart.Mouth:Math.Min(l,r)<1.5&&cheek>Math.Min(l,r)?l<r?CharacterPart.LeftEye:CharacterPart.RightEye:(CharacterPart?)null;
                if(id is "mymelody" or "kuromi"&&owner is CharacterPart.LeftEye or CharacterPart.RightEye)
                {
                    var eye=owner==CharacterPart.LeftEye?anatomy.LeftEye:anatomy.RightEye;
                    if(center.Y>eye.Y+.025)owner=null;
                }
                if(owner.HasValue)for(var y=top;y<=bottom;y++)for(var x=left;x<=right;x++)owners[y*width+x]=(int)owner.Value;
            }
            return owners;
            bool Dark(int p){p*=4;var a=pixels[p+3];var limit=id=="mymelody"?.75:.5;return a>160&&pixels[p]<a*limit&&pixels[p+1]<a*limit&&pixels[p+2]<a*limit;}
        }
        var tones=new List<double>();
        for(var i=0;i<pixels.Length;i+=4)
            if(buffers[(int)CharacterPart.Head][i+3]>200&&pixels[i]+pixels[i+1]+pixels[i+2]>pixels[i+3]*2)
                tones.Add(Math.Max(0,(pixels[i+2]-pixels[i+1])*255d/pixels[i+3]));
        tones.Sort();var blushContrast=24+(tones.Count==0?0:tones[tones.Count/2]);
        // Blush and its enclosed hatch marks are facial features, not stretchable skin.
        if(id is not ("mymelody" or "kuromi" or "rilakkuma" or "korilakkuma" or "ode"))
        {ExtractBlush(CharacterPart.LeftCheek,CharacterPart.LeftBlush);ExtractBlush(CharacterPart.RightCheek,CharacterPart.RightBlush);}
        // Keep antialiasing around each decal with that decal. Leaving its pale edge
        // in the skin creates a ghost eye/mouth after an expression or cheek movement.
        foreach(var feature in new[]{CharacterPart.LeftEye,CharacterPart.RightEye,CharacterPart.Nose,CharacterPart.Mouth,CharacterPart.LeftBlush,CharacterPart.RightBlush,CharacterPart.ForeheadMark,CharacterPart.LeftBrow,CharacterPart.RightBrow})
        {
            var layer=buffers[(int)feature];var seed=(byte[])layer.Clone();
            var padding=id is "mymelody" or "kuromi" or "ode"?4:2;
            for(var y=padding;y<height-padding;y++)for(var x=padding;x<width-padding;x++)
            {
                if(id=="ode"&&feature==CharacterPart.LeftEye&&y/(double)height>anatomy.LeftEye.Y+(pose?.Frame==0?.065:.03))continue;
                if(feature==CharacterPart.Nose&&y/(double)height>(anatomy.Nose.Y+anatomy.Mouth.Y)/2)continue;
                if(feature==CharacterPart.Mouth&&y/(double)height<(anatomy.Nose.Y+anatomy.Mouth.Y)/2)continue;
                var i=(y*width+x)*4;if(layer[i+3]>0||outline[y*width+x]||!Interior(x,y))continue;
                if(buffers[(int)CharacterPart.Head][i+3]==0&&buffers[(int)CharacterPart.LeftCheek][i+3]==0&&buffers[(int)CharacterPart.RightCheek][i+3]==0&&buffers[(int)CharacterPart.Chin][i+3]==0)continue;
                var nearby=false;
                for(var yy=y-padding;yy<=y+padding&&!nearby;yy++)for(var xx=x-padding;xx<=x+padding;xx++)if(seed[(yy*width+xx)*4+3]>200){nearby=true;break;}
                if(!nearby)continue;
                Buffer.BlockCopy(pixels,i,layer,i,4);
                foreach(var part in new[]{CharacterPart.Head,CharacterPart.LeftCheek,CharacterPart.RightCheek,CharacterPart.Chin})Array.Clear(buffers[(int)part],i,4);
            }
        }
        // Opaque backing belongs to the skin, including near-opaque atlas pixels.
        // Use the measured face tone; feature pixels must never seed their own backing.
        var head=buffers[(int)CharacterPart.Head];
        var red=new List<int>();var green=new List<int>();var blue=new List<int>();
        for(var i=0;i<head.Length;i+=4)if(head[i+3]>=245&&head[i]+head[i+1]+head[i+2]>head[i+3])
        {blue.Add(head[i]*255/head[i+3]);green.Add(head[i+1]*255/head[i+3]);red.Add(head[i+2]*255/head[i+3]);}
        red.Sort();green.Sort();blue.Sort();
        var skinR=red.Count>0?red[red.Count/2]:255;var skinG=green.Count>0?green[green.Count/2]:255;var skinB=blue.Count>0?blue[blue.Count/2]:255;
        foreach(var feature in facialParts)
        {
            var layer=buffers[(int)feature];
            for(var y=0;y<height;y++)for(var x=0;x<width;x++)
            {
                var i=(y*width+x)*4;if(layer[i+3]==0)continue;
                var point=new Point(portraitBounds.X+x*portraitBounds.Width/width,portraitBounds.Y+y*portraitBounds.Height/height);
                if(pixels[i+3]>0)
                {
                    head[i+3]=pixels[i+3]>=200?(byte)255:pixels[i+3];
                    head[i]=(byte)(skinB*head[i+3]/255);head[i+1]=(byte)(skinG*head[i+3]/255);head[i+2]=(byte)(skinR*head[i+3]/255);
                }
            }
        }
        if(id is "rilakkuma" or "korilakkuma")
        {
            var muzzle=buffers[(int)CharacterPart.Muzzle];var values=new List<int>[3];for(var ch=0;ch<3;ch++)values[ch]=new();
            for(var i=0;i<muzzle.Length;i+=4)if(muzzle[i+3]>=245)for(var ch=0;ch<3;ch++)values[ch].Add(muzzle[i+ch]*255/muzzle[i+3]);
            var tone=new int[3];for(var ch=0;ch<3;ch++){values[ch].Sort();tone[ch]=values[ch].Count==0?250:values[ch][values[ch].Count/2];}
            foreach(var feature in new[]{CharacterPart.Nose,CharacterPart.Mouth})
                for(var y=0;y<height;y++)for(var x=0;x<width;x++)
                {
                    var i=(y*width+x)*4;if(buffers[(int)feature][i+3]==0)continue;
                    var point=new Point(portraitBounds.X+x*portraitBounds.Width/width,portraitBounds.Y+y*portraitBounds.Height/height);
                    if(!anatomy.Area("Muzzle").Contains(point))continue;
                    var alpha=pixels[i+3]>=200?255:pixels[i+3];muzzle[i+3]=(byte)alpha;
                    for(var ch=0;ch<3;ch++)muzzle[i+ch]=(byte)(tone[ch]*alpha/255);
                }
        }
        void ExtractBlush(CharacterPart cheek,CharacterPart blush)
        {
            var layer=buffers[(int)cheek];var area=cheek==CharacterPart.LeftCheek?anatomy.LeftCheek:anatomy.RightCheek;var left=width;var right=-1;var top=height;var bottom=-1;
            for(var y=0;y<height;y++)for(var x=0;x<width;x++)
            {
                var i=(y*width+x)*4;var a=pixels[i+3];if(a<128||(layer[i+3]==0&&buffers[(int)CharacterPart.Head][i+3]==0))continue;
                var point=new Point(portraitBounds.X+x*portraitBounds.Width/width,portraitBounds.Y+y*portraitBounds.Height/height);if(area.Distance(point)>1.6)continue;
                var b=pixels[i];var g=pixels[i+1];var r=pixels[i+2];
                if(r>g+blushContrast*a/255d&&r>b+2*a/255d&&b>g*.85&&g>a*.55&&r>a*.7&&b>a*.5)
                {left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
            }
            if(right<left)return;
            var cx=(left+right)/2d;var cy=(top+bottom)/2d;var rx=(right-left+3)/2d;var ry=(bottom-top+3)/2d;
            for(var y=Math.Max(0,top-1);y<=Math.Min(height-1,bottom+1);y++)for(var x=Math.Max(0,left-1);x<=Math.Min(width-1,right+1);x++)
            {
                var i=(y*width+x)*4;if((layer[i+3]==0&&buffers[(int)CharacterPart.Head][i+3]==0)||!Interior(x,y)||outline[y*width+x])continue;
                var a=pixels[i+3];var pink=pixels[i+2]>pixels[i+1]+blushContrast*a/255d&&pixels[i+2]>pixels[i]+2*a/255d&&pixels[i]>pixels[i+1]*.85&&pixels[i+1]>a*.55&&pixels[i+2]>a*.7&&pixels[i]>a*.5;
                if(Math.Pow((x-cx)/rx,2)+Math.Pow((y-cy)/ry,2)>1&&!pink)continue;
                Buffer.BlockCopy(pixels,i,buffers[(int)blush],i,4);Array.Clear(layer,i,4);Array.Clear(buffers[(int)CharacterPart.Head],i,4);
            }
        }
        var parts=new List<SpritePart>();
        for(var index=0;index<buffers.Length;index++)
        {
            var buffer=buffers[index];var left=width;var top=height;var right=-1;var bottom=-1;
            for(var y=0;y<height;y++)for(var x=0;x<width;x++)
                if(buffer[(y*width+x)*4+3]>0){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
            if(right<left)continue;
            var w=right-left+1;var h=bottom-top+1;var crop=new byte[w*h*4];
            for(var y=0;y<h;y++)Buffer.BlockCopy(buffer,((top+y)*width+left)*4,crop,y*w*4,w*4);
            parts.Add(new SpritePart((CharacterPart)index,left,top,w,h,crop));
        }
        parts.Sort((a,b)=>(IsExpression(a.Part)?100+(int)a.Part:(int)a.Part).CompareTo(IsExpression(b.Part)?100+(int)b.Part:(int)b.Part));
        return new LayeredFrame(id,width,height,parts,pose??new CharacterPose(id,"unregistered",0,-1),anatomy);
    }
    private static readonly CharacterPart[] facialParts={CharacterPart.LeftEye,CharacterPart.RightEye,CharacterPart.Nose,CharacterPart.Mouth,CharacterPart.LeftCheek,CharacterPart.RightCheek,CharacterPart.Chin,CharacterPart.LeftBlush,CharacterPart.RightBlush,CharacterPart.Muzzle,CharacterPart.ForeheadMark,CharacterPart.LeftBrow,CharacterPart.RightBrow};
    private static CharacterPart Classify(CharacterAnatomy anatomy,Point point,int facing)
    {
        // Features belong to the face, never to the head-outline layer.
        if(facing is not (5 or 6 or 7))
        {
            if(anatomy.LeftEye.Contains(point))return CharacterPart.LeftEye;
            if(anatomy.RightEye.Contains(point))return CharacterPart.RightEye;
            if(anatomy.Nose.Contains(point))return CharacterPart.Nose;
            if(anatomy.Mouth.Contains(point))return CharacterPart.Mouth;
            if(anatomy.LeftCheek.Contains(point))return CharacterPart.LeftCheek;
            if(anatomy.RightCheek.Contains(point))return CharacterPart.RightCheek;
        }
        if(anatomy.Area("Chin").Contains(point))return CharacterPart.Chin;
        if(anatomy.LeftEar.Contains(point))return CharacterPart.LeftEar;
        if(anatomy.RightEar.Contains(point))return CharacterPart.RightEar;
        if(anatomy.InHeadSkin(point))return CharacterPart.Head;
        if(anatomy.Body.Contains(point))return CharacterPart.Body;
        if(anatomy.HasTail&&anatomy.Area("Tail").Contains(point))return CharacterPart.Tail;
        // Props, clothing and protruding limbs retain their original pixels independently.
        return CharacterPart.Accessory;
    }
    public byte[] Canvas(CharacterPart part)
    {
        var result=new byte[Width*Height*4];
        foreach(var layer in Parts)if(layer.Part==part)
            for(var y=0;y<layer.Height;y++)Buffer.BlockCopy(layer.Pixels,y*layer.Width*4,result,((layer.Y+y)*Width+layer.X)*4,layer.Width*4);
        return result;
    }
    public byte[] Compose()
    {
        var result=new byte[Width*Height*4];
        foreach(var layer in Parts)
            for(var y=0;y<layer.Height;y++)for(var x=0;x<layer.Width;x++)
            {
                var src=(y*layer.Width+x)*4;if(layer.Pixels[src+3]==0)continue;
                var dest=((y+layer.Y)*Width+x+layer.X)*4;
                if(!layer.Overlay)Buffer.BlockCopy(layer.Pixels,src,result,dest,4);
                else
                {
                    var alpha=layer.Pixels[src+3]/255d;
                    for(var ch=0;ch<4;ch++)result[dest+ch]=(byte)Math.Min(255,layer.Pixels[src+ch]+result[dest+ch]*(1-alpha));
                }
            }
        return result;
    }
    public bool TryExpression(LayeredFrame expression,out LayeredFrame result)
    {
        result=this;
        if(!Pose.CanUseExpression(expression.Pose)||Width!=expression.Width||Height!=expression.Height)return false;
        var tones=new List<int>[3];for(var ch=0;ch<3;ch++)tones[ch]=new();
        foreach(var layer in expression.Parts)if(layer.Part==CharacterPart.Head)
            for(var i=0;i<layer.Pixels.Length;i+=4)if(layer.Pixels[i+3]>=245&&layer.Pixels[i]+layer.Pixels[i+1]+layer.Pixels[i+2]>layer.Pixels[i+3]*1.5)
                for(var ch=0;ch<3;ch++)tones[ch].Add(layer.Pixels[i+ch]*255/layer.Pixels[i+3]);
        var tone=new int[3];for(var ch=0;ch<3;ch++){tones[ch].Sort();tone[ch]=tones[ch].Count==0?255:tones[ch][tones[ch].Count/2];}
        var parts=new List<SpritePart>();
        foreach(var part in Parts)if(!IsExpression(part.Part))parts.Add(part);
        foreach(var part in expression.Parts)if(IsExpression(part.Part))
        {
            var landmark=part.Part==CharacterPart.LeftBrow?"LeftEye":part.Part==CharacterPart.RightBrow?"RightEye":part.Part.ToString();
            var from=expression.Anatomy.Area(landmark);var to=Anatomy.Area(landmark);
            var scale=Math.Clamp((Anatomy.RightEye.X-Anatomy.LeftEye.X)/Math.Max(.04,expression.Anatomy.RightEye.X-expression.Anatomy.LeftEye.X),.8,1.25);
            if(Anatomy.RightEye.X-Anatomy.LeftEye.X<.04)scale=1;
            var x=(int)Math.Round(to.X*Width+(part.X-from.X*Width)*scale);var y=(int)Math.Round(to.Y*Height+(part.Y-from.Y*Height)*scale);
            var w=Math.Max(1,(int)Math.Round(part.Width*scale));var h=Math.Max(1,(int)Math.Round(part.Height*scale));var data=new byte[w*h*4];
            for(var yy=0;yy<h;yy++)for(var xx=0;xx<w;xx++)
            {
                var sx=Math.Clamp((xx+.5)/scale-.5,0,part.Width-1);var sy=Math.Clamp((yy+.5)/scale-.5,0,part.Height-1);
                var ix=(int)sx;var iy=(int)sy;var fx=sx-ix;var fy=sy-iy;var ix2=Math.Min(ix+1,part.Width-1);var iy2=Math.Min(iy+1,part.Height-1);
                for(var ch=0;ch<4;ch++)data[(yy*w+xx)*4+ch]=(byte)((part.Pixels[(iy*part.Width+ix)*4+ch]*(1-fx)+part.Pixels[(iy*part.Width+ix2)*4+ch]*fx)*(1-fy)+(part.Pixels[(iy2*part.Width+ix)*4+ch]*(1-fx)+part.Pixels[(iy2*part.Width+ix2)*4+ch]*fx)*fy);
            }
            // Padding is useful for exact source reconstruction, but its skin tone
            // must not travel with a decal onto a differently shaded neutral face.
            for(var i=0;i<data.Length;i+=4)
            {
                var alpha=data[i+3];if(alpha==0)continue;
                if(CharacterId is "mymelody" or "kuromi"&&expression.Pose.Frame==6)
                {
                    var yy=(y+i/4/w+.5)/Height;
                    var vertical=part.Part is CharacterPart.LeftEye or CharacterPart.RightEye?.028:part.Part==CharacterPart.Mouth?.016:.025;
                    if(Math.Abs(yy-to.Y)>vertical){Array.Clear(data,i,4);continue;}
                }
                var contrast=0d;
                for(var ch=0;ch<3;ch++)contrast=Math.Max(contrast,Math.Abs(data[i+ch]*255d/alpha-tone[ch]));
                var opacity=Math.Clamp((contrast-16)/16,0,1);
                for(var ch=0;ch<4;ch++)data[i+ch]=(byte)(data[i+ch]*opacity);
            }
            // The transformed decal stays inside the common portrait canvas.
            if(x>=0&&y>=0&&x+w<=Width&&y+h<=Height)parts.Add(new SpritePart(part.Part,x,y,w,h,data,true));
        }

        result=new LayeredFrame(CharacterId,Width,Height,parts,Pose,Anatomy);return true;
    }
    private static bool IsExpression(CharacterPart part) => part is CharacterPart.LeftEye or CharacterPart.RightEye or CharacterPart.Nose or CharacterPart.Mouth or CharacterPart.LeftBrow or CharacterPart.RightBrow;
    public static void RunChecks()
    {
        foreach(var id in CharacterDefinition.Ids)
        {
            var pixels=new byte[96*96*4];
            for(var y=0;y<96;y++)for(var x=0;x<96;x++)
            {var i=(y*96+x)*4;var alpha=(byte)((x+y)%7==0?0:(x+y)%7==1?160:255);pixels[i]=(byte)(alpha*.9);pixels[i+1]=(byte)(alpha*.85);pixels[i+2]=(byte)(alpha*.8);pixels[i+3]=alpha;}
            var frame=Import(id,pixels,96,96,new Rect(0,0,1,1));var restored=frame.Compose();
            foreach(var layer in frame.Parts)for(var i=0;i<layer.Pixels.Length;i+=4)
                for(var ch=0;ch<3;ch++)if(layer.Pixels[i+ch]>layer.Pixels[i+3])throw new InvalidOperationException("Invalid premultiplied layer: "+id);
            for(var i=0;i<pixels.Length;i++)if(pixels[i]!=restored[i])throw new InvalidOperationException("Layer import altered pixels: "+id);
            var anatomy=CharacterAnatomy.For(id);
            if(id is not ("mymelody" or "kuromi" or "rilakkuma" or "korilakkuma" or "ode"))
            {
                var matte=new byte[96*96*4];
                for(var i=0;i<matte.Length;i+=4){matte[i]=matte[i+1]=matte[i+2]=254;matte[i+3]=254;}
                var cx=(int)(anatomy.LeftCheek.X*96);var cy=(int)(anatomy.LeftCheek.Y*96);
                for(var y=cy-2;y<=cy+2;y++)for(var x=cx-2;x<=cx+2;x++)
                {var i=(y*96+x)*4;matte[i]=180;matte[i+1]=150;matte[i+2]=254;}
                var split=Import(id,matte,96,96,new Rect(0,0,1,1));var at=(cy*96+cx)*4;
                if(split.Canvas(CharacterPart.LeftBlush)[at+3]==0||split.Canvas(CharacterPart.Head)[at+3]!=255)
                    throw new InvalidOperationException("Near-opaque blush left a skin hole: "+id);
                var composed=split.Compose();for(var i=0;i<matte.Length;i++)if(matte[i]!=composed[i])throw new InvalidOperationException("Blush extraction changed resting pixels: "+id);
            }
            if(anatomy.Region(anatomy.Area("LeftEye").Center)!=HitRegion.Face||anatomy.Region(anatomy.LeftCheek.Center)!=HitRegion.LeftCheek)
                throw new InvalidOperationException("Head/face landmarks overlap incorrectly: "+id);
            if(anatomy.Region(anatomy.LeftCheek.Center,false)==HitRegion.LeftCheek)
                throw new InvalidOperationException("Back of head accepted cheek input");
            for(var facing=0;facing<8;facing++)
            for(var expression=0;expression<8;expression++)
                if(new CharacterPose(id,"affection",0,facing).CanUseExpression(new CharacterPose(id,"affection",0,expression))!=(facing<=4&&facing==expression))
                    throw new InvalidOperationException("Expression facing mismatch: "+id);
            for(var step=0;step<12;step++)
            {
                var seconds=step*.037;
                if(PartMotion.Chew(CharacterPart.Body,seconds)!=PartMotion.Still||PartMotion.Chew(CharacterPart.Head,seconds)!=PartMotion.Still)
                    throw new InvalidOperationException("Chewing moved the body/head");
                if(PartMotion.Sip(CharacterPart.LeftCheek,seconds)!=PartMotion.Still)
                    throw new InvalidOperationException("Drinking chewed a cheek");
            }
            foreach(var part in Enum.GetValues<CharacterPart>())
                if(PartMotion.Chew(part,0)!=PartMotion.Still||PartMotion.Sip(part,0)!=PartMotion.Still)
                    throw new InvalidOperationException("Eating motion does not start at rest");
            if(PartMotion.Chew(CharacterPart.Mouth,.1)!=PartMotion.Still||PartMotion.Chew(CharacterPart.Mouth,.45)==PartMotion.Still)
                throw new InvalidOperationException("Chewing was not confined to the bite frame");
            if(new CharacterPose(id,"walk",0,-1).CanUseExpression(new CharacterPose(id,"reaction",0,2)))
                throw new InvalidOperationException("Unknown pose accepted a frontal expression");
        }
    }
}
