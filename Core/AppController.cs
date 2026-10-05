using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Momonga.AI;
using Momonga.Animation;
using Momonga.Character;
using Momonga.Content;
using Momonga.Economy;
using Momonga.Inventory;
using Momonga.Persistence;
using Momonga.Platform;
using Momonga.Simulation;
using Momonga.UI;

namespace Momonga.Core;

public sealed class AppController : IDisposable
{
    private readonly PetWindow pet;
    private readonly PetMotion motion;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Stopwatch clock = new();
    private readonly TrayService tray;
    private readonly SaveService save;
    private readonly LifeSimulation life;
    private readonly ActivityPointService points;
    private readonly AnonymousActivityMonitor? activity;
    private readonly DialogueService dialogue;
    private readonly SpeechBubbleWindow bubble;
    private readonly Dictionary<string, HabitatItemWindow> itemWindows = new();
    private readonly Dictionary<string, Window> panels = new();
    private RadialMenuWindow? radial;
    private BallWindow? ballWindow;
    private PlaygroundWindow? playgroundWindow;
    private DebugWindow? debug;
    private double lastTick, nextSense, nextSave = 60, bubbleUntil, nextStartle, nextAutonomousSpeech;
    private bool active, fullscreen, hidden, finishing, bubbleAutonomous;
    private string feedback = "";
    private Point cursorWorld;
    public LifeSimulation Life => life;

    public AppController(bool testMode = false, SaveData? existingData = null)
    {
        var catalog = ItemCatalog.Load();
        save = new SaveService(testMode ? Path.GetFullPath("obj/ui-check/save.json") : null);
        var data = existingData ?? (testMode ? new SaveData() : save.Load(catalog));
        var character = CharacterDefinition.Load(data.CharacterId); Theme.Apply(character.Theme); CharacterSprites.Select(character.AssetSet);
        life = new LifeSimulation(data, character, catalog);
        motion = new PetMotion(character: character); points = new ActivityPointService(data); dialogue = new DialogueService(data, character.DialogueSetId);
        if (!testMode) activity = new AnonymousActivityMonitor();
        pet = new PetWindow(character.AssetSet) { Title = character.DisplayName + " · Companion" }; pet.SetScale(data.Settings.PetScale);
        pet.DirectTouch = () => data.Settings.DirectTouch;
        var work = SystemParameters.WorkArea;
        var monitors = MonitorService.Areas(pet);
        if (!monitors.Any(m => m.Id == data.Zone.MonitorId && m.Work.IntersectsWith(new Rect(data.Zone.X, data.Zone.Y, data.Zone.Width, data.Zone.Height))))
        {
            data.Zone.X = work.Left + 40; data.Zone.Y = Math.Max(work.Top, work.Bottom - data.Zone.Height - 20);
            data.Zone.MonitorId = monitors[0].Id;
            data.PetX = work.Left + work.Width * 0.65; data.PetY = work.Bottom - pet.Height - 30;
        }
        if (data.Items.Count == 0 && data.Inventory.GetValueOrDefault("cushion") > 0 && data.PetMonitorId == "")
        {
            foreach (var id in new[] { "food-bowl", "water-bowl", "cushion" }) life.Shop.Place(id);
            for (var i = 0; i < data.Items.Count; i++) { data.Items[i].X = work.Left + 60 + i * 95; data.Items[i].Y = work.Bottom - 120; }
        }
        pet.Left = data.PetX; pet.Top = data.PetY;
        bubble = new SpeechBubbleWindow(data.Settings);
        pet.DragFinished += OnDrop; pet.RecallRequested += Recall; pet.SizeChanged += OnSizeChanged;
        pet.RadialRequested += OpenRadial; pet.InteractionRequested += Interact;
        pet.HideRequested += Hide;
        life.Reaction += pet.React; life.Speech += Speak;
        life.ActionAnimation += pet.AnimateAction; life.Consumed += pet.AnimateFood;
        tray = new TrayService(Recall, Hide, () => Application.Current.Shutdown(), pet.Portrait!, character.DisplayName);
        tray.Add("상점", () => OpenPanel("Shop")); tray.Add("보관함", () => OpenPanel("Inventory"));
        tray.Add("설정", () => OpenPanel("Settings")); tray.Add("디버그", ShowDebug);
        timer.Tick += Tick;
    }

    public void Start()
    {
        Application.Current.MainWindow = pet;
        SyncItems(); pet.Show(); OnDrop();
        cursorWorld = new Point(pet.Left + pet.Width / 2, pet.Top + pet.Height / 2);
        clock.Start(); timer.Start();
        Speak("Greeting", true);
    }
    private Rect Bounds()
    {
        var area = MonitorService.At(pet, new Point(pet.Left + pet.Width / 2, pet.Top + pet.Height / 2)).Work;
        if (life.Habitat.Quiet && !life.Ball.Active && !life.Playground.Active) area.Intersect(life.Habitat.Bounds);
        return new Rect(area.Left, area.Top, Math.Max(0, area.Width - pet.Width), Math.Max(0, area.Height - pet.Height));
    }
    private void Recall()
    {
        hidden = false; SyncItems();
        var work = MonitorService.At(pet, new Point(pet.Left, pet.Top)).Work;
        pet.Left = work.Left + work.Width * 0.65; pet.Top = work.Bottom - pet.Height - 30;
        pet.Show(); OnDrop();
    }
    public void ShowPet() => Recall();
    public void CheckRecovery(Action complete)
    {
        Hide();
        if (!tray.IsVisible || pet.IsVisible) throw new InvalidOperationException("Hidden companion lost its tray entry");
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false })?.Dispose();
        var started = Stopwatch.StartNew();
        var poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        poll.Tick += (_, _) =>
        {
            if (!pet.IsVisible && started.Elapsed.TotalSeconds < 5) return;
            poll.Stop();
            if (!pet.IsVisible) { File.WriteAllText("self-test-error.txt", "Second launch did not restore hidden companion"); Application.Current.Shutdown(1); }
            else complete();
        };
        poll.Start();
    }
    private void Hide()
    {
        tray.EnsureVisible();
        if (life.Playing) life.StopPlay(); ballWindow?.Hide();
        playgroundWindow?.Close(); playgroundWindow=null;
        hidden = true; pet.Hide(); bubble.Hide(); radial?.Close();
        foreach (var item in itemWindows.Values) item.Hide();
    }
    private void OnDrop()
    {
        var point = PetMotion.Clamp(new Point(pet.Left, pet.Top), MonitorService.MovementBounds(pet));
        pet.Left = point.X; pet.Top = point.Y; motion.Rest(); UpdatePosition();
        life.ActorSize = new Size(pet.Width, pet.Height); life.SeatOnBed(); Changed();
    }
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        life.Data.Settings.PetScale = pet.Width / 128;
        if (pet.IsLoaded) { OnDrop(); SyncItems(); }
    }
    private void UpdatePosition()
    {
        life.Data.PetX = pet.Left; life.Data.PetY = pet.Top;
        life.Habitat.PetCenter = new Point(pet.Left + pet.Width / 2, pet.Top + pet.Height / 2);
        life.Data.PetMonitorId = MonitorService.At(pet, life.Habitat.PetCenter).Id;
    }
    private void SyncItems()
    {
        foreach (var removed in itemWindows.Keys.Where(id => !life.Data.Items.Any(i => i.Id == id)).ToArray())
        { itemWindows[removed].Close(); itemWindows.Remove(removed); }
        foreach (var item in life.Data.Items)
        {
            if (!itemWindows.TryGetValue(item.Id, out var window))
            {
                window = new HabitatItemWindow(item, life.Catalog[item.ItemId], ItemCommand);
                window.FoodName = id => life.Catalog.TryGetValue(id,out var food) ? food.Name : "밥";
                var captured = window;
                window.PetClicked += () => { if (!pet.ApplyArmed()) Interact(pet.DirectTouch() ? "Pet" : "Talk"); };
                window.PetMenuRequested += OpenRadial;
                window.ArmedClick = pet.ApplyArmed;
                window.GestureStart = pet.BeginSceneGesture;
                window.PetDragStarted += () => { life.Rest(30); pet.BeginExternalDrag(); };
                window.PetDragDelta += delta => { pet.Left += delta.X; pet.Top += delta.Y; };
                window.PetDragFinished += pet.EndExternalDrag;
                window.Moved += () =>
                {
                    item.X = captured.Left - captured.SceneOffset.X; item.Y = captured.Top - captured.SceneOffset.Y;
                    item.MonitorId = MonitorService.At(captured, new Point(item.X, item.Y)).Id;
                    SyncItems(); Changed();
                };
                itemWindows[item.Id] = window;
            }
            var size = life.Habitat.SizeOf(item); window.SetBaseSize(size);
            var work = MonitorService.At(pet, new Point(item.X, item.Y)).Work;
            item.X = Math.Clamp(item.X, work.Left, Math.Max(work.Left, work.Right - size.Width));
            item.Y = Math.Clamp(item.Y, work.Top, Math.Max(work.Top, work.Bottom - size.Height));
            window.Left = item.X; window.Top = item.Y; window.Refresh();
            if (!hidden && !window.IsVisible) window.Show();
            WindowPolicy.Behind(window, pet);
        }
    }
    private void ItemCommand(string command, HabitatItem item)
    {
        switch (command)
        {
            case "Menu": OpenItemRadial(item); return;
            case "ShopFood": OpenPanel("FoodShop", item); return;
            case "FreeFood":
                if (item.FoodQuantity == 0) { item.FoodId = "free-rice"; item.FoodQuantity = 1; item.FoodPortion=1; }
                break;
            case "StopPlay": life.StopPlay(); break;
            case "Settings": OpenPanel("Settings"); return;
            case "Use": life.ActorSize = new Size(pet.Width, pet.Height); life.UseItem(item); break;
            case "Wake": Command("Wake"); break;
            case "Food": OpenPanel("Food", item); break;
            case "Water": item.Water = 100; break;
            case "Sign": item.Active = !item.Active; break;
            case "Lock": item.Locked = !item.Locked; break;
            case "Remove":
                if (item.ItemId == "food-bowl" && item.FoodQuantity > 0)
                {
                    if (!life.Shop.ReturnFood(item)) { itemWindows[item.Id].ToolTip = "밥 보관 공간이 부족해요. 그릇을 먼저 비워주세요."; return; }
                }
                life.Data.Items.Remove(item); break;
            case "Smaller": pet.SetScale(life.Data.Settings.PetScale - .1); break;
            case "Larger": pet.SetScale(life.Data.Settings.PetScale + .1); break;
            case "Rotate": item.Rotation = (item.Rotation + 15) % 360; break;
        }
        SyncItems(); Changed();
    }
    private void Changed() { if (!finishing) nextSave = Math.Min(nextSave, clock.Elapsed.TotalSeconds + 1); if (bubble.IsVisible) bubble.RefreshSettings(); }
    private void Interact(string kind) { life.RefreshContext(fullscreen); life.Interact(kind); Changed(); }
    private void Speak(string tag, bool autonomous)
    {
        if (hidden || autonomous && (life.Habitat.Quiet || fullscreen)) return;
        if (autonomous && (life.Character.TalkFrequency <= 0 || clock.Elapsed.TotalSeconds < nextAutonomousSpeech)) return;
        bubbleAutonomous = autonomous;
        var text = dialogue.Pick(tag); bubble.Say(text, pet);
        bubbleUntil = clock.Elapsed.TotalSeconds + SpeechBubbleWindow.Duration(text);
        nextAutonomousSpeech = clock.Elapsed.TotalSeconds + life.Character.SpeechInterval;
    }
    private void OpenRadial()
    {
        if (life.Playing) life.StopPlay();
        pet.CancelArmed();
        radial?.Close();
        radial = new RadialMenuWindow(life, () => pet.Portrait, Command, workArea: MonitorService.At(pet,new Point(pet.Left,pet.Top)).Work);
        radial.Show(); radial.Activate();
    }
    private void OpenItemRadial(HabitatItem item)
    {
        pet.CancelArmed();
        radial?.Close();
        radial = new RadialMenuWindow(life, () => pet.Portrait, c => ItemCommand(c,item), item, MonitorService.At(pet,new Point(item.X,item.Y)).Work);
        radial.Show(); radial.Activate();
    }
    private void OpenPanel(string name, HabitatItem? targetBowl = null)
    {
        if(life.Playground.Active) {life.StopPlay();SyncPlayground();}
        var key = name + targetBowl?.Id;
        if (panels.TryGetValue(key, out var existing)) { existing.Activate(); return; }
        Action back = targetBowl == null ? OpenRadial : () => OpenItemRadial(targetBowl);
        Window window = name switch
        {
            "Characters" => new CharacterPickerWindow(life.Data.CharacterId, id => ((App)Application.Current).SwitchCharacter(id), OpenRadial),
            "FoodShop" => new ShopWindow(life, () => { SyncItems(); Changed(); }, "Food", back, () => OpenPanel("Food",targetBowl)),
            "Shop" => new ShopWindow(life, () => { SyncItems(); Changed(); }, back: back, inventory: () => OpenPanel("Inventory")),
            "Food" => new InventoryWindow(life, () => { SyncItems(); Changed(); }, "Food", targetBowl, back, () => OpenPanel("FoodShop",targetBowl)),
            "Snacks" => new InventoryWindow(life, () => { SyncItems(); Changed(); }, "Snack", back: back, shop: () => OpenPanel("Shop")),
            "Inventory" => new InventoryWindow(life, () => { SyncItems(); Changed(); }, back: back, shop: () => OpenPanel("Shop")),
            _ => new SettingsWindow(life, pet.SetScale, Changed, OpenRadial, pet.React)
        };
        panels[key] = window; window.Closed += (_, _) => panels.Remove(key);
        if (window is CompanionPanelWindow panel) panel.PlaceHud(pet);
        else MonitorService.PlacePopup(window, pet);
        window.Show(); window.Activate();
    }
    private void Command(string command)
    {
        life.RefreshContext(fullscreen);
        switch (command)
        {
            case "Pet": pet.ArmStroke(false); break;
            case "Chin": pet.ArmStroke(true); break;
            case "Soothe": case "Praise": case "Refuse": Interact(command); break;
            case "Poke": pet.ArmPoke(); break;
            case "Play": life.StartPlay(); break;
            case "BallPlay": life.StartBallPlay(); break;
            case "BubblePlay": case "SnackPlay": pet.CancelArmed();life.StartPlayground(command=="BubblePlay"?PlaygroundMode.Bubbles:PlaygroundMode.Snacks);break;
            case "StopPlay": life.StopPlay(); break;
            case "Characters": OpenPanel("Characters"); break;
            case "Quit": Application.Current.Shutdown(); return;
            case "Food": OpenPanel("Food"); break;
            case "Water": foreach (var item in life.Data.Items.Where(i => i.ItemId == "water-bowl")) item.Water = 100; SyncItems(); break;
            case "Snack": OpenPanel("Snacks"); break;
            case "FlickArm": pet.ArmFlick(); break;
            case "CheekArm": pet.ArmCheek(); break;
            case "Settings": case "Shop": case "Inventory": OpenPanel(command); break;
            
            case "Debug": ShowDebug(); break;
            case "Tools": OpenPanel("Settings"); break;
            case "Hide": Hide(); break;
            case "Wake": life.Rest(15); pet.React(PetPose.Happy); Speak("WakeReaction",false); break;
            case "Rest": motion.Rest(); life.Rest(30); break;
            case "Come": life.Approach(cursorWorld + new Vector(-45, 35)); break;
            case "CursorPlay":
                if (life.Context.CanInterrupt) life.StartPlay(true);
                break;
            default:
                if (command.StartsWith("Force:")) feedback = life.Brain.Force(command[6..], life.Context) ? "행동을 시작했어요." : "지금 실행할 수 없는 행동이에요.";
                if (command.StartsWith("Talk:")) Speak(command[5..], false);
                break;
        }
        Changed();
    }
    private void Tick(object? sender, EventArgs e)
    {
        var now = clock.Elapsed.TotalSeconds; var delta = Math.Clamp(now - lastTick, 0, 1); lastTick = now;
        if (now >= nextSense)
        {
            nextSense = now + 1; var wasActive = active; active = DesktopAwareness.IsActive();
            if (!hidden) WindowPolicy.Top(pet);
            fullscreen = life.Data.Settings.FullscreenCourtesy && DesktopAwareness.IsFullscreen();
            if (active && !wasActive && life.Data.SimulationSeconds >= life.Brain.Cooldowns.GetValueOrDefault("Greeting", 300))
            { Speak("ReturnGreeting", true); life.Brain.Cooldowns["Greeting"] = life.Data.SimulationSeconds + 300; }
        }
        var input = activity?.Drain() ?? (Keys: 0, Clicks: 0);
        points.Update(delta, active, DateTimeOffset.UtcNow, input.Keys, input.Clicks);
        life.Context.LongActiveSession = points.ActivityState == "LongActiveSession";
        var cursor = CursorTracker.RelativeTo(pet);
        if (cursor.HasValue)
        {
            var current = new Point(pet.Left + cursor.Value.X, pet.Top + cursor.Value.Y);
            if (delta > 0 && now >= nextStartle && !pet.IsInteracting && (current - cursorWorld).Length / delta > 1800 &&
                (current - life.Habitat.PetCenter).Length < 140 && life.Brain.Current.Name != "Sleep")
            { pet.React(PetPose.Startled); life.Brain.Pause(life.Data.SimulationSeconds, 1); nextStartle = now + 15; }
            cursorWorld = current;
        }
        Step(delta, cursor);
        if (life.Brain.Current.Name == "Idle" && life.Character.TalkFrequency > 0 && life.Data.Pet.Social < 85 &&
            life.Data.SimulationSeconds >= life.Brain.Cooldowns.GetValueOrDefault("IdleTalk", life.Character.IdleTalkInterval))
        { Speak("IdleTalk", true); life.Brain.Cooldowns["IdleTalk"] = life.Data.SimulationSeconds + life.Character.IdleTalkInterval; }
        if (bubbleAutonomous && (life.Habitat.Quiet || fullscreen) || now >= bubbleUntil) bubble.Hide();
        else if (bubble.IsVisible) bubble.Follow();
        if (now >= nextSave)
        {
            nextSave = now + 60;
            _ = save.SaveAsync(life.Data);
        }
    }
    private void Step(double seconds, Point? localCursor)
    {
        UpdatePosition(); life.NavigationBounds = Bounds(); life.ActorSize = new Size(pet.Width, pet.Height);
        life.Ball.Bounds = MonitorService.At(pet, life.Habitat.PetCenter).Work;
        life.Playground.Bounds=life.Ball.Bounds;
        life.ReachRadius = 18; life.Update(seconds, cursorWorld, fullscreen);
        var before = new Point(pet.Left, pet.Top);
        if (!hidden && !pet.IsInteracting && radial?.IsVisible != true && !pet.IsReacting && (!life.Brain.IsPaused(life.Data.SimulationSeconds) || life.Ball.Active || life.Playground.Active))
        {
            Point next;
            if (life.Destination.HasValue)
            {
                var target = PetMotion.Clamp(life.Destination.Value - new Vector(pet.Width / 2, pet.Height / 2), Bounds());
                var offset = target - before; var speed = Math.Min(offset.Length, seconds * (life.Ball.Running || life.Playground.Mode==PlaygroundMode.Bubbles ? 210 : life.Playground.Mode==PlaygroundMode.Snacks ? 85 : 42));
                next = offset.Length <= 0.01 ? before : before + offset * (speed / offset.Length);
            }
            else if (life.Brain.Current.Name is "Idle" or "Wander" or "LookAtCursor") next = motion.Update(before, Bounds(), seconds);
            else next = PetMotion.Clamp(before, Bounds());
            pet.Left = next.X; pet.Top = next.Y;
        }
        var arrived = !life.Destination.HasValue || (life.Destination.Value - life.Habitat.PetCenter).Length <= life.ReachRadius;
        pet.Render(new Point(pet.Left, pet.Top) - before, localCursor, life.Data.SimulationSeconds, motion.Activity, arrived ? life.Pose : null);
        if (life.Ball.Active) pet.RenderBall(life.Ball, new Point(pet.Left, pet.Top) - before, life.Data.SimulationSeconds);
        if (life.Playground.Active) pet.RenderPlayground(life.Playground,new Point(pet.Left,pet.Top)-before,new Point(pet.Left+pet.Width/2,pet.Top+pet.Height/2),life.Data.SimulationSeconds);
        var composite = false;
        foreach (var item in itemWindows.Values)
        {
            if (item.Item.ItemId == "ball" && life.Ball.Active) { item.Hide(); continue; }
            if (!hidden && !item.IsVisible) item.Show();
            int? scene = null;
            if (item.Item == life.InteractionItem && life.InteractionArrived && !pet.IsInteracting && !pet.IsReacting)
            {
                var beat = (int)(life.Data.SimulationSeconds / 0.4) % 2;
                scene = life.Brain.Current.Name switch
                {
                    "SitOnBed" => cursorWorld.X < life.Habitat.PetCenter.X ? 0 : 1,
                    "Sleep" => 2 + (int)(life.Data.SimulationSeconds / 1.2) % 2,
                    "Eat" when life.InteractionProgress < 1 => 4 + beat,
                    "Drink" when life.InteractionProgress < 1 => 6 + beat,
                    "Play" => item.Item.ItemId == "doll" ? 10 + beat : 8 + beat,
                    _ => null
                };
            }
            composite |= scene.HasValue;
            item.Refresh(life.Brain.Current.Name == "QuietProtest" && item.Item.Active, scene, actorWidth: pet.Width,motionTime:life.Data.SimulationSeconds);
            if (item.IsComposite) WindowPolicy.Top(item); else WindowPolicy.Behind(item, pet);
        }
        pet.ShowComposite(composite);
        SyncBall();
        SyncPlayground();
        if (bubble.IsVisible) WindowPolicy.Top(bubble);
        if (radial?.IsVisible == true) WindowPolicy.Top(radial);
        foreach (var panel in panels.Values) if (panel.IsVisible) WindowPolicy.Top(panel);
    }
    private void SyncBall()
    {
        if (!life.Ball.Active || hidden)
        { ballWindow?.Close(); ballWindow = null; return; }
        if (ballWindow == null)
        { ballWindow = new BallWindow(life.Ball); ballWindow.Refresh(); ballWindow.Show(); }
        ballWindow.Refresh();
        if (life.Ball.BehindPet) WindowPolicy.Behind(ballWindow, pet); else WindowPolicy.Top(ballWindow);
    }
    private void SyncPlayground()
    {
        if(!life.Playground.Active || hidden || playgroundWindow!=null && playgroundWindow.Mode!=life.Playground.Mode)
        {playgroundWindow?.Close();playgroundWindow=null;}
        if(!life.Playground.Active || hidden)return;
        if(playgroundWindow==null)
        {
            playgroundWindow=new PlaygroundWindow(life.Playground,life.StopPlay,life.Ball);playgroundWindow.Show();
            if(life.Playground.Mode==PlaygroundMode.Bubbles) {System.Windows.Input.Mouse.OverrideCursor=Icons.BubbleWand;ToolCursor.Show(Icons.BubbleWandPath);}
        }
        playgroundWindow.Refresh();WindowPolicy.Top(playgroundWindow);
    }
    private void DebugCommand(string command)
    {
        life.RefreshContext(fullscreen);
        switch (command)
        {
            case "AddAP": life.Data.ActivityPoints = Math.Min(1_000_000, life.Data.ActivityPoints + 15000); break;
            case "FillFood": foreach (var item in life.Data.Items.Where(i => i.ItemId == "food-bowl")) { item.FoodId = "meal"; item.FoodQuantity = 1; item.FoodPortion=1; } break;
            case "EmptyFood": foreach (var item in life.Data.Items.Where(i => i.ItemId == "food-bowl")) item.FoodQuantity = 0; break;
            case "FillWater": foreach (var item in life.Data.Items.Where(i => i.ItemId == "water-bowl")) item.Water = 100; break;
            case "EmptyWater": foreach (var item in life.Data.Items.Where(i => i.ItemId == "water-bowl")) item.Water = 0; break;
            case "ToggleSign": foreach (var item in life.Data.Items.Where(i => life.Catalog[i.ItemId].Category == "Bed")) item.Active = !item.Active; break;
            case "ToggleInside":
                var signCenter = life.Habitat.Center(life.Data.Items.First(i => life.Catalog[i.ItemId].Category == "Bed"));
                pet.Left = life.Habitat.Inside ? signCenter.X + 300 : signCenter.X - pet.Width / 2;
                pet.Top = signCenter.Y - pet.Height / 2; OnDrop(); break;
            default:
                if (command.StartsWith("Advance:") && int.TryParse(command[8..], out var seconds))
                    for (var i = 0; i < Math.Clamp(seconds, 0, 3600); i++) Step(1, null);
                else Command(command);
                break;
        }
        SyncItems(); Changed();
    }
    private void ShowDebug()
    {
        debug ??= new DebugWindow(() => string.Join("\n", typeof(PetState).GetProperties().Select(p => $"{p.Name,-12} {p.GetValue(life.Data.Pet):F1}")) +
            $"\nBehavior: {life.Brain.Current.Name}\nIntent: {life.Brain.Current.Intent}\nAP: {life.Data.ActivityPoints}\nActivity: {points.ActivityState}" +
            $"\nPetInsideHabitat: {life.Habitat.Inside}\nQuietSignActive: {life.Habitat.SignActive}\nQuietHabitat: {life.Habitat.Quiet}" +
            $"\nQuiet minutes: {life.Data.QuietSeconds / 60:F1}\nSave: {save.Status}\n{feedback}\n" +
            string.Join("\n", life.Brain.Scores.Select(p => $"{p.Key}: {p.Value:F1}")),
            (name, value) => { typeof(PetState).GetProperty(name)!.SetValue(life.Data.Pet, value); life.Data.Pet.Clamp(); },
            DebugCommand, life.Brain.Behaviors.Select(b => b.Name));
        debug.Show(); debug.Activate();
    }
    public void ShowUpdate(Momonga.Updates.AvailableUpdate release)
    {
        var window = new UpdateWindow(release);
        panels["Updates"] = window; window.Closed += (_, _) => panels.Remove("Updates");
        window.PlaceHud(pet); window.Show(); window.Activate();
    }

    public void CaptureUI()
    {
        var directory = Path.GetFullPath("obj/ui-check"); Directory.CreateDirectory(directory);
        void Capture(Window window, string name)
        {
            window.Show(); window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(directory, name + ".png")); encoder.Save(stream);
        }
        Capture(pet, "pet"); Capture(itemWindows.Values.First(), "furniture");
        var previews = new DrawingVisual();
        using (var drawing = previews.RenderOpen())
        {
            var characters = CharacterDefinition.All;
            for (var i = 0; i < characters.Length; i++)
            {
                var character = characters[i]; CharacterSprites.Select(character.AssetSet); Theme.Apply(character.Theme);
                if (((SolidColorBrush)Theme.Brush("Soft")).Color != (Color)ColorConverter.ConvertFromString(character.Theme.Soft))
                    throw new InvalidOperationException("Character palette was not applied immediately");
                var x = i % 4 * 300; var y = i / 4 * 180;
                drawing.DrawText(new FormattedText(character.DisplayName, System.Globalization.CultureInfo.GetCultureInfo("ko-KR"), FlowDirection.LeftToRight,
                    new Typeface("Malgun Gothic"), 14, System.Windows.Media.Brushes.Black, 1), new Point(x + 8, y + 5));
                var animator = new PetAnimator(character.AssetSet);
                drawing.DrawImage(animator.Frame(new Vector(), null, false, 1), new Rect(x + 4, y + 30, 130, 130));
                var bowl = itemWindows.Values.First(w => w.Item.ItemId == "food-bowl");
                bowl.Refresh(sceneFrame: 4, actorWidth: pet.Width); bowl.UpdateLayout();
                var scene = new RenderTargetBitmap((int)bowl.ActualWidth, (int)bowl.ActualHeight, 96, 96, PixelFormats.Pbgra32); scene.Render(bowl);
                drawing.DrawImage(scene, new Rect(x + 145, y + 20, 150 * scene.Width / scene.Height, 150)); bowl.Refresh();
            }
        }
        CharacterSprites.Select(life.Character.AssetSet);
        Theme.Apply(life.Character.Theme);
        var previewImage = new RenderTargetBitmap(1200, ((CharacterDefinition.Ids.Length+3)/4)*180, 96, 96, PixelFormats.Pbgra32); previewImage.Render(previews);
        var previewEncoder = new PngBitmapEncoder(); previewEncoder.Frames.Add(BitmapFrame.Create(previewImage));
        using (var previewFile = File.Create(Path.Combine(directory, "character-scenes.png"))) previewEncoder.Save(previewFile);
        var drinks=new DrawingVisual();
        using(var dc=drinks.RenderOpen())
        {
            dc.DrawRectangle(System.Windows.Media.Brushes.White,null,new Rect(0,0,1200,900));
            for(var i=0;i<CharacterDefinition.Ids.Length;i++)
            {
                var id=CharacterDefinition.Ids[i]; CharacterSprites.Select(id);
                var x=i%4*300; var y=i/4*180;
                dc.DrawText(new FormattedText(CharacterDefinition.Load(id).DisplayName,System.Globalization.CultureInfo.GetCultureInfo("ko-KR"),FlowDirection.LeftToRight,new Typeface("Malgun Gothic"),14,System.Windows.Media.Brushes.Black,1),new Point(x+8,y+5));
                for(var beat=0;beat<2;beat++)
                {
                    var source=InteractionSprites.ActorFrame(6+beat); var scale=130/Math.Max(source.Width,source.Height);
                    dc.DrawImage(source,new Rect(x+beat*150+(150-source.Width*scale)/2,y+35,source.Width*scale,source.Height*scale));
                }
            }
        }
        var drinkingImage=new RenderTargetBitmap(1200,900,96,96,PixelFormats.Pbgra32); drinkingImage.Render(drinks);
        var drinkingEncoder=new PngBitmapEncoder(); drinkingEncoder.Frames.Add(BitmapFrame.Create(drinkingImage));
        using(var stream=File.Create(Path.Combine(directory,"drinking-poses.png"))) drinkingEncoder.Save(stream);
        foreach(var id in CharacterDefinition.Ids.Where(id=>id!="momonga"))
        {
            CharacterSprites.Select(id);var sheet=new DrawingVisual();
            using(var dc=sheet.RenderOpen())
            {
                dc.DrawRectangle(System.Windows.Media.Brushes.White,null,new Rect(0,0,1280,1600));
                for(var frame=0;frame<80;frame++) dc.DrawImage(CharacterSprites.Frame(frame),new Rect(frame%8*160,frame/8*160,160,160));
            }
            var bitmap=new RenderTargetBitmap(1280,1600,96,96,PixelFormats.Pbgra32);bitmap.Render(sheet);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file=File.Create(Path.Combine(directory,id+"-all-frames.png"));encoder.Save(file);
        }
        pet.Topmost=false;WindowPolicy.Top(pet);
        if (!pet.Topmost || !WindowPolicy.IsTopmost(pet)) throw new InvalidOperationException("Pet did not recover native topmost state");
        CharacterSprites.Select(life.Character.AssetSet);
        var dragged = itemWindows.Values.First(); var originalPosition = new Point(dragged.Item.X, dragged.Item.Y);
        dragged.MoveItem(new Vector(21.5, 11.25)); dragged.Refresh();
        if (Math.Abs(dragged.Item.X - originalPosition.X - 21.5) > .01 || Math.Abs(dragged.Left - dragged.Item.X - dragged.SceneOffset.X) > .5)
            throw new InvalidOperationException("Refresh reset furniture drag position");
        dragged.MoveItem(new Vector(-21.5, -11.25));
        bubble.Say("더 귀여워해라!", pet); bubble.RevealAll(); Capture(bubble, "speech");
        foreach (var (window, name) in new (Window, string)[] {
            (new RadialMenuWindow(life, () => pet.Portrait, Command), "radial"), (new SettingsWindow(life, pet.SetScale, Changed), "settings"),
            (new ShopWindow(life, Changed), "shop"), (new InventoryWindow(life, Changed), "inventory"),
            (new CharacterPickerWindow(life.Data.CharacterId, _ => { }), "characters"),
            (new UpdateWindow(new Momonga.Updates.AvailableUpdate(new Version(0,3,2), "오데 대사 수정\n실행 시 새 버전 확인", "", 154000000, "")), "updates") })
        { try { Capture(window, name); } finally { window.Close(); } }
        foreach (var (id, frame, name) in new[] { ("cushion", 0, "sit-scene"), ("food-bowl", 4, "eat-scene"), ("water-bowl", 6, "drink-scene") })
        {
            var item = itemWindows.Values.First(w => w.Item.ItemId == id);
            var boundsBefore = new Rect(item.Left,item.Top,item.Width,item.Height);
            var originalQuantity = item.Item.FoodQuantity; var originalPortion=item.Item.FoodPortion;
            if (id == "food-bowl") { item.Item.FoodQuantity = 1; item.Item.FoodPortion=1; }
            item.Refresh(sceneFrame: frame, actorWidth: pet.Width); Capture(item, name); item.Refresh();
            if (id == "cushion")
            {
                var signActive = item.Item.Active; item.Item.Active = true;
                item.Refresh(sceneFrame: frame, actorWidth: pet.Width); Capture(item, "cushion-quiet");
                item.Item.Active = signActive; item.Refresh();
            }
            if (id == "food-bowl") { item.Refresh(sceneFrame: frame, depletion: .6, actorWidth: pet.Width); Capture(item, "eat-half-scene"); item.Item.FoodQuantity = originalQuantity; item.Item.FoodPortion=originalPortion; item.Refresh(); }
            if (item.Width != boundsBefore.Width || item.Height != boundsBefore.Height || Math.Abs(item.Left-boundsBefore.Left) > .5 || Math.Abs(item.Top-boundsBefore.Top) > .5)
                throw new InvalidOperationException("Furniture moved or resized during interaction"); // Native DPI rounding can change the reported DIP coordinate by less than a pixel.
        }
        var cheekStart = pet.PointToScreen(new Point(pet.Width*.18,pet.Height*.59));
        if (pet.BeginSceneGesture(new Point(.18,.59), cheekStart)) throw new InvalidOperationException("Default direct touch bypassed interaction menu");
        pet.ArmCheek();
        pet.BeginSceneGesture(new Point(.18,.59),cheekStart);
        pet.ContinueGesture(cheekStart - new Vector(pet.Width*.12,0));
        pet.Render(new Vector(),null,life.Data.SimulationSeconds);
        Capture(pet.CheekOverlay!,"cheek-pull"); pet.ReleaseMouseCapture();
        RunIntegrationChecks();
    }

    private void RunIntegrationChecks()
    {
        void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        System.Collections.Generic.IEnumerable<System.Windows.Controls.Button> Buttons(DependencyObject parent)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.Button button) yield return button;
                foreach (var nested in Buttons(child)) yield return nested;
            }
        }
        life.Data.Settings.DirectTouch=false;
        var clickAction=life.Brain.Current.Name;
        pet.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left) { RoutedEvent=System.Windows.Input.Mouse.MouseDownEvent });
        pet.ReleaseMouseCapture();
        Require(life.Data.InteractionHistory.Last().Kind=="Talk" && life.Brain.Current.Name==clickAction,"Plain character click did not speak without changing action");
        var bowlGeometry=itemWindows.Values.First(w=>w.Item.ItemId=="food-bowl");
        var previousFood=bowlGeometry.Item.FoodId; var previousQuantity=bowlGeometry.Item.FoodQuantity; var previousPortion=bowlGeometry.Item.FoodPortion;
        bowlGeometry.Item.FoodQuantity=1; bowlGeometry.Item.FoodPortion=1; bowlGeometry.Refresh(); var drawBounds=bowlGeometry.FurnitureBounds;
        foreach(var food in life.Catalog.Values.Where(i=>i.Category=="Food"))
            foreach(var half in new[]{false,true})
                foreach(var eating in new[]{false,true})
                {
                    bowlGeometry.Item.FoodId=food.Id; bowlGeometry.Item.FoodPortion=half ? .3 : 1;
                    bowlGeometry.Refresh(sceneFrame:eating ? 4 : null,actorWidth:pet.Width);
                    Require(bowlGeometry.FurnitureBounds==drawBounds,"Food art changed furniture drawing bounds");
                }
        bowlGeometry.Item.FoodId=previousFood; bowlGeometry.Item.FoodQuantity=previousQuantity; bowlGeometry.Item.FoodPortion=previousPortion; bowlGeometry.Refresh();
        var snackQuantity=life.Shop.Quantity("cookie"); Command("Snack");
        Require(panels["Snacks"].IsVisible && life.Shop.Quantity("cookie")==snackQuantity,"Snack menu consumed an arbitrary snack before selection"); panels["Snacks"].Close();
        foreach(var id in new[]{"ball","doll","cushion","beanbag","nest"})
        {
            var placed=new HabitatItem { ItemId=id,X=pet.Left,Y=pet.Top+pet.Height };
            var window=new HabitatItemWindow(placed,life.Catalog[id],(_,_)=>{}); window.SetBaseSize(life.Habitat.SizeOf(placed)); window.Refresh();
            var fixedBounds=window.FurnitureBounds;
            try
            {
                window.Refresh(sceneFrame:id=="ball" ? 8 : id=="doll" ? 10 : 2,actorWidth:pet.Width); CaptureExtra(window,id+"-interaction");
                Require(window.FurnitureBounds==fixedBounds,"Toy or bed changed bounds during interaction");
                if(id is "cushion" or "beanbag" or "nest")
                    for(var frame=0;frame<4;frame++)
                    {
                        window.Refresh(sceneFrame:frame,actorWidth:pet.Width);
                        Require(Math.Abs(window.ActorWidth-fixedBounds.Width*1.15)<.001 && window.FurnitureBounds==fixedBounds,"Bed actor changed scale between poses or changed furniture geometry");
                    }
            }
            finally { window.Close(); }
        }
        void CaptureExtra(Window window,string name)
        {
            window.Show(); window.UpdateLayout();
            var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(window);
            var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream=File.Create(Path.GetFullPath("obj/ui-check/"+name+".png")); encoder.Save(stream);
        }
        OpenRadial(); radial!.UpdateLayout();
        Buttons(radial).First(b => Equals(b.ToolTip, "장난")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        radial.UpdateLayout();
        Buttons(radial).First(b => Equals(b.ToolTip, "딱밤 준비")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Require(pet.FlickArmed && ToolCursor.Active && System.Windows.Input.Mouse.OverrideCursor == Icons.FlickCursor && !radial.IsVisible, "Flick menu failed to close safely or arm global custom cursor");
        Require(pet.ApplyArmed() && !pet.FlickArmed && System.Windows.Input.Mouse.OverrideCursor == null && life.Data.InteractionHistory.Last().Kind == "Flick", "Flick click failed to consume armed state");
        pet.ArmCheek(); var start = pet.PointToScreen(new Point(pet.Width*.18,pet.Height*.59));
        var positionBeforePull = new Point(pet.Left,pet.Top); var sizeBeforePull = pet.Width;
        Require(pet.BeginSceneGesture(new Point(.18,.59),start), "Cheek grab failed");
        var toDevice = PresentationSource.FromVisual(pet)!.CompositionTarget!.TransformToDevice;
        pet.ContinueGesture(start - toDevice.Transform(new Vector(pet.Width*.10,0)));
        Require(pet.CheekExpression == 5, "Moderate pull did not change expression");
        pet.ContinueGesture(start - toDevice.Transform(new Vector(pet.Width*.23,0)));
        Require(pet.CheekExpression == 6 && pet.Width == sizeBeforePull && new Point(pet.Left,pet.Top) == positionBeforePull, "Strong pull changed body size or position");
        pet.Render(new Vector(),null,life.Data.SimulationSeconds);
        Require(pet.CheekOverlay?.IsVisible==true && WindowPolicy.IsClickThrough(pet.CheekOverlay),"Cheek overlay clipped the stretch or intercepted mouse input");
        pet.ReleaseMouseCapture(); Require(life.Data.InteractionHistory.Last().Kind == "CheekPull", "Cheek release failed");
        for(var angle=0;angle<8;angle++)
        {
            pet.ArmCheek();pet.BeginSceneGesture(new Point(.18,.59),pet.PointToScreen(new Point(pet.Width*.18,pet.Height*.59)));
            var origin=pet.PointToScreen(new Point(pet.Width*.18,pet.Height*.59));
            pet.ContinueGesture(origin+toDevice.Transform(new Vector(Math.Cos(angle*Math.PI/4)*pet.Width*.20,Math.Sin(angle*Math.PI/4)*pet.Height*.20)));
            pet.Render(new Vector(),null,life.Data.SimulationSeconds);CaptureExtra(pet.CheekOverlay!,"cheek-angle-"+angle);pet.ReleaseMouseCapture();
        }
        pet.Render(new Vector(),null,life.Data.SimulationSeconds+.15);CaptureExtra(pet.CheekOverlay!,"cheek-release-bounce");
        Require(pet.IsInteracting,"Release animation was immediately interrupted");
        life.Data.SimulationSeconds+=.9;pet.Render(new Vector(),null,life.Data.SimulationSeconds);Require(!pet.IsInteracting&&!pet.CheekOverlay!.IsVisible,"Cheek overlay did not close after recovery");
        foreach (var kind in new[] { "Pet", "Praise", "Play", "Poke", "Refuse" }) Interact(kind);
        life.Data.Pet.Fun = 10; Command("Play"); Require(life.Playing, "Play failed to start before right-click check");
        OpenRadial(); Require(radial?.IsVisible == true, "Right-click HUD did not open"); radial?.Close();
        Require(!life.Playing, "Character right-click did not immediately stop play");
        OpenRadial(); radial!.UpdateLayout();
        Buttons(radial).First(b => Equals(b.ToolTip,"놀기")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));radial.UpdateLayout();
        Buttons(radial).First(b => Equals(b.ToolTip,"공 놀이")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Step(0,null);Require(life.Ball.Active && ballWindow?.IsVisible==true,"Play menu did not spawn a throwable ball");
        Require(playgroundWindow?.Mode==PlaygroundMode.Ball && WindowPolicy.IsBelow(pet,ballWindow!) && WindowPolicy.IsBelow(ballWindow!,playgroundWindow) && WindowPolicy.IsClickThrough(ballWindow!),"Ball input did not belong exclusively to the throwing overlay");
        CaptureExtra(ballWindow!,"throwable-ball");
        var ballPosition=life.Ball.Position;var ballPhase=life.Ball.Phase;
        ballWindow!.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left){RoutedEvent=System.Windows.Input.Mouse.MouseDownEvent});
        Require(life.Ball.Position==ballPosition && life.Ball.Phase==ballPhase,"Visual ball window still handled the old movement drag");
        life.Ball.Grab();life.Playground.BeginAim(life.Ball.Position);life.Playground.Pull(life.Ball.Position+new Vector(-80,20));playgroundWindow!.Refresh();CaptureExtra(playgroundWindow,"ball-aim");life.Playground.ReleaseBall(life.Ball);
        playgroundWindow!.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Right) {RoutedEvent=System.Windows.Input.Mouse.MouseUpEvent});
        Step(0,null);Require(!life.Ball.Active && ballWindow==null,"Ball right-click did not remove the toy");
        Command("BallPlay");Step(0,null);OpenRadial();radial!.Close();Step(0,null);
        Require(!life.Ball.Active && ballWindow==null,"Pet right-click left a ball window behind");
        OpenRadial();radial!.UpdateLayout();
        Buttons(radial).First(b=>Equals(b.ToolTip,"놀기")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));radial.UpdateLayout();
        Buttons(radial).First(b=>Equals(b.ToolTip,"비눗방울")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Step(0,null);
        Require(playgroundWindow?.IsVisible==true && ToolCursor.Active && System.Windows.Input.Mouse.OverrideCursor==Icons.BubbleWand,"Bubble menu did not arm a real wand cursor");
        life.Playground.Blow(life.Habitat.PetCenter+new Vector(-200,-180));Step(.05,null);CaptureExtra(playgroundWindow!,"bubble-play");
        Command("SnackPlay");Step(0,null);Require(!ToolCursor.Active,"Switching games did not restore the wand cursor");
        var launch=life.Habitat.PetCenter+new Vector(-140,-90);life.Playground.BeginAim(launch);life.Playground.Pull(launch+new Vector(-65,40));
        playgroundWindow!.Refresh();CaptureExtra(playgroundWindow,"snack-aim");
        playgroundWindow.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Right){RoutedEvent=System.Windows.Input.Mouse.MouseUpEvent});Step(0,null);
        Require(!life.Playground.Active && playgroundWindow==null && !ToolCursor.Active,"Mini-game right-click failed to restore desktop input");
        CharacterSprites.Select("kurimanju");
        var beerPet=new PetWindow("kurimanju") {Width=128,Height=136};
        try
        {
            beerPet.AnimateFood(life.Catalog["beer"]);
            ImageSource? previous=null;
            foreach(var t in new[]{.1,.8,1.8,3.2})
            {
                beerPet.Render(new Vector(),null,t);Require(beerPet.Portrait!=previous,"Beer animation did not change drinking/exhale poses");previous=beerPet.Portrait;
                CaptureExtra(beerPet,"kurimanju-beer-"+t.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        finally {beerPet.Close();CharacterSprites.Select(life.Character.AssetSet);}
        OpenRadial(); radial!.UpdateLayout();
        Buttons(radial).First(b => Equals(b.ToolTip,"더보기")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); radial.UpdateLayout();
        Require(Buttons(radial).Any(b => Equals(b.ToolTip,"종료")), "More menu has no exit"); radial.Close();
        var bowlForMenu = life.Data.Items.First(i => i.ItemId == "food-bowl");
        OpenItemRadial(bowlForMenu); radial!.UpdateLayout();
        Require(Buttons(radial).Any(b => Equals(b.ToolTip,"밥 상점")) && Buttons(radial).Any(b => Equals(b.ToolTip,"무료 흰밥")), "Bowl menu missing food shop or emergency food"); radial.Close();
        bowlForMenu.FoodQuantity = 0; var freeBalance = life.Data.ActivityPoints; ItemCommand("FreeFood",bowlForMenu);
        Require(bowlForMenu.FoodId == "free-rice" && bowlForMenu.FoodQuantity == 1 && life.Data.ActivityPoints == freeBalance, "Free rice was not free or not placed in bowl");
        foreach (var name in new[] { "Shop", "Inventory", "Settings", "Characters" })
        { OpenPanel(name); Require(panels[name].IsVisible, name + " panel did not open"); panels[name].Close(); }
        var picker = new CharacterPickerWindow(life.Data.CharacterId, _ => { }); picker.Show(); picker.UpdateLayout();
        var groups = picker.Body.Children.OfType<System.Windows.Controls.Expander>().ToArray();
        Require(groups.Length == CharacterDefinition.All.Select(c=>c.Category).Distinct().Count(), "Missing grouped character categories");
        groups[0].IsExpanded = false; picker.UpdateLayout();
        Require(!((System.Windows.Controls.WrapPanel)groups[0].Content).IsVisible, "Character category failed to collapse"); picker.Close();
        var wentBack = false;
        var settings = new SettingsWindow(life, pet.SetScale, Changed, () => wentBack = true);
        settings.Show(); settings.UpdateLayout();
        Buttons(settings).First(b => Equals(b.ToolTip, "뒤로가기")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Require(wentBack && !settings.IsVisible, "Settings back did not dismiss HUD");
        settings = new SettingsWindow(life, pet.SetScale, Changed); settings.Show(); settings.UpdateLayout();
        ((System.Windows.Controls.Canvas)settings.Content).RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left) { RoutedEvent = System.Windows.Input.Mouse.MouseDownEvent });
        Require(!settings.IsVisible, "Settings outside click did not dismiss HUD");
        ShowDebug(); Require(debug?.IsVisible == true, "Debug panel did not open"); debug?.Hide();
        var sign = life.Data.Items.First(i => life.Catalog[i.ItemId].Category == "Bed");
        if (!sign.Active) ItemCommand("Sign", sign);
        if (!life.Habitat.Inside) DebugCommand("ToggleInside");
        Step(0, null); Require(life.Quiet, "Physical placement failed to activate Quiet Habitat");
        Require(!life.Brain.Force("AskAttention", life.Context), "Quiet HUD allowed autonomous attention");
        pet.SetScale(2); Step(0, null);
        DebugCommand("FillFood"); life.Data.Pet.Hunger = 95; Command("Force:Eat");
        var foodCount = life.Data.Items.First(i => i.ItemId == "food-bowl").FoodQuantity;
        for (var i = 0; i < 30; i++) Step(1, null);
        Require(life.Data.Items.First(i => i.ItemId == "food-bowl").FoodQuantity < foodCount,
            "Large pet could not reach bowl inside habitat: " + life.Brain.Current.Name + " pos=" + life.Habitat.PetCenter + " target=" + life.Destination + " food=" + life.Data.Items.First(i => i.ItemId == "food-bowl").FoodQuantity);
        var position = new Point(pet.Left, pet.Top); var insideBounds = Bounds();
        Require(insideBounds.Contains(position), "Quiet pet escaped habitat");
        var apBefore = life.Data.ActivityPoints; DebugCommand("Advance:600");
        Require(life.Data.ActivityPoints == apBefore, "Debug time advancement generated activity points");
        ItemCommand("Sign", sign); Step(0, null);
        Require(!life.Quiet, "Disabling cushion quiet mode failed");
        var hungerBefore = life.Data.Pet.Hunger; Hide(); for (var i = 0; i < 60; i++) Step(1, null);
        Require(life.Data.Pet.Hunger >= hungerBefore && !pet.IsVisible && itemWindows.Values.All(w => !w.IsVisible), "Hide state broke simulation or leaked world windows");
        Require(tray.IsVisible, "Hiding pet removed tray recovery icon");
        ShowPet(); Require(pet.IsVisible && itemWindows.Values.All(w => w.IsVisible), "Tray recall did not restore pet and furniture");
        pet.SetScale(1); life.Data.Pet.Energy = 5; Command("Force:Sleep");
        Step(0, null);
        var sleepTravelSeconds = (life.Destination.GetValueOrDefault(life.Habitat.PetCenter) - life.Habitat.PetCenter).Length / 42;
        for (var i = 0; i < Math.Ceiling(sleepTravelSeconds) + 15; i++) Step(1, null);
        Require(life.Data.Pet.Energy > 5, "Sleep did not restore energy at furniture: " + life.Brain.Current.Name + " energy=" + life.Data.Pet.Energy + " center=" + life.Habitat.PetCenter + " target=" + life.Destination);
        var bed = life.Data.Items.First(i => life.Catalog[i.ItemId].Category == "Bed");
        pet.Left = life.FurnitureTarget(bed).X - pet.Width / 2; pet.Top = life.FurnitureTarget(bed).Y - pet.Height / 2;
        OnDrop(); Require(life.Brain.Current.Name == "SitOnBed", "Dropping on cushion did not select sitting");
        ItemCommand("Use", bed); for (var i = 0; i < 20; i++) Step(1, null);
        Require((life.Habitat.PetCenter - life.FurnitureTarget(bed)).Length <= life.ReachRadius, "Pet did not settle on selected cushion");
        Require(itemWindows[bed.Id].IsComposite && !WindowPolicy.IsBelow(itemWindows[bed.Id], pet), "Interaction scene was not above pet");
        var bedWidth = itemWindows[bed.Id].Width;
        pet.SetScale(1.37);
        Require(Math.Abs(itemWindows[bed.Id].Width / bedWidth - 1.37) < 0.01, "Cushion did not track continuous pet scale");
        ItemCommand("Use", bed); for (var i = 0; i < 20; i++) Step(1, null);
        var objects = new Window[] { itemWindows[bed.Id], pet };
        var left = objects.Min(w => w.Left); var top = objects.Min(w => w.Top);
        var right = objects.Max(w => w.Left + w.Width); var bottom = objects.Max(w => w.Top + w.Height);
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
            foreach (var window in objects)
            {
                window.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.Width), (int)Math.Ceiling(window.Height), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window); context.DrawImage(bitmap, new Rect(window.Left - left, window.Top - top, window.Width, window.Height));
            }
        var scene = new RenderTargetBitmap((int)Math.Ceiling(right - left), (int)Math.Ceiling(bottom - top), 96, 96, PixelFormats.Pbgra32);
        scene.Render(drawing); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(scene));
        using var output = File.Create("obj/ui-check/cushion-rest.png"); png.Save(output);
    }
    public void Dispose()
    {
        finishing = true; timer.Stop(); activity?.Dispose(); UpdatePosition(); save.SaveAsync(life.Data).GetAwaiter().GetResult();
        pet.CancelArmed();
        timer.Tick -= Tick;
        ballWindow?.Close();
        playgroundWindow?.Close();
        pet.DragFinished -= OnDrop; pet.RecallRequested -= Recall; pet.SizeChanged -= OnSizeChanged;
        pet.RadialRequested -= OpenRadial; pet.InteractionRequested -= Interact;
        pet.HideRequested -= Hide;
        life.Reaction -= pet.React; life.Speech -= Speak;
        life.ActionAnimation -= pet.AnimateAction; life.Consumed -= pet.AnimateFood;
        radial?.Close(); foreach (var panel in panels.Values.ToArray()) panel.Close(); debug?.Finish();
        foreach (var item in itemWindows.Values) item.Close();
        bubble.Close(); tray.Dispose(); pet.Close();
    }
}
