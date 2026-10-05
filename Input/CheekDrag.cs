using System;
using Momonga.Character;
#if CROSS_PLATFORM
using Avalonia;
#else
using System.Windows;
#endif

namespace Momonga.Input;

public sealed class CheekDrag
{
    public Point Grab { get; private set; }
    public Vector Pull { get; private set; }
    public bool Held { get; private set; }
    public bool Left { get; private set; }
    public double Strength { get; private set; }
    public double Radius { get; private set; }
    public static (double Left,double Right,double Y) Cheeks
    {
        get {var a=CharacterAnatomy.Current;return(a.LeftCheek.X,a.RightCheek.X,a.LeftCheek.Y);}
    }
    public static double CheekY => Cheeks.Y;
    private CharacterAnatomy anatomy=CharacterAnatomy.Current;
    private string anatomyId=Animation.CharacterSprites.Current;
    private LayeredFrame? cachedRig;
    private byte[]? cachedPixels;
    private Vector released;
    private double releasedAt = double.NegativeInfinity;
    private double inner;
    public bool Active(double now) => Held || now - releasedAt < .85;
    public int Expression(double now) => Held ? Strength < .08 ? 4 : Strength < .18 ? 5 : 6
        : now - releasedAt < .18 ? Strength < .08 ? 4 : Strength < .18 ? 5 : 6
        : now - releasedAt < .5 && Strength >= .18 ? 5 : 4;
    public void Begin(Point normalized, bool left)
    {
        anatomyId=Animation.CharacterSprites.Current;
        anatomy=CharacterAnatomy.For(anatomyId);
        cachedRig=null;cachedPixels=null;
        var cheekX = left ? anatomy.LeftCheek.X : anatomy.RightCheek.X;
        var x = Math.Clamp(normalized.X,cheekX-.04,cheekX+.04);
        Grab = new Point(x, Math.Clamp(normalized.Y, CheekY-.04,CheekY+.04));
        Left = left; Held = true; Pull = new Vector(); Strength = 0;
        inner=left ? Cheeks.Left+.12 : Cheeks.Right-.12;
        Radius = .07 + .05 * Math.Clamp((left ? normalized.X - .16 : .84 - normalized.X) / .24, 0, 1);
        releasedAt = double.NegativeInfinity;
    }
    public void Move(Vector normalized)
    {
        var inward = Left ? normalized.X : -normalized.X;
        var eye=Left?anatomy.LeftEye:anatomy.RightEye;
        var cheek=Left?anatomy.LeftCheek:anatomy.RightCheek;
        var inwardLimit=Math.Clamp((Math.Abs(eye.X-cheek.X)-eye.RadiusX)*.4,.008,.06);
        if(inward>0)normalized=new Vector((Left?1:-1)*inwardLimit*(1-Math.Exp(-inward/inwardLimit)),normalized.Y);
        var verticalLimit=Math.Min(.08,cheek.RadiusY*.65);
        normalized=new Vector(normalized.X,verticalLimit*Math.Tanh(normalized.Y/verticalLimit));
        var length = normalized.Length;
        Pull = length <= .24 ? normalized : normalized * ((.24 + .14 * (1 - Math.Exp(-(length - .24) / .14))) / length);
        Strength = Pull.Length;
    }
    public void Release(double now)
    {
        if (!Held) return;
        Held = false; released = Pull; releasedAt = Strength > .005 ? now : double.NegativeInfinity;
        if (!Active(now)) Pull = new Vector();
    }
    public void Update(double now)
    {
        if (Held) return;
        var t = Math.Max(0, now - releasedAt);
        Pull = t >= .65 ? new Vector() : released * (Math.Exp(-t * 10) * (Math.Cos(t * 24) + 10d / 24 * Math.Sin(t * 24)));
    }
    public byte[] Warp(byte[] pixels, int width, int height, LayeredFrame? model=null)
    {
        if(width<2||height<2||pixels.Length!=checked(width*height*4)||model!=null&&(model.Width!=width||model.Height!=height))throw new ArgumentException("Cheek raster and layers must share dimensions");
        var output=new byte[width*height*16];var ow=width*2;var oh=height*2;var px=width/2;var py=height/2;
        for(var y=0;y<height;y++)Buffer.BlockCopy(pixels,y*width*4,output,((y+py)*ow+px)*4,width*4);
        if(Pull.Length<.00001)return output;
        if(cachedRig==null||!ReferenceEquals(cachedPixels,pixels)||cachedRig.Width!=width||cachedRig.Height!=height)
        {
            cachedPixels=pixels;
            cachedRig=model??LayeredFrame.Import(anatomyId,pixels,width,height,new Rect(0,0,1,1),new CharacterPose(anatomyId,"affection",Expression(0),2));
        }
        var rig=cachedRig!;
        var skin=new byte[width*height*4];
        foreach(var part in rig.Parts)
            for(var y=0;y<part.Height;y++)for(var x=0;x<part.Width;x++)
            {
                var src=(y*part.Width+x)*4;if(part.Pixels[src+3]==0)continue;
                var xx=x+part.X;var yy=y+part.Y;var dst=(yy*width+xx)*4;
                if(!Anchored(part.Part)&&!Background(part.Part))Buffer.BlockCopy(part.Pixels,src,skin,dst,4);
            }
        Array.Clear(output);pixels=skin;
        foreach(var part in rig.Parts)if(Background(part.Part))
            for(var y=0;y<part.Height;y++)for(var x=0;x<part.Width;x++)
            {
                var src=(y*part.Width+x)*4;if(part.Pixels[src+3]==0)continue;
                Buffer.BlockCopy(part.Pixels,src,output,((y+part.Y+py)*ow+x+part.X+px)*4,4);
            }
        if(anatomyId is "mymelody" or "kuromi")
        {
            var channels=new System.Collections.Generic.List<int>[3];for(var ch=0;ch<3;ch++)channels[ch]=new();
            foreach(var part in rig.Parts)if(part.Part==CharacterPart.Accessory)
                for(var i=0;i<part.Pixels.Length;i+=4)
                {
                    var a=part.Pixels[i+3];if(a<245)continue;var b=part.Pixels[i];var g=part.Pixels[i+1];var r=part.Pixels[i+2];
                    if(anatomyId=="mymelody"?r>g+20&&b>g*.85:r+g+b<240)
                        for(var ch=0;ch<3;ch++)channels[ch].Add(part.Pixels[i+ch]*255/a);
                }
            var tone=new int[3];for(var ch=0;ch<3;ch++){channels[ch].Sort();tone[ch]=channels[ch].Count==0?40:channels[ch][channels[ch].Count/2];}
            for(var y=0;y<height;y++)for(var x=0;x<width;x++)
            {
                if(y/(double)height<anatomy.LeftEye.Y-.09||y/(double)height>anatomy.Mouth.Y+.07)continue;
                var at=(y*width+x)*4;if(skin[at+3]==0)continue;var dest=((y+py)*ow+x+px)*4;
                for(var ch=0;ch<3;ch++)output[dest+ch]=(byte)(tone[ch]*skin[at+3]/255);output[dest+3]=skin[at+3];
            }
        }
        // Backtrace a smooth velocity field: forward triangles can fold over each other.
        // Each destination pixel samples the skin exactly once, so folds cannot paint stripes.
        for(var y=0;y<oh;y++)for(var x=0;x<ow;x++)
        {
            var point=new Point((x+.5-px)/width,(y+.5-py)/height);
            const int steps=40;
            var affected=point.X>Math.Min(Grab.X,Grab.X+Pull.X)-.276&&point.X<Math.Max(Grab.X,Grab.X+Pull.X)+.276&&point.Y>Math.Min(Grab.Y,Grab.Y+Pull.Y)-Radius*1.8&&point.Y<Math.Max(Grab.Y,Grab.Y+Pull.Y)+Radius*1.8;
            for(var step=affected?steps-1:-1;step>=0;step--)
            {
                var center=Grab+Pull*((step+.5)/steps);
                var dx=(point.X-center.X)/.23;var dy=(point.Y-center.Y)/(Radius*1.5);
                var fade=Math.Clamp((Math.Sqrt(dx*dx+dy*dy)-.35)/.85,0,1);
                var across=Math.Clamp((point.X-inner)/(Grab.X-inner),0,1);
                var weight=1d;
                var top=Math.Clamp((point.Y-(anatomy.LeftCheek.Y-.15))/.10,0,1);
                var bottom=Math.Clamp((Math.Min(anatomy.LeftCheek.Y+.18,anatomy.Mouth.Y+.10)-point.Y)/.10,0,1);
                weight=weight*weight*(3-2*weight)*across*across*(3-2*across)*(1-fade*fade*(3-2*fade))*top*top*(3-2*top)*bottom*bottom*(3-2*bottom);
                point-=Pull*(weight/steps);
            }
            var sampleX=point.X*width-.5;var sampleY=point.Y*height-.5;
            if(sampleX<0||sampleY<0||sampleX>=width-1||sampleY>=height-1)continue;
            var xx=(int)sampleX;var yy=(int)sampleY;var fx2=sampleX-xx;var fy2=sampleY-yy;var offset=(yy*width+xx)*4;var dest=(y*ow+x)*4;
            for(var ch=0;ch<4;ch++)
                {
                var value=(pixels[offset+ch]*(1-fx2)+pixels[offset+4+ch]*fx2)*(1-fy2)+(pixels[offset+width*4+ch]*(1-fx2)+pixels[offset+width*4+4+ch]*fx2)*fy2;
                var alpha=(pixels[offset+3]*(1-fx2)+pixels[offset+7]*fx2)*(1-fy2)+(pixels[offset+width*4+3]*(1-fx2)+pixels[offset+width*4+7]*fx2)*fy2;
                output[dest+ch]=(byte)Math.Clamp(value+output[dest+ch]*(1-alpha/255),0,255);
                }
        }
        // Facial features are drawn last at their own anchored positions.
        foreach(var part in rig.Parts)
        {
            if(!Anchored(part.Part))continue;
            for(var y=0;y<part.Height;y++)for(var x=0;x<part.Width;x++)
            {
                var src=(y*part.Width+x)*4;var dest=((y+part.Y+py)*ow+x+part.X+px)*4;var alpha=part.Pixels[src+3]/255d;
                for(var ch=0;ch<4;ch++)output[dest+ch]=(byte)Math.Min(255,part.Pixels[src+ch]+output[dest+ch]*(1-alpha));
            }
        }
        return output;
        bool Background(CharacterPart part)=>part==CharacterPart.Tail||part==CharacterPart.Accessory&&anatomyId is "mymelody" or "kuromi";
        bool Anchored(CharacterPart part) => part is CharacterPart.LeftEye or CharacterPart.RightEye or CharacterPart.Nose or CharacterPart.Mouth or CharacterPart.LeftBlush or CharacterPart.RightBlush or CharacterPart.Muzzle or CharacterPart.ForeheadMark or CharacterPart.LeftBrow or CharacterPart.RightBrow;
    }
}
