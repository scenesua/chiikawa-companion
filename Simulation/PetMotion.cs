using System;
#if CROSS_PLATFORM
using Avalonia;
#else
using System.Windows;
#endif
using Momonga.Character;

namespace Momonga.Simulation;

public enum IdleActivity { Calm, Doze, Groom, Stretch, Play, TailHug }

// M1 movement only. Needs and utility selection enter in M2.
public sealed class PetMotion
{
    private readonly Random random;
    private readonly CharacterDefinition character;
    private Point? target;
    private double idleSeconds;
    private double activitySeconds;
    public IdleActivity Activity { get; private set; }
    public string Behavior => target.HasValue ? "Wander" : "Idle";

    public PetMotion(Random? random = null, CharacterDefinition? character = null)
    {
        this.random = random ?? new Random();
        this.character = character ?? new CharacterDefinition();
        this.character.Validate();
        Rest();
    }

    public void Rest()
    {
        target = null;
        idleSeconds = character.WanderFrequency == 0 ? double.PositiveInfinity
            : random.Next(8, 19) / character.WanderFrequency;
        Activity = IdleActivity.Calm;
        activitySeconds = random.Next(6, 13);
    }

    public Point Update(Point position, Rect bounds, double seconds)
    {
        position = Clamp(position, bounds);
        seconds = Math.Clamp(seconds, 0, 0.1);
        if (!target.HasValue)
        {
            idleSeconds -= seconds;
            activitySeconds -= seconds;
            if (activitySeconds <= 0)
            {
                if (Activity != IdleActivity.Calm) Activity = IdleActivity.Calm;
                else
                {
                    var roll = random.NextDouble();
                    var doze = character.Sleepiness * 0.25;
                    Activity = roll < doze ? IdleActivity.Doze
                        : roll < doze + 0.2 ? IdleActivity.Groom
                        : roll < doze + 0.32 ? IdleActivity.Stretch
                        : roll < doze + 0.32 + character.Playfulness * 0.2 + character.Mischief * 0.05 ? IdleActivity.Play
                        : IdleActivity.TailHug;
                }
                activitySeconds = Activity == IdleActivity.Calm ? random.Next(6, 13) : random.Next(4, 8);
            }
            if (idleSeconds <= 0)
            {
                Activity = IdleActivity.Calm;
                target = Clamp(new Point(position.X + random.Next(-180, 181),
                    position.Y + random.Next(-70, 71)), bounds);
            }
            return position;
        }

        var destination = Clamp(target.Value, bounds);
        var direction = (Vector)(destination - position);
        var distance = direction.Length;
        var step = 32 * seconds;
        if (distance <= step)
        {
            Rest();
            return destination;
        }
        return position + direction * (step / distance);
    }

    public static Point Clamp(Point point, Rect bounds) => new(
        Math.Clamp(point.X, bounds.Left, bounds.Right),
        Math.Clamp(point.Y, bounds.Top, bounds.Bottom));
}
