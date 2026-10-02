using System;
using System.Text.Json;

namespace Momonga.Character;

public sealed record CharacterDefinition
{
    public string CharacterId { get; init; } = "momonga";
    public string Category { get; init; } = "치이카와";
    public string Description { get; init; } = "";
    public string DisplayName { get; init; } = "모몽가";
    public double TalkFrequency { get; init; } = 0.65;
    public double SpeechInterval => 30 + (1 - TalkFrequency) * 150;
    public double IdleTalkInterval => SpeechInterval * 2.5;
    public double WanderFrequency { get; init; } = 0.25;
    public double Playfulness { get; init; } = 0.72;
    public double Sleepiness { get; init; } = 0.55;
    public string AssetSet { get; init; } = "momonga";
    public string DialogueSetId { get; init; } = "momonga";
    public string AnimationSetId { get; init; } = "momonga";
    public double Clinginess { get; init; } = 0.7;
    public double Timidity { get; init; } = 0.28;
    public double Mischief { get; init; } = 0.78;
    public double Patience { get; init; } = 0.38;
    public double FoodLove { get; init; } = 0.78;
    public double SnackLove { get; init; } = 0.94;
    public ThemeDefinition Theme { get; init; } = new();
    public string[] FavoriteFoods { get; init; } = { "nuts" };
    public string[] FavoriteSnacks { get; init; } = { "dessert", "special-snack" };
    public string[] FavoriteToys { get; init; } = { "doll" };
    public string[] PreferredBeds { get; init; } = { "beanbag", "nest" };
    public System.Collections.Generic.Dictionary<string, double> BehaviorModifiers { get; init; } = new();
    public System.Collections.Generic.Dictionary<string, double> RelationshipModifiers { get; init; } = new();
    public System.Collections.Generic.Dictionary<string, double> ItemPreferences { get; init; } = new();

    public void Validate()
    {
        foreach (var id in new[] { CharacterId, AssetSet, DialogueSetId, AnimationSetId })
            if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || !System.Linq.Enumerable.All(id, c => char.IsAsciiLetterOrDigit(c) || c == '-'))
                throw new InvalidOperationException("Invalid character content identifier");
        if (Theme == null || string.IsNullOrWhiteSpace(DisplayName)) throw new InvalidOperationException("Missing character theme or name");
        foreach (var value in new[] { TalkFrequency, WanderFrequency, Playfulness, Sleepiness, Clinginess, Timidity, Mischief, Patience, FoodLove, SnackLove })
            if (!double.IsFinite(value) || value < 0 || value > 1)
                throw new InvalidOperationException("Character traits must be finite values from 0 to 1");
        if (ItemPreferences == null || BehaviorModifiers == null || RelationshipModifiers == null || FavoriteFoods == null || FavoriteSnacks == null ||
            FavoriteToys == null || PreferredBeds == null) throw new InvalidOperationException("Missing character preferences");
        foreach (var value in System.Linq.Enumerable.Concat(System.Linq.Enumerable.Concat(BehaviorModifiers.Values, RelationshipModifiers.Values), ItemPreferences.Values))
            if (!double.IsFinite(value) || value < 0 || value > 5) throw new InvalidOperationException("Invalid personality modifier");
    }

    public static readonly string[] Ids = { "chiikawa", "hachiware", "usagi", "momonga", "kurimanju", "shisa", "kani", "rakko", "dekatsuyo", "anoko", "goblin", "chiikabu", "ode", "rilakkuma", "korilakkuma", "mymelody", "kuromi" };
    public static CharacterDefinition[] All => System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(Ids, Load));

    public static CharacterDefinition Load(string id = "momonga")
    {
        if (!System.Linq.Enumerable.Contains(Ids, id)) throw new ArgumentException("Unknown character", nameof(id));
        using var data = Content.ContentResource.Open($"Content/{id}.json");
        var definition = JsonSerializer.Deserialize<CharacterDefinition>(data)
            ?? throw new InvalidOperationException("Missing character definition");
        definition.Validate();
        return definition;
    }
}
