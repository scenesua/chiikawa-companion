using System;

namespace Momonga.Simulation;

public sealed class PetState
{
    public double Hunger { get; set; } = 35;
    public double Thirst { get; set; } = 20;
    public double Energy { get; set; } = 80;
    public double Social { get; set; } = 75;
    public double Fun { get; set; } = 75;
    public double Mood { get; set; } = 65;
    public double Stress { get; set; } = 5;
    public double Annoyance { get; set; }
    public double Curiosity { get; set; } = 40;
    public double Affection { get; set; } = 25;
    public double Trust { get; set; } = 35;

    public void Clamp()
    {
        Hunger = Limit(Hunger); Thirst = Limit(Thirst); Energy = Limit(Energy);
        Social = Limit(Social); Fun = Limit(Fun); Mood = Limit(Mood);
        Stress = Limit(Stress); Annoyance = Limit(Annoyance); Curiosity = Limit(Curiosity);
        Affection = Limit(Affection); Trust = Limit(Trust);
    }

    private static double Limit(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 50;

    public void Update(double seconds)
    {
        var minutes = seconds / 60;
        Hunger += minutes * .2; Thirst += minutes * 2.5; Energy -= minutes * 0.8;
        Social -= minutes * 1.2; Fun -= minutes;
        Annoyance -= minutes * 3; Stress -= minutes * 0.3;
        Mood += Math.Sign(65 - Mood) * Math.Min(Math.Abs(65 - Mood), minutes * 0.5);
        Clamp();
    }

    public void Offline(TimeSpan elapsed)
    {
        var hours = Math.Clamp(elapsed.TotalHours, 0, 2);
        Hunger += hours * 7.5; Thirst += hours * 8;
        Energy = Math.Max(Math.Min(Energy, 35), Energy - hours * 5);
        Social = Math.Max(Math.Min(Social, 30), Social - hours * 6);
        Fun = Math.Max(Math.Min(Fun, 30), Fun - hours * 5);
        Annoyance = Math.Max(0, Annoyance - hours * 6);
        Clamp();
    }
}
