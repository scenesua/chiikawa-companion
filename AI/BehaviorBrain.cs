using System;
using System.Collections.Generic;
using System.Linq;
using Momonga.Character;
using Momonga.Simulation;

namespace Momonga.AI;

public enum Intent { None, NeedFood, NeedWater, WantSnack, WantAttention, WantPlay, WantSleep, WantPrivacy }

public sealed class BehaviorContext
{
    public required PetState Pet { get; init; }
    public required CharacterDefinition Character { get; init; }
    public double Now { get; set; }
    public bool Quiet { get; set; }
    public bool SuppressInterruptions { get; set; }
    public bool HasFood { get; set; }
    public bool HasWater { get; set; }
    public bool HasBed { get; set; }
    public bool HasToy { get; set; }
    public bool OnCushion { get; set; }
    public double QuietSeconds { get; set; }
    public double RecentSnackSeconds { get; set; } = double.PositiveInfinity;
    public int RecentSnacks { get; set; }
    public bool LongActiveSession { get; set; }
    public bool CanInterrupt => !Quiet && !SuppressInterruptions;
}

public sealed record UtilityBehavior(string Name, Intent Intent, double MinimumDuration, double Cooldown,
    Func<BehaviorContext, bool> CanExecute, Func<BehaviorContext, double> EvaluateUtility)
{
    public double StartedAt { get; private set; }
    public double ActiveSeconds { get; private set; }
    public void Start(double now) { StartedAt = now; ActiveSeconds = 0; }
    public void Update(double seconds) => ActiveSeconds += seconds;
    public void Stop() => ActiveSeconds = 0;
}

public sealed class BehaviorBrain
{
    private readonly Random random;
    public IReadOnlyList<UtilityBehavior> Behaviors { get; }
    public Dictionary<string, double> Cooldowns { get; set; } = new();
    public Dictionary<string, double> Scores { get; } = new();
    public UtilityBehavior Current { get; private set; }
    public event Action<UtilityBehavior>? Changed;
    private double nextEvaluation;
    private double pauseUntil;

    public BehaviorBrain(Random? random = null)
    {
        this.random = random ?? new Random();
        Behaviors = new UtilityBehavior[]
        {
            new("Idle", Intent.None, 8, 0, _ => true, _ => 8),
            new("SitOnBed", Intent.None, 8, 20, c => c.OnCushion, c => 12 + c.Character.Sleepiness * 8),
            new("Wander", Intent.None, 5, 25, _ => true, c => 4 + c.Character.WanderFrequency * 12),
            new("LookAtCursor", Intent.None, 3, 20, _ => true, c => 5 + c.Pet.Curiosity * 0.05),
            new("Eat", Intent.NeedFood, 8, 6, c => c.HasFood, c => c.Pet.Hunger < 45 ? -100 : c.Pet.Hunger * (0.95 + c.Character.FoodLove * 0.3) - 20),
            new("Drink", Intent.NeedWater, 6, 6, c => c.HasWater, c => c.Pet.Thirst * 1.2 - 20),
            new("Sleep", Intent.WantSleep, 25, 10, _ => true, c => (100 - c.Pet.Energy) * 1.1 - 20 + c.Character.Sleepiness * 8),
            new("Play", Intent.WantPlay, 10, 20, c => c.HasToy || c.CanInterrupt, c => (100 - c.Pet.Fun) * c.Character.Playfulness - 10),
            new("AskFood", Intent.NeedFood, 15, 180, c => c.CanInterrupt && !c.HasFood, c => c.Pet.Hunger - 35),
            new("AskWater", Intent.NeedWater, 15, 180, c => c.CanInterrupt && !c.HasWater, c => c.Pet.Thirst - 35),
            new("AskSnack", Intent.WantSnack, 15, 360, c => c.CanInterrupt && c.RecentSnackSeconds > 240 && c.Pet.Annoyance < 65,
                c => c.Character.SnackLove * 28 + (60 - c.Pet.Mood) * 0.15 + Math.Min(10, c.RecentSnackSeconds / 120) + Math.Min(6, c.RecentSnacks * 2)),
            new("AskAttention", Intent.WantAttention, 12, 180, c => c.CanInterrupt && c.Pet.Annoyance < 55,
                c => (100 - c.Pet.Social) * c.Character.Clinginess - 15 + c.Pet.Affection * 0.04 + (c.LongActiveSession ? 3 : 0)),
            new("Sulk", Intent.WantPrivacy, 20, 15, _ => true, c => c.Pet.Annoyance * 1.1 - 35 + c.Pet.Stress * 0.25),
            new("Hide", Intent.WantPrivacy, 15, 30, c => c.HasBed, c => c.Pet.Stress - 35 + c.Character.Timidity * 10),
            new("QuietProtest", Intent.WantPrivacy, 6, 120, c => c.Quiet && (c.QuietSeconds > 1800 || c.Pet.Social < 35),
                c => 15 + c.Pet.Stress * 0.4)
        };
        Current = Behaviors[0];
    }

    public bool Update(BehaviorContext context, double seconds)
    {
        if (context.Now < pauseUntil && Current.CanExecute(context)) return false;
        Current.Update(seconds);
        var invalid = !Current.CanExecute(context);
        if (!invalid && context.Now < nextEvaluation) return false;
        nextEvaluation = context.Now + 2;
        Scores.Clear();
        foreach (var behavior in Behaviors)
        {
            var ready = behavior == Current || !Cooldowns.TryGetValue(behavior.Name, out var until) || context.Now >= until;
            var modifier = context.Character.BehaviorModifiers.GetValueOrDefault(behavior.Name, 1);
            Scores[behavior.Name] = behavior.CanExecute(context) && ready
                ? Math.Max(0, behavior.EvaluateUtility(context) * modifier + random.NextDouble() * 3) : 0;
        }
        if (!invalid && Current.ActiveSeconds < Current.MinimumDuration) return false;
        var selected = Behaviors.MaxBy(b => Scores[b.Name])!;
        // Completed actions yield so another need can surface; they retain their own cooldown.
        if (!invalid && selected == Current && Current.ActiveSeconds < Math.Max(20, Current.MinimumDuration * 2)) return false;
        if (selected == Current) selected = Behaviors[0];
        return Select(selected, context.Now);
    }

    public bool Force(string name, BehaviorContext context)
    {
        var behavior = Behaviors.FirstOrDefault(b => b.Name == name);
        if (behavior == null || !behavior.CanExecute(context)) return false;
        Select(behavior, context.Now);
        return true;
    }

    private bool Select(UtilityBehavior behavior, double now)
    {
        Cooldowns[Current.Name] = now + Current.Cooldown;
        Current.Stop(); Current = behavior; Current.Start(now);
        Changed?.Invoke(Current);
        return true;
    }

    public void Pause(double now, double seconds) => pauseUntil = now + seconds;
    public bool IsPaused(double now) => now < pauseUntil;
}
