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
    internal static void CheckCheek()
    {
        var drag=new CheekDrag();
        var marked=new byte[100*100*4];for(var i=0;i<marked.Length;i+=4){marked[i]=240;marked[i+1]=245;marked[i+2]=250;marked[i+3]=255;}
        var eyeY=(int)((CheekDrag.CheekY-.085)*100);
        var eye=(eyeY*100+30)*4;marked[eye]=marked[eye+1]=marked[eye+2]=20;
        drag.Begin(new Point(CheekDrag.Cheeks.Left,CheekDrag.CheekY),true);drag.Move(new Vector(-.20,.17));
        var layers=drag.Warp(marked,100,100);var fixedEye=((eyeY+50)*200+30+50)*4;
        Require(layers[fixedEye]==20&&layers[fixedEye+3]==255,"Expression layer stretched with the cheek");
        var pixels=new byte[100*100*4];for(var i=0;i<pixels.Length;i+=4){pixels[i]=70;pixels[i+1]=100;pixels[i+2]=120;pixels[i+3]=255;}
        for(var side=0;side<2;side++)
        for(var angle=0;angle<8;angle++)
        {
            drag.Begin(new Point(side==0?.30:.68,.59),side==0);
            drag.Move(new Vector(Math.Cos(angle*Math.PI/4)*.23,Math.Sin(angle*Math.PI/4)*.23));
            var warped=drag.Warp(pixels,100,100);var ear=((20+50)*200+50+50)*4;
            Require(warped[ear]==70&&warped[ear+3]==255,"Cheek pull moved an ear");
            var initial=drag.Pull;drag.Release(10);drag.Update(10);
            Require((drag.Pull-initial).Length<.0001,"Release jumped instead of starting from the held pose");
            drag.Update(10.15);
            Require(drag.Pull.X*initial.X+drag.Pull.Y*initial.Y<0,"Cheek release had no opposite bounce");
            drag.Update(10.9);Require(drag.Pull.Length==0&&!drag.Active(10.9),"Cheek never settled");
        }
        drag.Begin(new Point(.18,.56),true);var edge=drag.Radius;drag.Begin(new Point(.34,.60),true);
        Require(drag.Radius>edge && Math.Abs(drag.Grab.Y-Math.Clamp(.60,CheekDrag.CheekY-.04,CheekDrag.CheekY+.04))<.0001,"Grab location or amount of skin was discarded");
        drag.Move(new Vector(-100,100));Require(drag.Strength<.381,"Extreme cheek drag was unbounded");
        drag.Begin(new Point(.30,.56),true);drag.Release(0);Require(!drag.Active(0),"Click without pulling played a release reaction");
    }
    public static void Run()
    {
        Momonga.Character.LayeredFrame.RunChecks();
        Momonga.Updates.UpdateService.RunChecks();
        CheckBall();
        CheckPlayground();
        CheckBeer();
        CheckPlayBalance();
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
            speech.RevealAll(); speech.Dispatcher.Invoke(speech.RefreshSettings, System.Windows.Threading.DispatcherPriority.ApplicationIdle); var shortHeight = speech.Height;
            speech.Say(string.Join(" ", Enumerable.Repeat("A long bubble must wrap onto multiple lines.", 20)), speechAnchor); speech.RevealAll();
            speech.Dispatcher.Invoke(speech.RefreshSettings, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Require(speech.Width <= 276 && speech.Height > shortHeight, $"Speech bubble failed width cap or vertical wrapping: {speech.Width} x {speech.Height}, short {shortHeight}, work {Momonga.Platform.MonitorService.At(speechAnchor, new Point(400,400)).Work}");
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
#endif
        CheckCheek();
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
        var quietSocial = data.Pet.Social;
        for (var i = 0; i < 3600; i++) life.Update(1, new Point(600, 300), false);
        Require(autoSpeech == 0 && data.Pet.Hunger > hunger && data.Pet.Social < quietSocial && data.Pet.Stress > 5,
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
        var dekatsuyo = CharacterDefinition.Load("dekatsuyo");
        var dekaDialogue = new DialogueService(new SaveData(), dekatsuyo.DialogueSetId);
        foreach (var tag in new[] {"Hungry","Thirsty","SnackRequest","Happy","Annoyed","Sleepy","Greeting","ReturnGreeting","PetReaction","FlickReaction","IdleTalk","Attention","Meal","Snack","WaterReaction","PraiseReaction","SootheReaction","RefuseReaction","WakeReaction"})
        {
            var first = dekaDialogue.Pick(tag); var second = dekaDialogue.Pick(tag);
            Require(first != "…" && second != "…" && first != second, "Missing or repeating Dekatsuyo speech: " + tag);
        }
        var requestLife = new LifeSimulation(new SaveData(), character, catalog);
        requestLife.Data.Pet.Hunger = 95; requestLife.RefreshContext(); requestLife.Brain.Force("AskFood", requestLife.Context);
        var requests = 0; requestLife.Speech += (tag, autonomous) => { if (tag == "Hungry" && autonomous) requests++; };
        for (var i = 0; i < 60; i++) requestLife.Update(1, new Point(500, 200), false);
        Require(requests == 2, "Request repeated more than once or lost its retry");
        Require(PetInteractionDetector.Region(new Point(55, 49), 128, 136) == HitRegion.Head, "Head hit region incorrect");
        var chin = CharacterAnatomy.Current.Area("Chin").Center;
        Require(PetInteractionDetector.Region(new Point(chin.X*128,chin.Y*136),128,136) == HitRegion.Chin, "Calibrated chin hit region incorrect");
        Require(catalog["free-rice"].Price == 0 && catalog["free-rice"].Hunger >= 40 && catalog["pudding"].Category == "Snack" && catalog["curry-rice"].Utensil == "Spoon", "Food category, utensils or emergency meal incorrect");
        Require(catalog.Values.Where(i => i.Category == "Snack").Select(i => i.AnimationFrame).Distinct().Count() == catalog.Values.Count(i => i.Category == "Snack"), "Snacks share generic animation");
        var playData = new SaveData(); var playLife = new LifeSimulation(playData,character,catalog); playData.Pet.Fun = 10;
        playLife.StartPlay();
        for (var i = 0; i < 30; i++) { if (playLife.Destination.HasValue) playLife.Habitat.PetCenter = playLife.Destination.Value; playLife.Update(1,new Point(),false); }
        Require(playLife.Playing && playLife.Brain.Current.Name == "Play", "Play ended before satisfaction");
        playData.Pet.Fun = 100; playLife.Update(0,new Point(),false); Require(playLife.Playing,"Satisfied play ended before one minute");
        for(var i=0;i<30;i++)playLife.Update(1,new Point(),false);
        Require(!playLife.Playing,"Satisfied pet did not stop play after one minute");
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
    private static void CheckBeer()
    {
        var catalog=ItemCatalog.Load();
        foreach(var id in new[]{"kurimanju","momonga"})
        {
            var data=new SaveData(); data.Inventory["beer"]=1; data.Pet.Fun=10; data.Pet.Energy=10;
            var life=new LifeSimulation(data,CharacterDefinition.Load(id),catalog); var spoken=0; var consumed=0;
            life.Speech+=(tag,_)=>{if(tag=="Snack:beer")spoken++;};life.Consumed+=_=>consumed++;
            Require(life.GiveSnack("beer") && consumed==1 && data.Inventory["beer"]==0,"Beer consumption failed");
            if(id=="kurimanju")
            {
                Require(data.Pet.Fun==80 && data.Pet.Energy==75 && spoken==0,"Kurimanju beer boost or toast timing failed");
                life.Update(1,new Point(),false);Require(spoken==0,"Beer toast happened before drinking");
                life.Update(.6,new Point(),false);life.Update(.6,new Point(),false);Require(spoken==1,"Beer toast failed or repeated");
            }
            else Require(data.Pet.Energy==10 && data.Pet.Fun<80 && spoken==1,"Kurimanju beer bonus leaked to another character");
        }
    }
    private static void CheckPlayBalance()
    {
        var pet=new PetState{Fun=75,Social=75};pet.Update(60);
        Require(Math.Abs(pet.Fun-74.8)<.001 && Math.Abs(pet.Social-74.4)<.001,"Fun/attention decay is still too fast");
        pet.Offline(TimeSpan.FromSeconds(1));Require(pet.Fun>74 && pet.Social>74,"Brief offline time abruptly emptied fun/attention");
        var data=new SaveData();data.Items.Add(new HabitatItem{ItemId="ball"});data.Inventory["ball"]=1;
        var life=new LifeSimulation(data,CharacterDefinition.Load(),ItemCatalog.Load());
        Require(!data.Items.Any(i=>i.ItemId=="ball") && life.Shop.Quantity("ball")==1 && life.Shop.Place("ball")==null,"Ball remained permanent furniture or lost ownership");
        life.Habitat.PetCenter=new Point(500,400);data.Pet.Fun=100;data.Pet.Social=10;
        life.StartPlayground(PlaygroundMode.Snacks);life.Playground.CatchChance=1;data.SimulationSeconds+=120;
        for(var round=0;round<2;round++)
        {
            life.Playground.BeginAim(new Point(420,400));life.Playground.Pull(new Point(360,400));life.Playground.ReleaseAim();
            for(var i=0;i<30;i++)life.Update(.033,new Point(),false);
            Require(life.Playground.Active && life.Playground.Hits==round+1,"Snack tossing ended after catching or when fun was full");
        }
        Require(data.Pet.Social>10,"Snack play did not replenish attention");life.StopPlay();
        data.Pet.Fun=20;data.Pet.Social=10;life.StartBallPlay();life.Playground.BeginAim(new Point(250,100));life.Playground.Pull(new Point(190,100));life.Ball.Grab();life.Playground.ReleaseBall(life.Ball);
        var y=life.Ball.Position.Y;life.Update(.1,new Point(),false);Require(life.Ball.Position.X>190 && life.Ball.Position.Y==y && !life.Playground.Aiming,"Slingshot ball did not travel straight");
        life.Ball.Grab();life.Ball.Drag(life.Habitat.PetCenter);life.Ball.Throw(new Vector());var fun=data.Pet.Fun;var social=data.Pet.Social;
        life.Update(.5,new Point(),false);Require(data.Pet.Fun-fun is >1 and <2 && data.Pet.Social>social,"Ball reward is too fast or does not replenish attention");
        life.StopPlay();Require(!life.Ball.Active && !life.Playground.Active,"Ball stop left a toy or throwing overlay alive");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void CheckBall()
    {
        var ball = new BallGame { Bounds = new Rect(-600,-400,600,400) };
        var actor = new Size(128,136); var home = new Point(-300,-200);
        ball.Start(home,1); Require(ball.Grab(),"Ball could not be grabbed");
        ball.Drag(new Point(-15,-15)); ball.Throw(new Vector(1200,1200));
        ball.Update(.1,home,actor,false);
        Require(ball.Position.X < -20 && ball.Position.Y < -20,"Ball did not bounce off both monitor edges");
        for(var i=0;i<60;i++) ball.Update(.1,home,actor,false);
        Require(ball.Position.X>=-586 && ball.Position.X<=-14 && ball.Position.Y>=-386 && ball.Position.Y<=-14,"Ball escaped negative-coordinate monitor");
        ball.Start(home,1); ball.Grab(); ball.Drag(new Point(-100,-150)); ball.Throw(new Vector());
        var reward=ball.Update(.5,new Point(-100,-150),actor,false);
        Require(ball.Phase==BallPhase.Catching && reward>0,"Pet did not catch the ball");
        ball.Update(.5,new Point(-100,-150),actor,false);
        Require(ball.Phase==BallPhase.Returning && ball.Target==home,"Pet did not bring the ball home");
        ball.Update(.1,home,actor,false); Require(ball.Phase==BallPhase.Ready,"Returned ball did not become throwable again");
        ball.Finish(); ball.Update(.5,home,actor,false); Require(ball.BehindPet && ball.Opacity<1,"Satisfied pet did not hide the ball behind its back");
        ball.Update(.7,home,actor,false); Require(!ball.Active,"Hidden ball did not finish play");
        ball.Start(home,1); ball.Cancel(); Require(!ball.Active,"Ball stop failed");
        var life = new LifeSimulation(new SaveData(),CharacterDefinition.Load(),ItemCatalog.Load());
        life.Habitat.PetCenter=new Point(300,200);life.Data.Pet.Fun=20;
        Require(life.StartBallPlay() && life.Playing && life.InteractionItem==null,"Ball play still requires furniture or a purchase");
        life.Data.Pet.Fun=100;life.Update(.2,home,false);Require(life.Ball.Phase!=BallPhase.Hiding,"Ball ended immediately when already happy");
        life.Data.SimulationSeconds+=60;life.Update(.2,home,false);
        Require(life.Ball.Phase==BallPhase.Hiding,"Satisfaction did not start the ending animation");
        life.Update(1,home,false);Require(!life.Playing && !life.Ball.Active,"Ball ending left pet playing");
        life.StartBallPlay();life.StopPlay();Require(!life.Playing && !life.Ball.Active,"Manual stop left the ball alive");
        life.Data.Pet.Fun=20;life.StartBallPlay();
        for(var i=0;i<54000 && life.Ball.Active;i++)
        {
            if(life.Ball.Phase==BallPhase.Ready)life.Ball.Throw(new Vector(380,90));
            life.Update(1.0/30,home,false);
            if(life.Destination is Point target)
            {
                var offset=(Vector)(target-life.Habitat.PetCenter);
                if(offset.Length>.01) life.Habitat.PetCenter+=offset*(Math.Min(offset.Length,210.0/30)/offset.Length);
            }
        }
        Require(!life.Playing && !life.Ball.Active && life.Data.Pet.Fun>=94,"Autonomous fetch loop failed to satisfy the pet and finish");
    }
    private static void CheckPlayground()
    {
        var pet=new Point(500,400);var size=new Size(128,136);
        var game=new PlaygroundGame {Bounds=new Rect(0,0,1000,800)};
        game.Start(PlaygroundMode.Bubbles,1,pet);game.Blow(new Point(800,200));game.Blow(new Point(800,200));
        Require(game.Particles.Count==5,"Bubble click was not a throttled burst");
        game.Update(.05,pet,size,false);Require(game.Target.HasValue,"Pet did not target a bubble");
        var before=game.Particles[0].Position;game.Update(1,pet,size,true);
        Require(game.Particles[0].Position==before,"Paused bubbles kept drifting");
        var hits=0;
        for(var i=0;i<300;i++)
        {
            hits+=game.Update(1.0/30,pet,size,false);
            if(game.Target is Point target) {var d=(Vector)(target-pet);if(d.Length>.01)pet+=d*(Math.Min(d.Length,210.0/30)/d.Length);}
        }
        Require(hits>0 && game.Particles.Count==0,"Bubble chase did not pop and clean up particles");
        pet=new Point(500,400);game.Start(PlaygroundMode.Snacks,1,pet);
        game.BeginAim(new Point(380,410));game.Pull(new Point(300,410));game.ReleaseAim();
        game.Update(.033,pet,size,false);
        Require(game.Target.HasValue && ((Vector)(game.Target.Value-pet)).Length<=60.001,"Snack prediction did not make a bounded anticipation step");
        hits=0;
        for(var i=0;i<100;i++)
        {
            hits+=game.Update(1.0/30,pet,size,false);
            if(game.Target is Point target) {var d=(Vector)(target-pet);if(d.Length>.01)pet+=d*(Math.Min(d.Length,85.0/30)/d.Length);}
        }
        Require(hits==1 && game.Particles.Count==0,"Predicted snack was not caught exactly once");
        game.Start(PlaygroundMode.Snacks,1,pet);game.BeginAim(new Point(120,100));game.Pull(new Point(100,100));game.ReleaseAim();
        game.Update(.05,pet,size,false);Require(!game.Target.HasValue,"Pet chased a distant missed snack");
        for(var i=0;i<40;i++)game.Update(.1,pet,size,false);
        Require(game.Hits==0 && game.Misses==1 && game.Particles.Count==0,"Missed snack did not disappear without feeding");
        Require(game.WatchTarget==null && game.Deadpan,"Miss did not end in a front-facing deadpan reaction");
        game.CatchChance=0;game.Start(PlaygroundMode.Snacks,1,new Point(500,400));game.BeginAim(new Point(420,400));game.Pull(new Point(360,400));game.ReleaseAim();
        Require(game.WatchTarget.HasValue,"Pet did not watch a flying treat");
        for(var i=0;i<30;i++)game.Update(.033,new Point(500,400),size,false);
        Require(game.Hits==0 && game.Bonks==1 && game.Deadpan,"Failed catch did not bonk then deadpan");game.CatchChance=1;
        game.Start(PlaygroundMode.Snacks,.5,new Point(400,200));game.BeginAim(new Point(240,200));game.Pull(new Point(100,200));game.ReleaseAim();
        Require(game.Update(.5,new Point(400,200),new Size(64,68),false)==1,"Fast snack skipped a small pet");
        game.Stop();Require(!game.Active && game.Particles.Count==0 && !game.Aiming,"Playground cancellation retained projectiles or capture state");
        var life=new LifeSimulation(new SaveData(),CharacterDefinition.Load(),ItemCatalog.Load());
        life.Habitat.PetCenter=new Point(500,400);life.Data.Pet.Fun=20;
        life.StartPlayground(PlaygroundMode.Snacks);var hunger=life.Data.Pet.Hunger;var consumed=0;
        life.Playground.CatchChance=1;
        life.Consumed+=_=>consumed++;life.Playground.BeginAim(new Point(420,400));life.Playground.Pull(new Point(360,400));life.Playground.ReleaseAim();
        for(var i=0;i<30;i++)life.Update(.033,pet,false);
        Require(consumed==1 && life.Data.Pet.Hunger<hunger,"Snack hit did not feed or animate eating");
        life.StartPlayground(PlaygroundMode.Snacks);life.Playground.CatchChance=0;var bonks=0;hunger=life.Data.Pet.Hunger;
        life.ActionAnimation+=kind=>{if(kind=="SnackBonk")bonks++;};life.Playground.BeginAim(new Point(420,400));life.Playground.Pull(new Point(360,400));life.Playground.ReleaseAim();
        for(var i=0;i<30;i++)life.Update(.033,pet,false);
        Require(bonks==1 && consumed==1 && life.Data.Pet.Hunger>=hunger,"Head bonk fed the pet or failed to animate");
        Require(CharacterDefinition.All.Select(c=>c.SnackCatchChance).Distinct().Count()>2 && CharacterDefinition.Load("rakko").SnackCatchChance==1,"Catch probabilities did not differ by character");
        life.StartPlayground(PlaygroundMode.Bubbles);life.Data.Pet.Fun=100;life.Playground.Blow(life.Habitat.PetCenter);life.Update(.05,pet,false);
        Require(life.Playground.Active,"Bubbles ended after the first burst");
        life.Data.SimulationSeconds+=60;life.Update(.05,pet,false);
        Require(!life.Playing && !life.Playground.Active,"Satisfied pet did not end bubble play");
        life.StartPlayground(PlaygroundMode.Snacks);life.StopPlay();Require(!life.Playground.Active,"Manual stop retained snack play");
    }
}
