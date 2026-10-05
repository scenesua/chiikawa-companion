using System;
#if CROSS_PLATFORM
using Avalonia;
#else
using System.Windows;
#endif

namespace Momonga.Simulation;

public enum BallPhase { Off, Ready, Held, Flying, Catching, Returning, Hiding }

// Runtime toy: no inventory purchase or saved projectile velocity.
public sealed class BallGame
{
    private Point home;
    private Point catchFrom;
    private Vector velocity;
    private double elapsed;
    public Rect Bounds { get; set; } = new(0, 0, 1600, 900);
    public Point Position { get; private set; }
    public double Radius { get; private set; } = 18;
    public BallPhase Phase { get; private set; }
    public bool Active => Phase != BallPhase.Off;
    public bool Running => Phase is BallPhase.Flying or BallPhase.Returning;
    public bool BehindPet => Phase == BallPhase.Hiding && elapsed > .25;
    public double Opacity => Phase == BallPhase.Hiding ? Math.Clamp(1 - elapsed / 1.1, 0, 1) : 1;
    public Point? Target => Phase == BallPhase.Flying ? Position : Phase == BallPhase.Returning ? home : null;
    private Point Clamp(Point p) => PetMotion.Clamp(p, new Rect(Bounds.Left + Radius, Bounds.Top + Radius,
        Math.Max(0, Bounds.Width - Radius * 2), Math.Max(0, Bounds.Height - Radius * 2)));
    public void Start(Point pet, double scale)
    {
        Radius = Math.Clamp(18 * scale, 9, 72);
        home = pet; Position = Clamp(pet + new Vector(Radius * 4.5, Radius));
        velocity = new(); elapsed = 0; Phase = BallPhase.Ready;
    }
    public void Cancel() { Phase = BallPhase.Off; velocity = new(); }
    public void Finish() { if (!Active || Phase == BallPhase.Hiding) return; catchFrom = Position; Phase = BallPhase.Hiding; elapsed = 0; }
    public bool Grab()
    {
        if (!Active || Phase == BallPhase.Hiding) return false;
        Phase = BallPhase.Held; velocity = new(); elapsed = 0; return true;
    }
    public void Drag(Point point) { if (Phase == BallPhase.Held) Position = Clamp(point); }
    public void Throw(Vector speed)
    {
        if (!Active || Phase == BallPhase.Hiding || !double.IsFinite(speed.X) || !double.IsFinite(speed.Y)) return;
        velocity = speed.Length > 1600 ? speed * (1600 / speed.Length) : speed;
        Phase = BallPhase.Flying; elapsed = 0;
    }
    public double Update(double seconds, Point pet, Size actor, bool paused)
    {
        Radius = Math.Clamp(18 * actor.Width / 128, 9, 72);
        Position = Clamp(Position);
        home = PetMotion.Clamp(home, new Rect(Bounds.Left + actor.Width / 2, Bounds.Top + actor.Height / 2,
            Math.Max(0, Bounds.Width - actor.Width), Math.Max(0, Bounds.Height - actor.Height)));
        if (!Active || paused) return 0;
        seconds = Math.Clamp(seconds, 0, 1); elapsed += seconds;
        var reward = 0.0;
        if (Phase == BallPhase.Flying)
        {
            // Substeps preserve bounces through a delayed UI tick, including corners.
            var remaining = seconds;
            while (remaining > 0)
            {
                var dt = Math.Min(remaining, 1.0 / 120); remaining -= dt;
                var next = Position + velocity * dt;
                if (next.X < Bounds.Left + Radius && velocity.X < 0 || next.X > Bounds.Right - Radius && velocity.X > 0) velocity = new Vector(-velocity.X * .86, velocity.Y);
                if (next.Y < Bounds.Top + Radius && velocity.Y < 0 || next.Y > Bounds.Bottom - Radius && velocity.Y > 0) velocity = new Vector(velocity.X, -velocity.Y * .86);
                Position = Clamp(next); velocity *= Math.Exp(-.35 * dt);
            }
            if (elapsed > .35 && ((Vector)(Position - pet)).Length < Math.Max(Radius * 2, Math.Max(actor.Width, actor.Height) * .6) && velocity.Length < 480)
            { catchFrom = Position; Phase = BallPhase.Catching; elapsed = 0; reward = 1.5; }
        }
        else if (Phase == BallPhase.Catching && elapsed >= .45) { Phase = BallPhase.Returning; elapsed = 0; }
        else if (Phase == BallPhase.Returning && ((Vector)(home - pet)).Length < Math.Max(18, actor.Width * .2))
        { Phase = BallPhase.Ready; elapsed = 0; Position = Clamp(home + new Vector(Radius * 4.5, Radius)); reward = .5; }
        if (Phase == BallPhase.Catching)
            Position = Clamp(catchFrom + (Vector)(pet + new Vector(0,actor.Height*.18) - catchFrom) * Math.Min(1,elapsed/.45));
        else if (Phase == BallPhase.Returning) Position = Clamp(pet + new Vector(0, actor.Height * .18));
        if (Phase == BallPhase.Hiding)
        {
            var behind = pet + new Vector(actor.Width * .3 * (1 - Math.Min(1, elapsed / .6)), actor.Height * .12);
            Position = Clamp(catchFrom + (Vector)(behind - catchFrom) * Math.Min(1, elapsed / .25));
            if (elapsed >= 1.1) Cancel();
        }
        return reward;
    }
}
