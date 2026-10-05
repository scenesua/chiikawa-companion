using System;
using System.Collections.Generic;
using System.Linq;
using Momonga.Inventory;
using Momonga.Persistence;

namespace Momonga.Economy;

public sealed class ShopService(SaveData data, IReadOnlyDictionary<string, ItemDefinition> catalog)
{
    public int Quantity(string id) => data.Inventory.GetValueOrDefault(id) + (data.LeftoverFood.GetValueOrDefault(id) > 0 ? 1 : 0);
    public int AvailableFurniture(string id) => Quantity(id) - data.Items.Count(i => i.ItemId == id);
    public bool Buy(string id)
    {
        if (!catalog.TryGetValue(id, out var item) || data.ActivityPoints < item.Price || Quantity(id) >= 9999) return false;
        data.ActivityPoints -= item.Price;
        data.Inventory[id] = data.Inventory.GetValueOrDefault(id) + 1;
        return true;
    }
    public bool Consume(string id)
    {
        if (!catalog.TryGetValue(id, out var item) || !item.Consumable || data.Inventory.GetValueOrDefault(id) <= 0) return false;
        data.Inventory[id]--; return true;
    }
    public HabitatItem? Place(string id)
    {
        if (id == "ball") return null;
        if (!catalog.TryGetValue(id, out var item) || item.Consumable || AvailableFurniture(id) <= 0 || data.Items.Count >= 30) return null;
        var placed = new HabitatItem { ItemId = id, ZoneId = data.Zone.Id, MonitorId = data.Zone.MonitorId,
            X = data.PetX + data.Items.Count % 4 * 90, Y = data.PetY + 100 };
        data.Items.Add(placed); return placed;
    }
    public bool FillFood(HabitatItem bowl, string food)
    {
        if (bowl.ItemId != "food-bowl" || bowl.ZoneId != data.Zone.Id || !data.Items.Contains(bowl) ||
            !catalog.TryGetValue(food, out var item) || item.Category != "Food" || bowl.FoodQuantity >= 1 ||
            bowl.FoodQuantity > 0 && bowl.FoodId != food || Quantity(food) <= 0) return false;
        if (data.Inventory.GetValueOrDefault(food) > 0) { data.Inventory[food]--; bowl.FoodPortion = 1; }
        else { bowl.FoodPortion = data.LeftoverFood[food]; data.LeftoverFood.Remove(food); }
        bowl.FoodId = food; bowl.FoodQuantity = 1; return true;
    }
    public bool ReturnFood(HabitatItem bowl)
    {
        if (bowl.FoodQuantity <= 0) return true;
        var total = data.LeftoverFood.GetValueOrDefault(bowl.FoodId) + bowl.FoodPortion;
        var whole = (int)System.Math.Floor(total); var partial = total-whole;
        if (data.Inventory.GetValueOrDefault(bowl.FoodId)+whole+(partial>0 ? 1 : 0)>9999) return false;
        data.Inventory[bowl.FoodId] = data.Inventory.GetValueOrDefault(bowl.FoodId)+whole;
        if (partial>0) data.LeftoverFood[bowl.FoodId]=partial; else data.LeftoverFood.Remove(bowl.FoodId);
        bowl.FoodQuantity=0; bowl.FoodPortion=0; return true;
    }
}
