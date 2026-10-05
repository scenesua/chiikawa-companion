using System;
using System.Collections.Generic;
using System.Text.Json;
#if CROSS_PLATFORM
using Avalonia;
#else
using System.Windows;
#endif

namespace Momonga.Input;

public readonly record struct AnatomyArea(double X, double Y, double RadiusX, double RadiusY)
{
    public Point Center => new(X,Y);
    public double Distance(Point point) => Math.Sqrt(Math.Pow((point.X-X)/RadiusX,2)+Math.Pow((point.Y-Y)/RadiusY,2));
    public bool Contains(Point point) => Distance(point)<=1;
    public double Mask(Point point) => Math.Clamp((1-Distance(point))/.12,0,1);
}

// Anatomy refers to the fitted front-facing action portrait. Atlas cells and other poses
// need their own landmarks; do not treat these coordinates as a skeleton for every frame.
public sealed class CharacterAnatomy
{
    private static readonly Dictionary<string,Dictionary<string,double[]>> profiles=Load();
    private static readonly Dictionary<string,CharacterAnatomy> loaded=new();
    private static readonly Dictionary<string,CharacterAnatomy> affectionProfiles=new();
    private readonly Dictionary<string,double[]> areas;
    private CharacterAnatomy(Dictionary<string,double[]> areas)
    {
        this.areas=areas;
        LeftEye=Area("LeftEye");RightEye=Area("RightEye");Nose=Area("Nose");Mouth=Area("Mouth");
        LeftEar=Area("LeftEar");RightEar=Area("RightEar");
    }
    public AnatomyArea LeftEye { get; }
    public AnatomyArea RightEye { get; }
    public AnatomyArea Nose { get; }
    public AnatomyArea Mouth { get; }
    public AnatomyArea LeftEar { get; }
    public AnatomyArea RightEar { get; }
    public static CharacterAnatomy Current => For(Animation.CharacterSprites.Current);
    public static CharacterAnatomy For(string id)
    {
        if(loaded.TryGetValue(id,out var existing))return existing;
        return loaded[id]=new CharacterAnatomy(profiles.TryGetValue(id,out var value)?value:throw new ArgumentException("Missing character anatomy",nameof(id)));
    }
    public CharacterAnatomy Fit(byte[] pixels,int width,int height,Rect bounds,Character.CharacterPose? pose)
    {
        if(pose?.Facing!=2||pose.Clip is not ("affection" or "meal" or "drink" or "snack" or "beer"))return this;
        if(pose.CharacterId=="ode"&&pose.Clip=="affection")
        {
            var eye=pose.Frame switch {0=>new Point(.488,.362),1=>new Point(.500,.414),7=>new Point(.502,.408),_=>LeftEye.Center};
            var shifted=new Dictionary<string,double[]>();
            foreach(var pair in areas){var a=pair.Value;shifted[pair.Key]=pair.Key is "Body" or "Tail"?(double[])a.Clone():new[]{a[0]+eye.X-LeftEye.X,a[1]+eye.Y-LeftEye.Y,a[2],a[3]};}
            shifted["Mouth"][1]=pose.Frame switch {0=>.467,1=>.522,7=>.499,_=>shifted["Mouth"][1]};
            return new CharacterAnatomy(shifted);
        }
        if(pose.Clip=="affection"&&pose.Frame==0&&affectionProfiles.TryGetValue(pose.CharacterId,out var neutral))return neutral;
        var seen=new bool[width*height];var candidates=new List<(Point Center,int Count)>();
        for(var at=0;at<seen.Length;at++)
        {
            if(seen[at]||!Dark(at))continue;
            var queue=new Queue<int>();queue.Enqueue(at);seen[at]=true;var count=0;double cx=0,cy=0;
            var left=width;var right=0;var top=height;var bottom=0;
            while(queue.Count>0)
            {
                var next=queue.Dequeue();var x=next%width;var y=next/width;count++;cx+=x;cy+=y;
                left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
                if(x>0)Visit(next-1);if(x+1<width)Visit(next+1);if(y>0)Visit(next-width);if(y+1<height)Visit(next+width);
                void Visit(int p){if(seen[p]||!Dark(p))return;seen[p]=true;queue.Enqueue(p);}
            }
            if(count<(pose.CharacterId is "ode" or "mymelody" or "kuromi"?5:Math.Max(5,width*height*.00035))||count>width*height*.03||right-left>width*(pose.CharacterId=="ode"?.3:.18)||bottom-top>height*.23)continue;
            if(pose.CharacterId is "mymelody" or "kuromi")
            {
                if(bounds.Y+cy/count*bounds.Height/height<LeftEye.Y-.05)continue;
                var white=0;var samples=0;var radius=Math.Max(8,width/24);var mx=(int)(cx/count);var my=(int)(cy/count);
                for(var y=Math.Max(0,my-radius);y<=Math.Min(height-1,my+radius);y++)for(var x=Math.Max(0,mx-radius);x<=Math.Min(width-1,mx+radius);x++)
                {var i=(y*width+x)*4;var a=pixels[i+3];samples++;if(a>200&&pixels[i]>a*.85&&pixels[i+1]>a*.85&&pixels[i+2]>a*.85)white++;}
                if(white<samples*.4)continue;
            }
            candidates.Add((new Point(bounds.X+cx/count*bounds.Width/width,bounds.Y+cy/count*bounds.Height/height),count));
        }
        var center=(LeftEye.X+RightEye.X)/2;var eyeY=(LeftEye.Y+RightEye.Y)/2;var distance=RightEye.X-LeftEye.X;
        if(distance<.04)
        {
            var bestEye=double.PositiveInfinity;var eye=LeftEye.Center;
            var largeEye=pose.CharacterId=="ode"&&candidates.Exists(c=>c.Count>=200);
            foreach(var candidate in candidates)
            {
                if(pose.CharacterId=="ode"&&(largeEye?candidate.Count<200:candidate.Center.Y>eyeY+.015))continue;
                var score=Math.Abs(candidate.Center.X-center)*2+Math.Abs(candidate.Center.Y-eyeY);if(score<bestEye){bestEye=score;eye=candidate.Center;}}
            if(bestEye>.4)return this;
            var single=new Dictionary<string,double[]>();
            foreach(var pair in areas){var a=pair.Value;single[pair.Key]=pair.Key is "Body" or "Tail"?(double[])a.Clone():new[]{a[0]+eye.X-center,a[1]+eye.Y-eyeY,a[2],a[3]};}
            var fittedEye=new CharacterAnatomy(single);if(pose.Clip=="affection"&&pose.Frame==0)affectionProfiles[pose.CharacterId]=fittedEye;return fittedEye;
        }
        var best=double.PositiveInfinity;Point first=default,second=default;
        foreach(var a in candidates)foreach(var b in candidates)
        {
            var dx=b.Center.X-a.Center.X;var dy=Math.Abs(a.Center.Y-b.Center.Y);var scale=dx/distance;
            if(scale<.75||scale>1.4||dy>(pose.CharacterId=="kuromi"?.025:.06)||Math.Abs((a.Center.X+b.Center.X)/2-center)>.14||Math.Abs((a.Center.Y+b.Center.Y)/2-eyeY)>.22)continue;
            if(Math.Max(a.Count,b.Count)>Math.Min(a.Count,b.Count)*2.5)continue;
            var score=Math.Abs((a.Center.X+b.Center.X)/2-center)+Math.Abs((a.Center.Y+b.Center.Y)/2-eyeY)*.6+dy*1.5+Math.Abs(scale-1)*.05+(pose.CharacterId is "mymelody" or "kuromi"?.1:2)/Math.Sqrt(a.Count*(double)b.Count);
            if(score<best){best=score;first=a.Center;second=b.Center;}
        }
        if(!double.IsFinite(best))return this;
        var s=Math.Clamp((second.X-first.X)/distance,.8,1.2);var x0=(first.X+second.X)/2;var y0=(first.Y+second.Y)/2;
        var fitted=new Dictionary<string,double[]>();
        foreach(var pair in areas)
        {
            var a=pair.Value;
            var scale=pair.Key is "HeadOutline" or "LeftEar" or "RightEar"?1:s;
            fitted[pair.Key]=pair.Key is "Body" or "Tail"?(double[])a.Clone():new[]{x0+(a[0]-center)*scale,y0+(a[1]-eyeY)*scale,a[2]*scale,a[3]*scale};
        }
        if(pose.Clip=="affection"&&pose.Frame==6&&pose.CharacterId is "mymelody" or "kuromi")
        {
            var mouth=pose.CharacterId=="mymelody"?new Point(.6032,.7126):new Point(.4843,.7391);
            fitted["Mouth"][0]=mouth.X;fitted["Mouth"][1]=mouth.Y;
        }
        fitted["LeftEye"][0]=first.X;fitted["LeftEye"][1]=first.Y;fitted["RightEye"][0]=second.X;fitted["RightEye"][1]=second.Y;
        if(pose.Clip=="affection")
        {
            foreach(var name in new[]{"LeftEye","RightEye"}){fitted[name][2]=Math.Max(fitted[name][2],.06);fitted[name][3]=Math.Max(fitted[name][3],.055);}
            fitted["Mouth"][2]=Math.Max(fitted["Mouth"][2],.045);fitted["Mouth"][3]=Math.Max(fitted["Mouth"][3],.03);
            var result=new CharacterAnatomy(fitted);if(pose.Frame==0)affectionProfiles[pose.CharacterId]=result;return result;
        }
        return new CharacterAnatomy(fitted);
        bool Dark(int i){i*=4;var alpha=pixels[i+3];var limit=pose.CharacterId=="mymelody"?.75:.5;return alpha>180&&pixels[i]<alpha*limit&&pixels[i+1]<alpha*limit&&pixels[i+2]<alpha*limit;}
    }
    public AnatomyArea Area(string name)
    {
        var a=areas[name];return new(a[0],a[1],a[2],a[3]);
    }
    public AnatomyArea HeadOutline => Area("HeadOutline");
    public bool InHeadSkin(Point point) => Math.Abs(point.X-HeadOutline.X)<=HeadOutline.RadiusX&&Math.Abs(point.Y-HeadOutline.Y)<=HeadOutline.RadiusY;
    public AnatomyArea Body => Area("Body");
    public AnatomyArea LeftCheek => Area("LeftCheek");
    public AnatomyArea RightCheek => Area("RightCheek");
    public bool HasTail => areas.ContainsKey("Tail");
    public bool InHead(Point point) => HeadOutline.Contains(point)||LeftEar.Contains(point)||RightEar.Contains(point);
    public double ExpressionMask(Point point) => Math.Max(Math.Max(LeftEye.Mask(point),RightEye.Mask(point)),Math.Max(Nose.Mask(point),Mouth.Mask(point)));
    public bool IsFaceFeature(Point point) => ExpressionMask(point)>0||LeftCheek.Contains(point)||RightCheek.Contains(point)||Area("Chin").Contains(point);
    public HitRegion Region(Point point,bool faceVisible=true)
    {
        if(faceVisible)
        {
            if(ExpressionMask(point)>0)return HitRegion.Face;
            if(LeftCheek.Contains(point))return HitRegion.LeftCheek;
            if(RightCheek.Contains(point))return HitRegion.RightCheek;
            if(Area("Chin").Contains(point))return HitRegion.Chin;
        }
        if(InHead(point))return HitRegion.Head;
        if(Body.Contains(point))return HitRegion.Body;
        if(HasTail&&Area("Tail").Contains(point))return HitRegion.Tail;
        return HitRegion.Outside;
    }
    private static Dictionary<string,Dictionary<string,double[]>> Load()
    {
        using var resource=Content.ContentResource.Open("Content/character-anatomy.json");
        var result=JsonSerializer.Deserialize<Dictionary<string,Dictionary<string,double[]>>>(resource)??throw new InvalidOperationException("Missing anatomy profiles");
        foreach(var id in Character.CharacterDefinition.Ids)
        {
            if(!result.TryGetValue(id,out var profile))throw new InvalidOperationException("Missing anatomy: "+id);
            foreach(var name in new[]{"HeadOutline","Body","LeftEye","RightEye","Nose","Mouth","LeftCheek","RightCheek","Chin","LeftEar","RightEar"})
                if(!profile.ContainsKey(name))throw new InvalidOperationException("Missing anatomy area: "+id+"/"+name);
            foreach(var area in profile.Values)
            {
                if(area.Length!=4)throw new InvalidOperationException("Invalid anatomy area");
                foreach(var value in area)if(!double.IsFinite(value)||value<0||value>1)throw new InvalidOperationException("Invalid anatomy coordinate");
                if(area[2]<=0||area[3]<=0)throw new InvalidOperationException("Empty anatomy area");
            }
        }
        return result;
    }
}
