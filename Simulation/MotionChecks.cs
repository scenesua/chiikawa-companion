using System;
using System.Windows;
using Momonga.Character;

namespace Momonga.Simulation;

public static class MotionChecks
{
    public static void Run()
    {
        var bounds = new Rect(0, 0, 400, 200);
        Require(PetMotion.Clamp(new Point(-20, 250), bounds) == new Point(0, 200), "Clamp");
        var motion = new PetMotion(new Random(42));
        var position = new Point(200, 100);
        var wandered = false;
        for (var i = 0; i < 10000; i++)
        {
            var next = motion.Update(position, bounds, 0.05);
            Require(bounds.Contains(next), "Movement escaped work area");
            Require((next - position).Length <= 1.600001, "Movement exceeded speed");
            wandered |= next != position;
            position = next;
        }
        Require(wandered, "Pet never wandered");
        motion.Rest();
        Require(motion.Update(position, bounds, 0.05) == position, "Rest did not stop movement");
        Require(motion.Update(position, bounds, -1) == position, "Negative time moved pet");
        Require(motion.Update(position, new Rect(10, 20, 0, 0), 0.1) == new Point(10, 20), "Tiny work area");
        var quietCharacter = CharacterDefinition.Load() with { WanderFrequency = 0 };
        var stationary = new PetMotion(new Random(42), quietCharacter);
        position = new Point(200, 100);
        var activities = new System.Collections.Generic.HashSet<IdleActivity>();
        for (var i = 0; i < 10000; i++)
        {
            Require(stationary.Update(position, bounds, 0.1) == position, "Zero wander frequency still moved");
            activities.Add(stationary.Activity);
        }
        Require(activities.Count == Enum.GetValues<IdleActivity>().Length && activities.Contains(IdleActivity.TailHug), "Stationary pet did not perform idle activities");
        Require(CountMoving(1) > CountMoving(0.25) * 2, "Character wander frequency had no meaningful effect");
        Require(CountMoving(0.25) < 2000, "Default character did not spend most of its time resting");
        try
        {
            _ = new PetMotion(character: quietCharacter with { WanderFrequency = double.NaN });
            throw new InvalidOperationException("Invalid personality was accepted");
        }
        catch (InvalidOperationException error) when (error.Message.Contains("Character traits")) { }
    }

    private static int CountMoving(double frequency)
    {
        var pet = new PetMotion(new Random(42), new CharacterDefinition { WanderFrequency = frequency });
        var bounds = new Rect(0, 0, 400, 200);
        var position = new Point(200, 100);
        var moving = 0;
        for (var i = 0; i < 10000; i++)
        {
            var next = pet.Update(position, bounds, 0.1);
            if (next != position) moving++;
            position = next;
        }
        return moving;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
