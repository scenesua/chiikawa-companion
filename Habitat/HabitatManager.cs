using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Momonga.Character;
using Momonga.Inventory;
using Momonga.Persistence;

namespace Momonga.Habitat;

public sealed class HabitatManager(SaveData data, IReadOnlyDictionary<string, ItemDefinition> catalog, CharacterDefinition character)
{
    public Rect Bounds { get { var sign = Find("Sign"); var center = sign == null ? PetCenter : Center(sign); return new Rect(center.X - 250, center.Y - 250, 500, 500); } }
    public Point PetCenter { get; set; }
    public bool Inside => SignActive && Bounds.Contains(PetCenter);
    public IEnumerable<HabitatItem> Placed => data.Items;
    public bool SignActive => Placed.Any(i => catalog[i.ItemId].Category == "Bed" && i.Active);
    public bool Quiet => Inside && SignActive;
    public bool HasBed => Placed.Any(i => catalog[i.ItemId].Category == "Bed");
    public bool FavoriteBed => Placed.Any(i => character.PreferredBeds.Contains(i.ItemId));
    public bool HasToy => Placed.Any(i => catalog[i.ItemId].Category == "Toy");
    public Size SizeOf(HabitatItem item) => new(80 * data.Settings.PetScale, 80 * data.Settings.PetScale);
    public Point Center(HabitatItem item) { var size = SizeOf(item); return new Point(item.X + size.Width / 2, item.Y + size.Height / 2); }
    public HabitatItem? Find(string type) => Placed.Where(i => type switch
    {
        "Food" => i.ItemId == "food-bowl" && i.FoodQuantity > 0,
        "Water" => i.ItemId == "water-bowl" && i.Water > 0,
        "Sign" => catalog[i.ItemId].Category == "Bed" && i.Active,
        _ => catalog[i.ItemId].Category == type
    }).MinBy(i => (Center(i) - PetCenter).Length -
        (character.PreferredBeds.Contains(i.ItemId) || character.FavoriteToys.Contains(i.ItemId) ? 80 : 0));
}
