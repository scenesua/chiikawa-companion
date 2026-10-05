using System;
using System.Collections.Generic;
using System.Linq;
#if CROSS_PLATFORM
using Avalonia;
#else
using System.Windows;
#endif

namespace Momonga.Simulation;

public enum PlaygroundMode { Off, Bubbles, Snacks, Ball }
public sealed class PlayParticle
{
    public Point Position { get; set; }
    public Vector Velocity { get; set; }
    public double Radius { get; init; }
    public double Age { get; set; }
    public double PopAge { get; set; } = -1;
    public bool Popped => PopAge >= 0;
    public bool Bonked { get; set; }
}

public sealed class PlaygroundGame
{
    private readonly Random random = new();
    private readonly List<PlayParticle> particles = new();
    private double time, lastBurst = -1, scale = 1;
    private Point catchHome;
    private double deadpanFrom, deadpanUntil;
    public Rect Bounds { get; set; } = new(0,0,1600,900);
    public PlaygroundMode Mode { get; private set; }
    public bool Active => Mode != PlaygroundMode.Off;
    public IReadOnlyList<PlayParticle> Particles => particles;
    public Point? Target { get; private set; }
    public bool Aiming { get; private set; }
    public Point AimOrigin { get; private set; }
    public Point AimPointer { get; private set; }
    public Vector AimVelocity
    {
        get { var speed=(Vector)(AimOrigin-AimPointer)*7.5; return speed.Length>1000 ? speed*(1000/speed.Length) : speed; }
    }
    public int Hits { get; private set; }
    public int Misses { get; private set; }
    public int Bonks { get; private set; }
    public double CatchChance { get; set; } = 1;
    public Point? WatchTarget => Mode==PlaygroundMode.Snacks ? particles.LastOrDefault(p=>!p.Popped)?.Position : null;
    public bool Deadpan => Mode==PlaygroundMode.Snacks && time>=deadpanFrom && time<deadpanUntil && !WatchTarget.HasValue;
    public void Start(PlaygroundMode mode, double petScale, Point pet)
    {
        Stop(); Mode = mode; scale = Math.Clamp(petScale,.5,2); catchHome=pet; time = 0; lastBurst = -1; Hits = Misses = Bonks = 0;deadpanFrom=deadpanUntil=0;
    }
    public void Stop() { Mode = PlaygroundMode.Off; particles.Clear(); Target = null; Aiming = false; }
    public void Blow(Point point)
    {
        if (Mode != PlaygroundMode.Bubbles || time - lastBurst < .3) return;
        lastBurst = time;
        for (var i=0; i<5 && particles.Count<24; i++)
            particles.Add(new PlayParticle { Position=point+new Vector((random.NextDouble()-.5)*24*scale,0),
                Radius=(10+random.NextDouble()*9)*scale, Age=-i*.06,
                Velocity=new Vector((random.NextDouble()-.5)*65,-18-random.NextDouble()*22) });
    }
    public void BeginAim(Point point)
    {
        if (Mode is not (PlaygroundMode.Snacks or PlaygroundMode.Ball)) return;
        Aiming=true; AimOrigin=AimPointer=point;
    }
    public void Pull(Point point)
    {
        if (!Aiming) return;
        var offset=(Vector)(point-AimOrigin);
        AimPointer=AimOrigin+(offset.Length>140*scale ? offset*(140*scale/offset.Length) : offset);
    }
    public void ReleaseAim()
    {
        if (!Aiming) return;
        Aiming=false; var pull=(Vector)(AimOrigin-AimPointer);
        if (pull.Length<4*scale || particles.Count>=8) return;
        deadpanUntil=0;
        particles.Add(new PlayParticle {Position=AimPointer,Velocity=AimVelocity,Radius=12*scale});
    }
    public void ReleaseBall(BallGame ball)
    {
        if (!Aiming || Mode != PlaygroundMode.Ball) return;
        Aiming=false; ball.Drag(AimPointer); ball.Throw(AimVelocity);
    }
    public int Update(double seconds, Point pet, Size actor, bool paused)
    {
        if (!Active || paused) return 0;
        seconds=Math.Clamp(seconds,0,1); time+=seconds; scale=Math.Clamp(actor.Width/128,.5,2);
        var hits=0;
        var body=new Rect(pet.X-actor.Width*.35,pet.Y-actor.Height*.4,actor.Width*.7,actor.Height*.85);
        foreach (var p in particles)
        {
            if (p.Popped) {p.PopAge+=seconds;if(p.Bonked)p.Position+=p.Velocity*seconds;continue;}
            p.Age+=seconds; if(p.Age<0)continue;
            if(Mode==PlaygroundMode.Bubbles)
            {
                p.Position+=p.Velocity*seconds+new Vector(Math.Sin(p.Age*2)*8*seconds,0);
                if(((Vector)(p.Position-pet)).Length < Math.Max(actor.Width,actor.Height)*.6+p.Radius)
                {p.PopAge=0;hits++;}
            }
            else
            {
                var remaining=seconds;
                while(remaining>0 && !p.Popped)
                {
                    var dt=Math.Min(remaining,1.0/120);remaining-=dt;
                    var next=p.Position+p.Velocity*dt;
                    var hitbox=new Rect(body.X-p.Radius,body.Y-p.Radius,body.Width+p.Radius*2,body.Height+p.Radius*2);
                    if(Crosses(p.Position,next,hitbox))
                    {
                        p.PopAge=0;
                        if(random.NextDouble()<Math.Clamp(CatchChance,0,1))hits++;
                        else {p.Bonked=true;Bonks++;deadpanFrom=time+.6;deadpanUntil=time+2.8;}
                    }
                    p.Position=next;p.Velocity+=new Vector(0,320*dt);
                    if(p.Bonked) {p.Position=pet+new Vector(0,-actor.Height*.18);p.Velocity=new Vector(35,-85);}
                }
            }
        }
        particles.RemoveAll(p=>
        {
            var expired=p.Popped ? p.PopAge>=.3 : p.Age>(Mode==PlaygroundMode.Bubbles?9:3) || !Bounds.Contains(p.Position);
            if(expired && !p.Popped && Mode==PlaygroundMode.Snacks) {Misses++;deadpanFrom=time;deadpanUntil=time+2.5;}
            return expired;
        });
        Hits+=hits;
        Target=Mode==PlaygroundMode.Bubbles ? particles.Where(p=>!p.Popped && p.Age>=0)
            .OrderBy(p=>((Vector)(p.Position-pet)).Length).Select(p=>(Point?)p.Position).FirstOrDefault() : null;
        if(Mode==PlaygroundMode.Snacks)
        {
            // A short anticipation step near home, not an unlimited chase of missed shots.
            foreach(var p in particles.Where(p=>!p.Popped))
            {
                for(var t=.12;t<=.48;t+=.12)
                {
                    var predicted=p.Position+p.Velocity*t+new Vector(0,160*t*t);
                    var offset=(Vector)(predicted-catchHome);
                    if(offset.Length>100*scale || ((Vector)(predicted-pet)).Length>actor.Width*.35+85*t)continue;
                    Target=catchHome+(offset.Length>60*scale?offset*(60*scale/offset.Length):offset);break;
                }
                if(Target.HasValue)break;
            }
        }
        return hits;
    }
    // Segment/rectangle collision keeps fast treats from skipping a small character.
    private static bool Crosses(Point a,Point b,Rect box)
    {
        var start=0.0;var end=1.0;var delta=(Vector)(b-a);
        foreach(var (origin,speed,min,max) in new[]{(a.X,delta.X,box.Left,box.Right),(a.Y,delta.Y,box.Top,box.Bottom)})
        {
            if(Math.Abs(speed)<.0001) {if(origin<min || origin>max)return false;continue;}
            var first=(min-origin)/speed;var last=(max-origin)/speed;
            if(first>last)(first,last)=(last,first);
            start=Math.Max(start,first);end=Math.Min(end,last);if(start>end)return false;
        }
        return true;
    }
}
