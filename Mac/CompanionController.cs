using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Momonga.Animation;
using Momonga.Character;
using Momonga.Content;
using Momonga.Economy;
using Momonga.Inventory;
using Momonga.Persistence;
using Momonga.Simulation;
using System.Diagnostics;

namespace Momonga.Mac;

public sealed partial class CompanionController : IDisposable
{
    private readonly IClassicDesktopStyleApplicationLifetime desktop;
    private readonly bool testing;
    private readonly SaveService save;
    private LifeSimulation life;
    private DialogueService dialogue;
    private PetMotion motion;
    private readonly MacActivity activity;
    private readonly ActivityPointService points;
    private readonly DispatcherTimer timer=new() { Interval=TimeSpan.FromMilliseconds(33) };
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private readonly Window pet;
    private readonly SpriteView petView=new();
    private readonly Dictionary<string,(Window Window,SceneView View)> furniture=new();
    private Window? panel,bubble;
    private TrayIcon? tray;
    private bool hidden,dragging,disposed;
    private readonly System.Threading.CancellationTokenSource updateCancellation=new();
    private Point position,pointer,pressWorld;
    private PixelPoint pressScreen;
    private Point pressLocal;
    private string armed="",gesture="";
    private double strokeDistance,previousTime,nextSave,nextTalk,reactUntil,snackUntil,bubbleUntil,nextTop;
    private PetPose? reaction;
    private ItemDefinition? snack;
    private int facing=2;
    private double nextTurn;
    public CompanionController(IClassicDesktopStyleApplicationLifetime desktop,bool testing)
    {
        this.desktop=desktop; this.testing=testing;
        save=new SaveService(testing?Path.Combine(Path.GetTempPath(),"chiikawa-ui-"+Guid.NewGuid()+".json"):null);
        var catalog=ItemCatalog.Load(); var data=testing?new SaveData():save.Load(catalog);
        life=new LifeSimulation(data,CharacterDefinition.Load(data.CharacterId),catalog);
        points=new ActivityPointService(data);
        dialogue=new DialogueService(data,life.Character.DialogueSetId); motion=new PetMotion(character:life.Character);
        activity=new MacActivity(!testing);
        pet=TransparentWindow(128*data.Settings.PetScale,136*data.Settings.PetScale);
        pet.Content=petView; desktop.MainWindow=pet;
        pet.PointerPressed+=PetPressed; pet.PointerMoved+=PetMoved; pet.PointerReleased+=PetReleased;
        pet.KeyDown+=(_,e)=>{ if(e.Key==Key.Escape) { Arm(""); panel?.Close(); } };
        AttachLife(); timer.Tick+=Tick;
    }
    private void AttachLife()
    {
        CharacterSprites.Current=life.Character.AssetSet;
        life.Reaction+=(p)=>{reaction=p;reactUntil=clock.Elapsed.TotalSeconds+3;};
        life.Speech+=Speak;
        life.Consumed+=item=>{snack=item;snackUntil=clock.Elapsed.TotalSeconds+3;};
    }
    private static Window TransparentWindow(double width,double height)=>new()
    {
        Width=width,Height=height,CanResize=false,SystemDecorations=SystemDecorations.None,
        ShowInTaskbar=false,ShowActivated=false,Topmost=true,Background=Brushes.Transparent,
        TransparencyLevelHint=new[]{WindowTransparencyLevel.Transparent},WindowStartupLocation=WindowStartupLocation.Manual
    };
    public void Start()
    {
        pet.Show(); position=new Point(life.Data.PetX,life.Data.PetY); position=Clamp(position); SetPosition(pet,position);
        pointer=position+new Vector(64,68);
        var menu=new NativeMenu();
        foreach(var (label,action) in new (string,Action)[]{("다시 보이기",Recall),("잠깐 숨기기",Hide),("캐릭터 변경",Characters),("설정",Settings),("종료",Exit)})
        { var item=new NativeMenuItem(label); item.Click+=(_,_)=>action(); menu.Items.Add(item); }
        var iconView=new SpriteView {Width=32,Height=32,Sprite=Sprites.Frame(life.Character.AssetSet,2)};
        iconView.Measure(new Size(32,32)); iconView.Arrange(new Rect(0,0,32,32));
        using var icon=new RenderTargetBitmap(new PixelSize(32,32)); icon.Render(iconView);
        using var iconStream=new MemoryStream(); icon.Save(iconStream); iconStream.Position=0;
        tray=new TrayIcon {Icon=new WindowIcon(iconStream),ToolTipText="Chiikawa Companion",Menu=menu,IsVisible=true}; tray.Clicked+=(_,_)=>Recall();
        TrayIcon.SetIcons(Application.Current!,new TrayIcons {tray});
        if(life.Data.Items.Count==0) foreach(var id in new[]{"food-bowl","water-bowl","cushion"}) life.Shop.Place(id);
        SyncFurniture(); timer.Start(); Speak("Greeting",false);
    }
    private Point World(Window window)=>new(window.Position.X/window.RenderScaling,window.Position.Y/window.RenderScaling);
    private void SetPosition(Window window,Point p)=>window.Position=new PixelPoint((int)Math.Round(p.X*window.RenderScaling),(int)Math.Round(p.Y*window.RenderScaling));
    private Rect Work()
    {
        var s=pet.Screens.ScreenFromWindow(pet) ?? pet.Screens.Primary;
        if(s==null) return new Rect(0,0,1600,900);
        return new Rect(s.WorkingArea.X/s.Scaling,s.WorkingArea.Y/s.Scaling,s.WorkingArea.Width/s.Scaling,s.WorkingArea.Height/s.Scaling);
    }
    private Point Clamp(Point p) {var w=Work();return PetMotion.Clamp(p,new Rect(w.X,w.Y,Math.Max(0,w.Width-pet.Width),Math.Max(0,w.Height-pet.Height)));}
    private void Tick(object? sender,EventArgs args)
    {
        var now=clock.Elapsed.TotalSeconds; var dt=Math.Min(.1,Math.Max(0,now-previousTime)); previousTime=now;
        if(MacActivity.Pointer() is Point native) pointer=native;
        var input=activity.Drain(); points.Update(dt,input.Keys+input.Clicks>0,DateTimeOffset.UtcNow,input.Keys,input.Clicks);
        life.ActorSize=new Size(pet.Width,pet.Height); life.Habitat.PetCenter=position+new Vector(pet.Width/2,pet.Height/2);
        life.Update(dt,pointer,hidden || panel!=null || dragging);
        var before=position;
        if(!dragging && panel==null && !hidden)
        {
            if(life.Destination is Point target)
            {
                var difference=(Vector)(target-life.Habitat.PetCenter);
                if(difference.Length>life.ReachRadius) position+=difference*(Math.Min(difference.Length,45*dt)/difference.Length);
            }
            else if(life.Brain.Current.Name=="Wander") position=motion.Update(position,new Rect(Work().X,Work().Y,Math.Max(0,Work().Width-pet.Width),Math.Max(0,Work().Height-pet.Height)),dt);
        }
        position=Clamp(position); SetPosition(pet,position); life.Data.PetX=position.X;life.Data.PetY=position.Y;
        var movement=(Vector)(position-before); var beat=(int)(now/.45)%2;
        var id=life.Character.AssetSet;
        if(snack!=null && now<snackUntil) petView.Sprite=Sprites.Snack(id,snack.AnimationFrame+beat);
        else if(now<reactUntil && reaction.HasValue) petView.Sprite=Sprites.Frame(id,24+(int)reaction.Value);
        else if(movement.Length>.01) { facing=Direction(movement);petView.Sprite=Sprites.Frame(id,(1+(int)(now/.18)%2)*8+facing); }
        else if(life.Pose is PetPose pose) petView.Sprite=Sprites.Frame(id,24+(int)pose);
        else if(life.Brain.Current.Name=="LookAtCursor" && ((Vector)(pointer-life.Habitat.PetCenter)).Length<350)
        {
            var desired=Direction((Vector)(pointer-life.Habitat.PetCenter)); var difference=(desired-facing+12)%8-4;
            if(difference!=0 && now>=nextTurn) {facing=(facing+(difference>0?1:7))%8;nextTurn=now+.14;}
            petView.Sprite=Sprites.Frame(id,facing);
        }
        else {var idle=motion.Update(position,new Rect(position.X,position.Y,0,0),0);petView.Sprite=Sprites.Idle(id,now%6<.2?1:(int)(now/12)%4==1?2+beat:(int)(now/12)%4==2?4+beat:0);}
        SyncFurniture();
        var composite=furniture.Values.Any(f=>f.View.Actor!=null);
        petView.IsVisible=!composite;
        if(bubble!=null)
        {
            if(now>bubbleUntil) {bubble.Close();bubble=null;}
            else SetPosition(bubble,ClampPopup(new Point(position.X+(pet.Width-bubble.Width)/2,position.Y-bubble.Height+12),bubble));
        }
        if(!hidden && now>=nextTalk && life.Brain.Current.Name=="Idle") Speak("IdleTalk",true);
        if(!hidden && now>=nextTop)
        {
            nextTop=now+1;
            foreach(var f in furniture.Values.Where(f=>f.View.Actor==null))WindowLevel.Maintain(f.Window);
            WindowLevel.Maintain(pet);
            foreach(var f in furniture.Values.Where(f=>f.View.Actor!=null))WindowLevel.Maintain(f.Window);
            if(bubble!=null)WindowLevel.Maintain(bubble);if(panel!=null)WindowLevel.Maintain(panel);
        }
        if(!testing && now>=nextSave) {nextSave=now+15;_ = save.SaveAsync(life.Data);}
    }
    private static int Direction(Vector v)=>((int)Math.Floor(Math.Atan2(v.Y,v.X)/(Math.PI/4)+.5)+8)%8;
    private void SyncFurniture()
    {
        foreach(var key in furniture.Keys.Where(k=>!life.Data.Items.Any(i=>i.Id==k)).ToArray()) {furniture[key].Window.Close();furniture.Remove(key);}
        foreach(var item in life.Data.Items)
        {
            if(!furniture.TryGetValue(item.Id,out var pair))
            {
                var view=new SceneView(); var win=TransparentWindow(160,240);win.Content=view;
                Point start=new();PixelPoint screen=new();bool pressed=false,moved=false,actor=false;int clickCount=0;
                win.PointerPressed+=(_,e)=>
                {
                    var point=e.GetCurrentPoint(win);
                    if(point.Properties.IsRightButtonPressed) {if(life.Playing) life.StopPlay();ItemMenu(item);e.Handled=true;return;}
                    actor=view.ActorBounds.Contains(e.GetPosition(view)) && view.Actor!=null;
                    if(actor && armed!="") {ApplyTool(armed);return;}
                    if(item.Locked && !actor) return;
                    pressed=true;moved=false;clickCount=e.ClickCount;screen=win.PointToScreen(e.GetPosition(win));start=actor?position:new Point(item.X,item.Y);e.Pointer.Capture(win);
                    if(actor) {dragging=true;life.Rest(30);}
                };
                win.PointerMoved+=(_,e)=>
                {
                    if(!pressed)return;var delta=win.PointToScreen(e.GetPosition(win))-screen;
                    if(Math.Abs(delta.X)+Math.Abs(delta.Y)>4)moved=true;
                    if(actor)position=start+new Vector(delta.X/win.RenderScaling,delta.Y/win.RenderScaling);
                    else {item.X=start.X+delta.X/win.RenderScaling;item.Y=start.Y+delta.Y/win.RenderScaling;}
                };
                win.PointerReleased+=(_,e)=>{if(!pressed)return;pressed=false;e.Pointer.Capture(null);dragging=false;if(actor){if(moved)life.SeatOnBed();else life.Interact("Talk");}else if(!moved && clickCount>=2) life.UseItem(item);};
                pair=(win,view); furniture[item.Id]=pair;
            }
            var scale=life.Data.Settings.PetScale;pair.Window.Width=160*scale;pair.Window.Height=240*scale;
            SetPosition(pair.Window,new Point(item.X-40*scale,item.Y-160*scale));
            pair.View.Scale=scale;pair.View.Furniture=item.ItemId=="food-bowl"?Sprites.Bowl(item.FoodId,item.FoodQuantity>0?item.FoodPortion:0,life.InteractionItem==item&&life.InteractionArrived&&life.Brain.Current.Name=="Eat"&&life.InteractionProgress<1):item.ItemId=="water-bowl"?Sprites.Water(item.Water):Sprites.Furniture(item.ItemId);
            pair.View.Bed=life.Catalog[item.ItemId].Category=="Bed";pair.View.Bowl=item.ItemId is "food-bowl" or "water-bowl";pair.View.Cushion=item.ItemId=="cushion";pair.View.Actor=null;pair.View.Sign=item.Active?Sprites.Load("wood-quiet-sign","wood-quiet-sign-frames",false)[0]:null;
            if(life.InteractionArrived && life.InteractionItem==item)
            {
                var beat=(int)(clock.Elapsed.TotalSeconds/.45)%2;var id=life.Character.AssetSet;
                pair.View.Actor=life.Brain.Current.Name switch {"SitOnBed"=>Sprites.Actor(id,beat),"Sleep"=>Sprites.Actor(id,2+beat),"Eat" when life.InteractionProgress<1=>Sprites.Meal(id,item.FoodId,beat),"Drink" when life.InteractionProgress<1=>Sprites.Drink(id,beat),"Play"=>item.ItemId=="doll"?Sprites.Idle(id,8+beat):Sprites.Frame(id,31),_=>null};
            }
            pair.View.InvalidateVisual();if(!hidden&&!pair.Window.IsVisible)pair.Window.Show();
        }
    }
    private void PetPressed(object? sender,PointerPressedEventArgs e)
    {
        var p=e.GetCurrentPoint(pet);pressLocal=e.GetPosition(pet);pointer=position+new Vector(pressLocal.X,pressLocal.Y);
        if(p.Properties.IsRightButtonPressed){if(life.Playing)life.StopPlay();else Radial();e.Handled=true;return;}
        if(!p.Properties.IsLeftButtonPressed)return;
        pressScreen=pet.PointToScreen(pressLocal);pressWorld=position;strokeDistance=0;gesture=armed;
        if(gesture==""&&life.Data.Settings.DirectTouch) gesture=pressLocal.Y/pet.Height<CharacterSprites.FaceY-.10?"Pet":pressLocal.Y/pet.Height<CharacterSprites.FaceY+.21?"Chin":"";
        if(gesture is "Flick" or "Poke") {ApplyTool(gesture);gesture="";return;}
        dragging=true;e.Pointer.Capture(pet);if(gesture=="")life.Rest(30);
    }
    private void PetMoved(object? sender,PointerEventArgs e)
    {
        var local=e.GetPosition(pet);pointer=position+new Vector(local.X,local.Y);
        if(!dragging)return;
        var screen=pet.PointToScreen(local);var delta=new Vector((screen.X-pressScreen.X)/pet.RenderScaling,(screen.Y-pressScreen.Y)/pet.RenderScaling);
        if(gesture=="")position=pressWorld+delta;
        else if(gesture=="CheekPull") {petView.LeftCheek=pressLocal.X<pet.Width*.5;petView.CheekPull=Math.Clamp(petView.LeftCheek?-delta.X:delta.X,0,pet.Width*.3);petView.InvalidateVisual();reaction=petView.CheekPull>pet.Width*.18?PetPose.Annoyed:PetPose.Startled;reactUntil=clock.Elapsed.TotalSeconds+1;}
        else {strokeDistance+=Math.Abs(local.X-pressLocal.X);pressLocal=local;if(strokeDistance>pet.Width*.3){strokeDistance=0;life.Interact(gesture);}}
    }
    private void PetReleased(object? sender,PointerReleasedEventArgs e)
    {
        if(!dragging)return;dragging=false;e.Pointer.Capture(null);
        if(gesture=="CheekPull")life.Interact("CheekPull");
        else if(gesture=="") {if(((Vector)(position-pressWorld)).Length<4)life.Interact("Talk");else{motion.Rest();life.SeatOnBed();}}
        petView.CheekPull=0;gesture="";petView.InvalidateVisual();
    }
    private void ApplyTool(string kind){life.Interact(kind);if(kind is "Flick" or "Poke")Arm("");}
    private void Arm(string kind)
    {
        armed=kind;panel?.Close();panel=null;
        if(kind==""){pet.Cursor=null;foreach(var item in furniture.Values)item.Window.Cursor=null;return;}
        var frame=kind=="Flick"?Sprites.Load("flick-cursor","flick-cursor-frames")[0]:Sprites.Load("hand-cursors","hand-cursors-frames")[kind=="Pet"?0:kind=="Chin"?1:2];
        var view=new SpriteView{Width=36,Height=36,Sprite=frame};view.Measure(new Size(36,36));view.Arrange(new Rect(0,0,36,36));
        using var bitmap=new RenderTargetBitmap(new PixelSize(36,36));bitmap.Render(view);pet.Cursor=new Cursor(bitmap,new PixelPoint(8,18));
        foreach(var item in furniture.Values)item.Window.Cursor=pet.Cursor;
    }
    private void Speak(string tag,bool automatic)
    {
        var now=clock.Elapsed.TotalSeconds;
        if(hidden || automatic&&(life.Habitat.Quiet||panel!=null||life.Character.TalkFrequency<=0||now<nextTalk))return;
        nextTalk=now+life.Character.SpeechInterval;
        bubble?.Close();var text=dialogue.Pick(tag);var label=new TextBlock {FontSize=life.Data.Settings.SpeechFontSize,TextWrapping=TextWrapping.Wrap,MaxWidth=190*life.Data.Settings.BubbleScale,Foreground=Brush(life.Character.Theme.Ink)};
        var frame=new Border {Background=Brushes.White,BorderBrush=Brush(life.Character.Theme.Accent),BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(12),Padding=new Thickness(9,6),Child=label};
        var win=TransparentWindow(50,35);win.SizeToContent=SizeToContent.WidthAndHeight;win.Content=frame;bubble=win;win.Show();bubbleUntil=now+3+text.Length*.08;
        var index=0;DispatcherTimer.Run(()=>{if(bubble!=win||index>=text.Length)return false;label.Text=text[..++index];win.UpdateLayout();return true;},TimeSpan.FromMilliseconds(45));
    }
    private Point ClampPopup(Point p,Window w){var work=Work();return new Point(Math.Clamp(p.X,work.X,Math.Max(work.X,work.Right-w.Width)),Math.Clamp(p.Y,work.Y,Math.Max(work.Y,work.Bottom-w.Height)));}
    private static IBrush Brush(string hex)=>new SolidColorBrush(Color.Parse(hex));
    private void Recall(){hidden=false;position=Clamp(position);pet.Show();SyncFurniture();}
    private void Hide(){hidden=true;pet.Hide();foreach(var f in furniture.Values)f.Window.Hide();panel?.Close();bubble?.Close();bubble=null;}
    private void Exit()=>desktop.Shutdown();
    public void Dispose(){if(disposed)return;disposed=true;updateCancellation.Cancel();timer.Stop();activity.Dispose();if(!testing)save.SaveAsync(life.Data).GetAwaiter().GetResult();tray?.Dispose();panel?.Close();bubble?.Close();foreach(var f in furniture.Values)f.Window.Close();pet.Close();}
}

public sealed class SceneView:Control
{
    public double Scale=1;
    public Sprite? Furniture,Actor,Sign;
    public bool Bed,Cushion,Bowl;
    public Rect ActorBounds {get;private set;}
    public override void Render(DrawingContext context)
    {
        base.Render(context);if(Furniture==null)return;
        var width=80*.84*Scale*(Cushion?1.19:1);var height=width*Furniture.Crop.Height/Furniture.Crop.Width;
        var draw=new Rect((Bounds.Width-width)/2,Bounds.Height-7-height,width,height);
        if(Bowl)
        {
            var canvasWidth=width*360/280;var a=Furniture.Draw;
            Sprites.Draw(context,Furniture,new Rect((Bounds.Width-canvasWidth)/2+a.X*canvasWidth/240,Bounds.Height-7-canvasWidth*355/360+a.Y*canvasWidth/240,a.Width*canvasWidth/240,a.Height*canvasWidth/240));
        }
        else Sprites.Draw(context,Furniture,draw);
        if(Actor!=null)
        {
            var aw=Bed?width*1.15:128*Scale*.95;
            ActorBounds=new Rect((Bounds.Width-aw)/2,Bounds.Height-7-width*(Bed?.16:.34)-aw*235/240,aw,aw);
            var a=Actor.Draw;var actual=new Rect(ActorBounds.X+a.X*aw/240,ActorBounds.Y+a.Y*aw/240,a.Width*aw/240,a.Height*aw/240);
            Sprites.Draw(context,Actor,actual);
            if(Bed)using(context.PushClip(new Rect(draw.X,draw.Y+draw.Height*.68,draw.Width,draw.Height*.32)))Sprites.Draw(context,Furniture,draw);
        }
        if(Sign!=null){var sw=width*.7;Sprites.Draw(context,Sign,new Rect((Bounds.Width-sw)/2,Bounds.Height-7-sw*.5,sw,sw*Sign.Crop.Height/Sign.Crop.Width));}
    }
}
