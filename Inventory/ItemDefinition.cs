using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Momonga.Inventory;

public sealed record ItemDefinition(string Id, string Name, string Category, int Price,
    double Hunger = 0, double Mood = 0, double Fun = 0, double Affection = 0,
    double Thirst = 0, int AnimationFrame = 16, string Utensil = "None")
{
    public bool Consumable => Category is "Food" or "Snack" or "Drink";
}

public static class ItemCatalog
{
    public static IReadOnlyDictionary<string, ItemDefinition> Load()
    {
        using var stream = Content.ContentResource.Open("Content/items.json");
        var items = JsonSerializer.Deserialize<ItemDefinition[]>(stream) ?? throw new InvalidOperationException("Empty item catalog");
        if (items.Any(i => string.IsNullOrWhiteSpace(i.Id) || i.Price < 0 || i.AnimationFrame < 0 || i.AnimationFrame > 22 || i.AnimationFrame % 2 != 0 ||
            new[] { i.Hunger, i.Mood, i.Fun, i.Affection, i.Thirst }.Any(v => !double.IsFinite(v) || v < 0 || v > 100) ||
            i.Utensil is not ("None" or "Spoon" or "Chopsticks") ||
            i.Category is not ("Food" or "Snack" or "Drink" or "Toy" or "Bed" or "Habitat")))
            throw new InvalidOperationException("Invalid item catalog");
        return items.ToDictionary(i => i.Id);
    }
}
