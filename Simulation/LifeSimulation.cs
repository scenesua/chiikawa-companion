using System;
using System.Collections.Generic;
using System.Linq;
#if CROSS_PLATFORM
using Avalonia;
#else
using System.Windows;
#endif
using Momonga.AI;
using Momonga.Animation;
using Momonga.Character;
using Momonga.Economy;
using Momonga.Habitat;
using Momonga.Inventory;
using Momonga.Persistence;

namespace Momonga.Simulation;

public sealed class LifeSimulation
{
    public SaveData Data { get; }
    public CharacterDefinition Character { get; }
    public IReadOnlyDictionary<string, ItemDefinition> Catalog { get; }
    public BehaviorBrain Brain { get; } = new();
    public BehaviorContext Context { get; }
    public HabitatManager Habitat { get; }
    public ShopService Shop { get; }
    private Point? destination;
    public Rect? NavigationBounds { get; set; }
    public Size ActorSize { get; set; } = new(128, 136);
    public Point? Destination
    {
        get
        {
            if (!destination.HasValue || !NavigationBounds.HasValue) return destination;
            var center = new Vector(ActorSize.Width / 2, ActorSize.Height / 2);
            return PetMotion.Clamp(destination.Value - center, NavigationBounds.Value) + center;
        }
        private set => destination = value;
    }
    public bool Quiet { get; private set; }
    public double ReachRadius { get; set; } = 40;
    public event Action<string, bool>? Speech;
    public event Action<PetPose>? Reaction;
    public event Action<string>? ActionAnimation;
    public event Action<ItemDefinition>? Consumed;
    public bool Playing { get; private set; }
    public bool CursorPlaying { get; private set; }
    public void StartPlay(bool cursor = false) { RefreshContext(Context.SuppressInterruptions); if (Brain.Force("Play", Context)) { Playing = true; CursorPlaying = cursor; if (cursor) { targetItem = null; Destination = userPoint + new Vector(-40,35); } } }
    public void StopPlay() { Playing = CursorPlaying = false; Rest(5); Brain.Cooldowns["Play"] = Data.SimulationSeconds + 120; }
    private HabitatItem? targetItem;
    private HabitatItem? seatedBed;
    private HabitatItem? resumeItem;
    private string resumeAction = "";
    private double resumeAt;
    public HabitatItem? InteractionItem => targetItem;
    public double InteractionProgress => completed ? 1 : Math.Clamp(atTargetSeconds / (Brain.Current.Name == "Drink" ? 4 : 6), 0, 1);
    public bool InteractionArrived => targetItem != null && Destination.HasValue && ((Vector)(Destination.Value - Habitat.PetCenter)).Length <= ReachRadius;
    public bool SeatOnBed()
    {
        resumeItem = null;
        var feet = Habitat.PetCenter + new Vector(0, ActorSize.Height * 0.30);
        var bed = Data.Items.Where(i => Catalog[i.ItemId].Category == "Bed").FirstOrDefault(i =>
        {
            var size = Habitat.SizeOf(i); var bounds = new Rect(i.X - 12, i.Y - 12, size.Width + 24, size.Height + 24);
            return bounds.Contains(feet) || bounds.Contains(Habitat.PetCenter);
        });
        if (bed == null) { seatedBed = null; return false; }
        seatedBed = bed; RefreshContext(Context.SuppressInterruptions); Context.OnCushion = true;
        Brain.Force("SitOnBed", Context); targetItem = bed; Destination = FurnitureTarget(bed); return true;
    }
    public Point FurnitureTarget(HabitatItem item) => Habitat.Center(item) +
        (Catalog[item.ItemId].Category == "Bed" ? new Vector(0, -ActorSize.Height * 0.30) : new Vector());
    public bool UseItem(HabitatItem item)
    {
        resumeItem = null;
        if (!Data.Items.Contains(item)) return false;
        var action = item.ItemId == "food-bowl" ? "Eat" : item.ItemId == "water-bowl" ? "Drink" : Catalog[item.ItemId].Category == "Bed" ? "Sleep" : "Play";
        RefreshContext(Context.SuppressInterruptions);
        if (action == "Eat" && item.FoodQuantity == 0 || action == "Drink" && item.Water <= 0 || !Brain.Force(action, Context)) return false;
        targetItem = item; Destination = FurnitureTarget(item); if (action == "Play") { Playing = true; CursorPlaying = false; } return true;
    }
    private bool completed;
    private int requests;
    private double requestAt;
    private double atTargetSeconds;
    private Point userPoint;

    public LifeSimulation(SaveData data, CharacterDefinition character, IReadOnlyDictionary<string, ItemDefinition> catalog)
    {
        Data = data; Character = character; Catalog = catalog;
        Habitat = new HabitatManager(data, catalog, character);
        Shop = new ShopService(data, catalog);
        Context = new BehaviorContext { Pet = data.Pet, Character = character };
        Brain.Cooldowns = data.Cooldowns;
        if (!data.Cooldowns.ContainsKey("AskSnack")) data.Cooldowns["AskSnack"] = data.SimulationSeconds + 600;
        Brain.Changed += Start;
    }

    public void RefreshContext(bool suppress = false)
    {
        Context.Now = Data.SimulationSeconds; Context.Quiet = Habitat.Quiet;
        Context.SuppressInterruptions = suppress; Context.HasFood = Habitat.Find("Food") != null;
        Context.HasWater = Habitat.Find("Water") != null; Context.HasBed = Habitat.HasBed; Context.HasToy = Habitat.HasToy;
        Context.OnCushion = seatedBed != null && Data.Items.Contains(seatedBed);
        Context.QuietSeconds = Data.QuietSeconds; Context.RecentSnackSeconds = Data.SimulationSeconds - Data.LastSnackTime;
        Context.RecentSnacks = Data.InteractionHistory.Count(i => i.Kind == "Snack" && Data.SimulationSeconds - i.Time < 1800);
    }

    public void Update(double seconds, Point user, bool suppress)
    {
        seconds = Math.Clamp(seconds, 0, 1);
        Data.SimulationSeconds += seconds; userPoint = user;
        RefreshContext(suppress);
        if (Playing && CursorPlaying) Destination = user + new Vector(-40,35);
        if (targetItem != null)
        {
            if (!Data.Items.Contains(targetItem)) { Brain.Force("Idle", Context); }
            else Destination = FurnitureTarget(targetItem);
        }
        var wasQuiet = Quiet; Quiet = Habitat.Quiet;
        if (Quiet)
        {
            Data.QuietSeconds += seconds;
            var minutes = seconds / 60;
            if (Data.QuietSeconds > 600) Data.Pet.Social -= minutes * 0.5;
            if (Data.QuietSeconds > 1800)
            {
                Data.Pet.Fun -= minutes * (Habitat.HasToy ? 0.05 : 0.4);
                Data.Pet.Stress += minutes * (Habitat.FavoriteBed ? 0.45 : Habitat.HasBed ? 0.6 : 0.9) * (Habitat.HasToy ? 0.5 : 1);
            }
            if (Data.Pet.Annoyance > 40) Data.Pet.Mood += minutes;
        }
        else
        {
            Data.QuietSeconds = 0;
            if (wasQuiet)
            {
                if (Data.Pet.Stress > 50) Brain.Force("Sulk", Context);
                else if (Data.Pet.Social < 40) Brain.Force("AskAttention", Context);
                else if (Context.RecentSnackSeconds > 360 && Data.Pet.Mood < 55) Brain.Force("AskSnack", Context);
                else Speech?.Invoke("ReturnGreeting", true);
            }
        }
        Data.Pet.Update(seconds);
        if (resumeItem != null && Data.SimulationSeconds >= resumeAt)
        {
            var item = resumeItem; resumeItem = null;
            if (Data.Items.Contains(item) && (resumeAction != "Eat" || item.FoodQuantity > 0) && (resumeAction != "Drink" || item.Water > 0))
            {
                if (resumeAction == "SitOnBed") { seatedBed = item; Context.OnCushion = true; }
                if (Brain.Force(resumeAction, Context)) { targetItem = item; Destination = FurnitureTarget(item); }
            }
        }
        if (Brain.Current.Name == "Play") Playing = true;
        if (Playing && (Brain.Current.Name != "Play" || Data.Pet.Fun >= 90 || Data.Pet.Hunger > 90 || Data.Pet.Energy < 10)) StopPlay();
        if (!Playing) Brain.Update(Context, seconds);
        if (Brain.IsPaused(Data.SimulationSeconds)) return;
        var arrived = !Destination.HasValue || ((Vector)(Destination.Value - Habitat.PetCenter)).Length <= ReachRadius;
        if (arrived) atTargetSeconds += seconds;
        var pet = Data.Pet;
        switch (Brain.Current.Name)
        {
            case "Eat" when arrived && !completed && targetItem?.FoodQuantity > 0:
                if (Catalog.TryGetValue(targetItem.FoodId, out var food))
                {
                    var bite = Math.Min(targetItem.FoodPortion, Math.Min(seconds/6, Math.Max(0,pet.Hunger-5)/Math.Max(1,food.Hunger)));
                    targetItem.FoodPortion = Math.Max(0,targetItem.FoodPortion-bite); pet.Hunger -= food.Hunger*bite;
                    pet.Mood += (food.Mood * Character.ItemPreferences.GetValueOrDefault(food.Id,1) + (Character.FavoriteFoods.Contains(food.Id) ? 4 : 0))*bite;
                    pet.Annoyance -= 6*bite;
                    if (targetItem.FoodPortion < .00001) { targetItem.FoodPortion=0; targetItem.FoodQuantity=0; }
                    if (targetItem.FoodQuantity == 0 || pet.Hunger <= 5.001) { completed=true; Speech?.Invoke("Meal:"+food.Id,true); }
                }
                break;
            case "Drink" when arrived && !completed && targetItem?.Water > 0:
                var drink = Math.Min(targetItem.Water,Math.Min(seconds*10,Math.Max(0,pet.Thirst-5)));
                targetItem.Water -= drink; pet.Thirst -= drink;
                if (atTargetSeconds >= 4 || targetItem.Water <= 0 || pet.Thirst <= 5.001) { completed=true; Speech?.Invoke("WaterReaction",true); }
                break;
            case "Sleep" when arrived:
                pet.Energy += seconds * (targetItem?.ItemId == "beanbag" ? .55 : targetItem?.ItemId == "nest" ? .6 : .45);
                pet.Stress -= seconds * (targetItem != null && Character.PreferredBeds.Contains(targetItem.ItemId) ? .05 : .025); break;
            case "Play" when arrived:
                pet.Fun += seconds * (targetItem?.ItemId == "doll" ? .6 : .8) * (targetItem != null && Character.FavoriteToys.Contains(targetItem.ItemId) ? 1.25 : 1);
                pet.Mood += seconds * .15; pet.Annoyance -= seconds * .08; break;
            case "Sulk" when arrived: pet.Annoyance -= seconds * 0.07; break;
        }
        if (Brain.Current.Name.StartsWith("Ask") && Context.CanInterrupt)
        {
            if (requests == 0 && (arrived || Brain.Current.ActiveSeconds > 5))
            { Speech?.Invoke(RequestTag(), true); requests = 1; requestAt = Data.SimulationSeconds; }
            else if (requests == 1 && Data.SimulationSeconds - requestAt >= 5 + Character.Patience * 8)
            { Speech?.Invoke(RequestTag(), true); requests = 2; }
        }
        pet.Clamp();
    }

    private string RequestTag() => Brain.Current.Name switch
    { "AskFood" => "Hungry", "AskWater" => "Thirsty", "AskSnack" => "SnackRequest", _ => "Attention" };

    private void Start(UtilityBehavior behavior)
    {
        if (behavior.Name != "Play") Playing = CursorPlaying = false;
        completed = false; requests = 0; atTargetSeconds = 0; targetItem = null; Destination = null;
        var kind = behavior.Name switch
        { "Eat" => "Food", "Drink" => "Water", "Sleep" or "Sulk" or "Hide" => "Bed", "Play" => "Toy", "QuietProtest" => "Sign", _ => "" };
        if (kind != "") targetItem = Habitat.Find(kind);
        if (behavior.Name == "SitOnBed") targetItem = seatedBed;
        if (behavior.Name == "Sleep" && seatedBed != null && Data.Items.Contains(seatedBed)) targetItem = seatedBed;
        if (behavior.Name is not ("SitOnBed" or "Sleep")) seatedBed = null;
        if (targetItem != null) Destination = FurnitureTarget(targetItem);
        else if ((behavior.Name.StartsWith("Ask") || behavior.Name == "Play") && Context.CanInterrupt)
        {
            var offset = (Vector)(userPoint - Habitat.PetCenter);
            if (offset.Length > 220) offset *= 220 / offset.Length;
            Destination = Habitat.PetCenter + offset + new Vector(-45, 35);
        }
    }

    public PetPose? Pose => Brain.Current.Name switch
    {
        "Eat" when !completed => PetPose.Eat,
        "Sleep" => PetPose.Sleep,
        "Play" => PetPose.Play,
        "Sulk" or "Hide" => PetPose.Sulk,
        "QuietProtest" => (int)(Data.SimulationSeconds / 0.6) % 2 == 0 ? PetPose.Annoyed : PetPose.Play,
        _ => null
    };

    public bool GiveSnack(string id)
    {
        if (!Catalog.TryGetValue(id, out var item) || item.Category is not ("Snack" or "Drink") || !Shop.Consume(id)) return false;
        var p = Data.Pet; var favorite = Character.ItemPreferences.GetValueOrDefault(id, Character.FavoriteSnacks.Contains(id) ? 1.2 : 1);
        p.Thirst -= item.Thirst; p.Hunger -= item.Hunger; p.Mood += item.Mood * favorite; p.Fun += item.Fun * favorite;
        p.Affection += item.Affection * Character.RelationshipModifiers.GetValueOrDefault("Snack", 1);
        p.Trust += 0.1;
        p.Annoyance -= 15; p.Stress -= 4; p.Social += 5; p.Clamp();
        Data.LastSnackTime = Data.SimulationSeconds;
        Brain.Cooldowns["AskSnack"] = Data.SimulationSeconds + 360;
        Destination = null; Brain.Force("Idle", Context); Brain.Pause(Data.SimulationSeconds, 3);
        Reaction?.Invoke(PetPose.Happy); Speech?.Invoke("Snack:" + item.Id, false); Record("Snack"); Consumed?.Invoke(item); return true;
    }

    public void Interact(string kind)
    {
        if (kind == "Talk") { Speech?.Invoke("IdleTalk", false); Record(kind); return; }
        if (kind == "Refuse") Brain.Cooldowns["AskSnack"] = Data.SimulationSeconds + 360;
        var previousItem = targetItem; var previousAction = Brain.Current.Name;
        var p = Data.Pet;
        var positive = kind is "Pet" or "Chin" or "Soothe" or "Praise" or "Play";
        if (positive)
        {
            p.Social += 8; p.Mood += 5; p.Annoyance -= kind == "Pet" ? 8 : 5; p.Stress -= 2;
            p.Affection += 0.2 * Character.RelationshipModifiers.GetValueOrDefault("Pet", 1); p.Trust += 0.1;
            if (kind == "Play") p.Fun += 12;
        }
        else
        {
            var recent = Data.InteractionHistory.Count(i => i.Kind is "Poke" or "CheekPull" or "Flick" && Data.SimulationSeconds - i.Time < 60);
            p.Annoyance += (kind == "Flick" ? 14 : kind == "CheekPull" ? 10 : kind == "Refuse" ? 7 : 5) *
                (1 + recent * (1 - Character.Patience) * 0.2);
            p.Stress += 1.5; if (recent >= 3) p.Trust -= 0.2;
        }
        p.Clamp(); Record(kind); Destination = null;
        Brain.Force(p.Annoyance > 70 ? "Sulk" : "Idle", Context); Brain.Pause(Data.SimulationSeconds, 3);
        if (previousItem != null && p.Annoyance <= 70 && previousAction is "SitOnBed" or "Sleep" or "Eat" or "Drink")
        { resumeItem = previousItem; resumeAction = previousAction == "Sleep" ? "SitOnBed" : previousAction; resumeAt = Data.SimulationSeconds + 3; }
        Reaction?.Invoke(positive ? PetPose.Happy : p.Annoyance < 25 ? PetPose.Startled : p.Annoyance < 70 ? PetPose.Annoyed : PetPose.Sulk);
        ActionAnimation?.Invoke(kind);
        Speech?.Invoke(positive ? kind == "Praise" ? "PraiseReaction" : kind == "Soothe" ? "SootheReaction" : "PetReaction" : kind == "Refuse" ? "RefuseReaction" : kind == "Flick" ? "FlickReaction" : "Annoyed", false);
    }

    private void Record(string kind)
    {
        Data.InteractionHistory.Add(new InteractionRecord(kind, Data.SimulationSeconds));
        if (Data.InteractionHistory.Count > 40) Data.InteractionHistory.RemoveAt(0);
    }
    public void Approach(Point point) { resumeItem = null; Brain.Force("LookAtCursor", Context); Destination = point; }
    public void Rest(double seconds) { resumeItem = null; Brain.Force("Idle", Context); Destination = null; Brain.Pause(Data.SimulationSeconds, seconds); }
}
