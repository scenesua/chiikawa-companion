using System;
using System.IO;
using System.Linq;
#if CROSS_PLATFORM
using Avalonia;
#else
using System.Windows;
#endif
using Momonga.AI;
using Momonga.Character;
using Momonga.Content;
using Momonga.Economy;
using Momonga.Input;
using Momonga.Inventory;
using Momonga.Persistence;
#if !CROSS_PLATFORM
using Momonga.UI;
#endif

namespace Momonga.Simulation;

public static class LifeChecks
{
    public static void Run()
    {
        var catalog = ItemCatalog.Load(); var character = CharacterDefinition.Load();
        var mealInterval=new PetState { Hunger=5 };
        mealInterval.Update(3*3600); Require(mealInterval.Hunger<45,"Meal needed before three hours");
        mealInterval.Update(3600); Require(mealInterval.Hunger>=45,"Meal was not needed within four hours");
        Require((character with { TalkFrequency = 1 }).SpeechInterval < (character with { TalkFrequency = 0 }).SpeechInterval, "Talk personality did not change frequency");
#if !CROSS_PLATFORM
        var speechAnchor = new PetWindow { Left = 400, Top = 400 };
        var speech = new SpeechBubbleWindow();
        try
        {
            speech.Say("가나다", speechAnchor); var emptyWidth = speech.Width;
            speech.RevealNext(); Require(speech.VisibleText == "가" && speech.Width + .5 >= emptyWidth, "Typewriter did not reveal one character");
            speech.RevealAll(); var shortHeight = speech.Height;
            speech.Say(string.Join(" ", Enumerable.Repeat("A long bubble must wrap onto multiple lines.", 20)), speechAnchor); speech.RevealAll();
            Require(speech.Width <= 276 && speech.Height > shortHeight, $"Speech bubble failed width cap or vertical wrapping: {speech.Width} x {speech.Height}, short {shortHeight}");
            speech.Say("새 대사", speechAnchor); Require(speech.VisibleText == "", "New speech did not cancel previous typing");
        }
        finally { speech.Close(); speechAnchor.Close(); }
        using (var inputHooks = new Momonga.Platform.AnonymousActivityMonitor()) inputHooks.Drain();
#endif
        var data = new SaveData(); data.Zone.X = data.Zone.Y = 0;
        var life = new LifeSimulation(data, character, catalog);
        foreach (var id in new[] { "food-bowl", "water-bowl", "cushion" }) Require(life.Shop.Place(id) != null, "Starter furniture missing");
        for (var i = 0; i < data.Items.Count; i++) { data.Items[i].X = 20 + i * 90; data.Items[i].Y = 40; }
        Require(life.Shop.Place("food-bowl") == null, "Unowned duplicate furniture placed");
        Require(!life.Shop.Consume("food-bowl"), "Furniture consumed");
        var food = data.Items.First(i => i.ItemId == "food-bowl");
        var bedTarget = data.Items.First(i => i.ItemId == "cushion");
        Require(life.UseItem(bedTarget), "Explicit cushion use failed");
        bedTarget.X += 50; life.Update(0, new Point(600, 300), false);
        Require(life.Destination == life.FurnitureTarget(bedTarget), "Moving cushion left pet target behind");
        Require(life.Shop.FillFood(food, "meal") && life.Shop.Quantity("meal") == 1, "Food fill did not consume inventory");
        Require(food.FoodQuantity == 1 && !life.Shop.FillFood(food, "meal") && life.Shop.Quantity("meal") == 1, "One meal failed to fill bowl or full bowl consumed inventory");
        Require(life.Habitat.SizeOf(food) == life.Habitat.SizeOf(bedTarget), "Furniture sizes are not linked");
#if !CROSS_PLATFORM
        Require(!data.Settings.DirectTouch && !speechAnchor.DirectTouch(), "Direct touch should be opt-in");
        for (var side = 0; side < 2; side++)
        {
            var previous = double.NegativeInfinity;
            for (var x = -40; x <= 180; x++)
            {
                var mapped = CheekSurface.MapX(x, side == 0 ? 25 : 83, side == 0 ? 46 : 64, 32, side == 0);
                Require(mapped > previous, "Cheek deformation folded over itself"); previous = mapped;
            }
        }
#endif
        data.Pet.Hunger = 90; life.Habitat.PetCenter = life.Habitat.Center(food); life.RefreshContext();
        Require(life.Brain.Force("Eat", life.Context), "Eating unavailable with food");
        for (var i = 0; i < 7; i++) life.Update(1, new Point(600, 300), false);
        Require(food.FoodQuantity == 0 && data.Pet.Hunger < 65, "Pet did not eat at bowl");
        var water = data.Items.First(i => i.ItemId == "water-bowl");
        data.Pet.Thirst = 90; life.Habitat.PetCenter = life.Habitat.Center(water); life.RefreshContext(); life.Brain.Force("Drink", life.Context);
        for (var i = 0; i < 5; i++) life.Update(1, new Point(600, 300), false);
        Require(water.Water == 60 && data.Pet.Thirst < 60, "Drinking did not use water");
        var partialData=new SaveData(); var partialLife=new LifeSimulation(partialData,character,catalog);
        var partialBowl=partialLife.Shop.Place("food-bowl")!;
        Require(partialLife.Shop.FillFood(partialBowl,"meal"),"Partial meal setup failed");
        partialData.Pet.Hunger=25; partialLife.Habitat.PetCenter=partialLife.FurnitureTarget(partialBowl);
        Require(partialLife.UseItem(partialBowl),"Manual small meal unavailable");
        var mealSpeech=0; partialLife.Speech+=(tag,auto)=>{ if(tag=="Meal:meal"&&auto) mealSpeech++; };
        for(var i=0;i<7;i++) partialLife.Update(1,new Point(),false);
        Require(partialBowl.FoodQuantity==1 && partialBowl.FoodPortion is > .6 and < .7 && partialData.Pet.Hunger<6 && mealSpeech==1,"Pet ate more than needed or failed to finish speech once");
        var remaining=partialBowl.FoodPortion;
        var state=System.Text.Json.JsonSerializer.Deserialize<SaveData>(System.Text.Json.JsonSerializer.Serialize(partialData))!;
        SaveService.Validate(state,catalog); Require(state.Items[0].FoodPortion==remaining,"Leftover portion lost in saved data");
        partialData.Inventory["meal"]=0;
        Require(partialLife.Shop.ReturnFood(partialBowl) && partialLife.Shop.Quantity("meal")==1 && partialLife.Shop.FillFood(partialBowl,"meal") && partialBowl.FoodPortion==remaining,"Returning bowl duplicated or discarded leftover food");
        partialData.Pet.Hunger=20; partialData.Pet.Thirst=0; partialData.Pet.Energy=partialData.Pet.Fun=partialData.Pet.Social=100;
        partialLife.Rest(0); for(var i=0;i<20;i++) partialLife.Update(1,new Point(),false);
        Require(partialBowl.FoodPortion==remaining && partialLife.Brain.Current.Name!="Eat","Slight hunger caused automatic grazing");
        var talkKind=""; partialLife.Speech+=(tag,auto)=>{ if(!auto)talkKind=tag; };
        var actionBefore=partialLife.Brain.Current.Name; var annoyanceBefore=partialData.Pet.Annoyance;
        partialLife.Interact("Talk"); Require(talkKind=="IdleTalk" && partialLife.Brain.Current.Name==actionBefore && partialData.Pet.Annoyance==annoyanceBefore,"Click conversation annoyed pet or interrupted action");
        Require(catalog["meal"].Utensil=="Chopsticks" && catalog["jiro-ramen"].Utensil=="Chopsticks" && catalog["dessert"].Utensil=="Spoon","Utensils disagree with food animations");
        var autoData = new SaveData(); var autonomous = new LifeSimulation(autoData, character, catalog);
        var autoFood = autonomous.Shop.Place("food-bowl")!; autoFood.FoodQuantity = 1;
        var autoWater = autonomous.Shop.Place("water-bowl")!; autoWater.Water = 100;
        autoData.Pet.Hunger = 95; autoData.Pet.Thirst = 0; autoData.Pet.Energy = autoData.Pet.Social = autoData.Pet.Fun = 100;
        autonomous.Habitat.PetCenter = autonomous.Habitat.Center(autoFood);
        for (var i = 0; i < 16; i++) autonomous.Update(1, new Point(900, 400), false);
        Require(autoFood.FoodQuantity == 0 && autoData.Pet.Hunger < 70, "Filled food failed autonomous eating");
        autoData.Pet.Hunger = 0; autoData.Pet.Thirst = 95; autonomous.Brain.Force("Idle", autonomous.Context);
        autonomous.Habitat.PetCenter = autonomous.Habitat.Center(autoWater);
        for (var i = 0; i < 16; i++) autonomous.Update(1, new Point(900, 400), false);
        Require(autoWater.Water < 100 && autoData.Pet.Thirst < 70, "Filled water failed autonomous drinking");
        autoFood.FoodQuantity = 0; autoWater.Water = 0; autoData.Pet.Hunger = 95; autoData.Pet.Thirst = 0;
        autonomous.Brain.Cooldowns.Clear(); autonomous.RefreshContext(); autonomous.Brain.Force("Idle", autonomous.Context);
        var emptyRequest = 0; autonomous.Speech += (tag, auto) => { if (auto && tag == "Hungry") emptyRequest++; };
        for (var i = 0; i < 20; i++) autonomous.Update(1, new Point(900, 400), false);
        Require(emptyRequest > 0, "Empty food bowl failed autonomous request");
        autoData.Pet.Hunger = 0; autoData.Pet.Thirst = 95; autonomous.Brain.Cooldowns.Clear(); autonomous.RefreshContext(); autonomous.Brain.Force("Idle", autonomous.Context);
        var emptyWaterRequest = 0; autonomous.Speech += (tag, auto) => { if (auto && tag == "Thirsty") emptyWaterRequest++; };
        for (var i = 0; i < 20; i++) autonomous.Update(1, new Point(900, 400), false);
        Require(emptyWaterRequest > 0, "Empty water bowl failed autonomous request");
        var sitData = new SaveData(); var sitting = new LifeSimulation(sitData, character, catalog);
        var cushion = sitting.Shop.Place("cushion")!; sitting.Habitat.PetCenter = sitting.FurnitureTarget(cushion);
        Require(sitting.SeatOnBed() && sitting.Brain.Current.Name == "SitOnBed", "Cushion drop failed to sit");
        sitting.Interact("Pet"); for (var i = 0; i < 4; i++) sitting.Update(1, new Point(900, 400), false);
        Require(sitting.Brain.Current.Name == "SitOnBed", "Petting abandoned cushion instead of resuming sitting");
        sitData.Pet.Energy = 0; for (var i = 0; i < 12; i++) sitting.Update(1, new Point(900, 400), false);
        Require(sitting.Brain.Current.Name == "Sleep", "Seated pet could not autonomously sleep");
        var beforeMood = data.Pet.Mood; var beforeHunger = data.Pet.Hunger;
        Require(life.GiveSnack("cookie") && life.Shop.Quantity("cookie") == 0, "Snack was not consumed");
        Require(data.Pet.Mood - beforeMood > beforeHunger - data.Pet.Hunger, "Snack acted like ordinary food");
        Require(!life.GiveSnack("cookie"), "Zero-quantity snack reused");
        var balance = data.ActivityPoints;
        Require(life.Shop.Buy("nuts") && data.ActivityPoints == balance - catalog["nuts"].Price && life.Shop.Quantity("nuts") == 1, "Purchase was not atomic");
        data.ActivityPoints = 0; Require(!life.Shop.Buy("nuts") && life.Shop.Quantity("nuts") == 1, "Unaffordable purchase changed inventory");
        for (var i = 0; i < 8; i++) life.Interact("Flick");
        Require(data.Pet.Annoyance > 70 && life.Brain.Current.Name == "Sulk", "Repeated teasing did not escalate to sulking");
        var annoyance = data.Pet.Annoyance; life.Interact("Pet"); Require(data.Pet.Annoyance < annoyance, "Petting did not soothe");
        var sign = data.Items.First(i => i.ItemId == "cushion");
        var autoSpeech = 0; life.Speech += (_, autonomous) => { if (autonomous) autoSpeech++; };
        sign.Active = true; life.Habitat.PetCenter = new Point(600, 300); life.RefreshContext();
        Require(!life.Habitat.Quiet && !life.Habitat.Inside, "Sign outside pet enabled Quiet Habitat");
        data.Pet.Annoyance = 0; life.Brain.Force("AskFood", life.Context);
        life.Habitat.PetCenter = new Point(200, 150); life.Update(1, new Point(600, 300), false);
        Require(life.Quiet && !life.Brain.Current.Name.StartsWith("Ask"), "Quiet Habitat failed to cancel existing request");
        foreach (var name in new[] { "AskFood", "AskWater", "AskSnack", "AskAttention" })
            Require(!life.Brain.Force(name, life.Context), "Quiet Habitat admitted " + name);
        var hunger = data.Pet.Hunger;
        for (var i = 0; i < 3600; i++) life.Update(1, new Point(600, 300), false);
        Require(autoSpeech == 0 && data.Pet.Hunger > hunger && data.Pet.Social < 35 && data.Pet.Stress > 5,
            "Quiet Habitat paused life, interrupted user, or missed long-duration trade-off");
        sign.Active = false; life.Update(1, new Point(600, 300), false); Require(!life.Quiet, "Removing sign failed to exit quiet");
        sign.Active = true; life.Habitat.PetCenter = new Point(600, 300); life.Update(1, new Point(600, 300), false);
        Require(!life.Quiet, "Dragging pet outside failed to exit quiet");
        life.RefreshContext(true); Require(!life.Brain.Force("AskFood", life.Context), "Fullscreen courtesy allowed spontaneous request");
        var economy = new SaveData(); var ap = new ActivityPointService(economy);
        var wall = economy.HourStarted;
        for (var i = 0; i < 120; i++) ap.Update(1, false, wall.AddSeconds(i));
        Require(economy.ActivityPoints == 15000, "AFK earned points");
        for (var i = 0; i < 3599; i++) ap.Update(1, true, wall.AddSeconds(i), 1, 1);
        Require(economy.ActivityPoints == 22198 && economy.HourPoints == 7198, "Hourly AP limit was not removed");
        var events = new SaveData(); var eventAP = new ActivityPointService(events);
        eventAP.Update(0.03, true, wall, 3, 2);
        Require(events.ActivityPoints == 15005, "Key and click counts did not immediately award AP");
        eventAP.Update(1, true, wall.AddSeconds(1));
        Require(events.ActivityPoints == 15005, "Active time alone awarded AP");
        eventAP.Update(0.03, true, wall.AddSeconds(2), 10000, 10000);
        eventAP.Update(0.03, true, wall.AddSeconds(2), 10000, 10000);
        Require(events.ActivityPoints == 15035, "Repeated burst bypassed AP cap");
        eventAP.Update(0.03, true, wall.AddHours(1), 1, 0);
        Require(events.HourPoints == 1, "Hourly AP reset failed");
        var legacy = new SaveData { Version = 1, ActivityPoints = 150 };
        legacy.Items.Add(new HabitatItem { ItemId = "food-bowl", X = 25, Y = 35 });
        SaveService.Validate(legacy, catalog);
        Require(legacy.Version == 3 && legacy.ActivityPoints == 15000, "Old AP balance migration failed");
        Require(legacy.Items[0].X == legacy.Zone.X + 25 && legacy.Items[0].Y == legacy.Zone.Y + 35, "Furniture world-position migration failed");
        legacy.Items[0].X = -100; legacy.Items[0].Y = 1200; SaveService.Validate(legacy, catalog);
        Require(legacy.Items[0].X == -100 && legacy.Items[0].Y == 1200, "Furniture remained constrained to old home");
        legacy.Items[0].FoodQuantity = 5; var storedMeals = legacy.Inventory["meal"];
        SaveService.Validate(legacy, catalog); SaveService.Validate(legacy, catalog);
        Require(legacy.Items[0].FoodQuantity == 1 && legacy.Inventory["meal"] == storedMeals + 4, "Bowl migration lost or duplicated meals");
        var offline = new PetState(); offline.Offline(TimeSpan.FromDays(365));
        Require(offline.Hunger <= 50 && offline.Social >= 30 && offline.Energy >= 35, "Long absence punished pet excessively");
        var dialogue = new DialogueService(data); var line = dialogue.Pick("SnackRequest");
        Require(dialogue.Pick("SnackRequest") != line, "Immediate dialogue repetition");
        var requestLife = new LifeSimulation(new SaveData(), character, catalog);
        requestLife.Data.Pet.Hunger = 95; requestLife.RefreshContext(); requestLife.Brain.Force("AskFood", requestLife.Context);
        var requests = 0; requestLife.Speech += (tag, autonomous) => { if (tag == "Hungry" && autonomous) requests++; };
        for (var i = 0; i < 60; i++) requestLife.Update(1, new Point(500, 200), false);
        Require(requests == 2, "Request repeated more than once or lost its retry");
        Require(PetInteractionDetector.Region(new Point(64, 20), 128, 136) == HitRegion.Head, "Head hit region incorrect");
        Require(PetInteractionDetector.Region(new Point(64, 100),128,136) == HitRegion.Chin, "Chin hit region incorrect");
        Require(catalog["free-rice"].Price == 0 && catalog["free-rice"].Hunger >= 40 && catalog["pudding"].Category == "Snack" && catalog["curry-rice"].Utensil == "Spoon", "Food category, utensils or emergency meal incorrect");
        Require(catalog.Values.Where(i => i.Category == "Snack").Select(i => i.AnimationFrame).Distinct().Count() == catalog.Values.Count(i => i.Category == "Snack"), "Snacks share generic animation");
        var playData = new SaveData(); var playLife = new LifeSimulation(playData,character,catalog); playData.Pet.Fun = 10;
        playLife.StartPlay();
        for (var i = 0; i < 30; i++) { if (playLife.Destination.HasValue) playLife.Habitat.PetCenter = playLife.Destination.Value; playLife.Update(1,new Point(),false); }
        Require(playLife.Playing && playLife.Brain.Current.Name == "Play", "Play ended before satisfaction");
        playData.Pet.Fun = 95; playLife.Update(0,new Point(),false); Require(!playLife.Playing,"Satisfied pet did not stop play");
        playData.Pet.Fun = 20; playLife.StartPlay(); playLife.StopPlay(); Require(!playLife.Playing && playLife.Brain.Current.Name == "Idle", "Manual stop failed");
        playLife.StartPlay(); Require(playLife.Brain.Force("Sleep", playLife.Context), "Sleep could not interrupt play");
        playLife.Update(0, new Point(), false);
        Require(!playLife.Playing && playLife.Brain.Current.Name == "Sleep", "Previous play state cancelled the selected sleep action");
        var path = Path.GetFullPath("obj/self-tests/" + Guid.NewGuid().ToString("N") + "/save.json");
        var save = new SaveService(path); data.Settings.PetScale = 1.5; data.CharacterId = "usagi";
        save.SaveAsync(data).GetAwaiter().GetResult(); save.SaveAsync(data).GetAwaiter().GetResult();
        var restored = save.Load(catalog);
        Require(restored.CharacterId == "usagi" && restored.ActivityPoints == data.ActivityPoints, "Character identity or AP lost during save");
        Require(restored.Items.Count == 3 && restored.Settings.PetScale == 1.5 && restored.Inventory["nuts"] == 1 && File.Exists(path + ".bak"), "Atomic save round trip failed");
        File.WriteAllText(path, "{ corrupt"); var recovered = save.Load(catalog);
        Require(recovered.ActivityPoints == 15000 && Directory.GetFiles(Path.GetDirectoryName(path)!, "*.corrupt-*.json").Length == 1, "Corrupt save recovery lost backup");
#if !CROSS_PLATFORM
        var themeReference = Theme.Brush("Accent");
        Theme.Apply(character.Theme with { Accent = "#226E78" });
        Require(((System.Windows.Media.SolidColorBrush)Theme.Brush("Accent")).Color.ToString() == "#FF226E78", "Character theme did not apply");
        Require(ReferenceEquals(themeReference, Theme.Brush("Accent")), "Existing windows lost live theme reference");
        Theme.Apply(character.Theme);
#endif
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
