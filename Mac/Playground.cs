using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Momonga.Simulation;
using Momonga.Animation;

namespace Momonga.Mac;

public sealed partial class CompanionController
{
    private Window? playgroundWindow;
    private PlaygroundMode playgroundMode;
    private Action? refreshPlayground;
    private void ClosePlayground() {playgroundWindow?.Close();playgroundWindow=null;refreshPlayground=null;}
    private void StartPlayground(PlaygroundMode mode)
    {
        Arm("");life.RefreshContext(false);life.StartPlayground(mode);SyncPlayground();
    }
    private void SyncPlayground()
    {
        var game=life.Playground;
        if(!game.Active || hidden || playgroundWindow!=null && playgroundMode!=game.Mode)ClosePlayground();
        if(!game.Active || hidden)return;
        if(playgroundWindow==null)
        {
            var win=TransparentWindow(game.Bounds.Width,game.Bounds.Height);playgroundWindow=win;playgroundMode=game.Mode;
            win.Background=new SolidColorBrush(Color.FromArgb(1,0,0,0));
            var view=new PlaygroundView(game);var grid=new Grid();grid.Children.Add(view);
            var tip=new TextBlock {Text=game.Mode==PlaygroundMode.Bubbles?"클릭하거나 누르고 있기 · 우클릭으로 놀이 끝내기":game.Mode==PlaygroundMode.Ball?"당겼다 놓아서 공 던지기 · 우클릭으로 놀이 끝내기":"당겼다 놓아서 간식 날리기 · 우클릭으로 놀이 끝내기",FontSize=12,Foreground=Brush(life.Character.Theme.Ink)};
            grid.Children.Add(new Border {Child=tip,Background=Brush(life.Character.Theme.Background),BorderBrush=Brush(life.Character.Theme.Border),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Padding=new Thickness(12,8),Margin=new Thickness(16),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top});win.Content=grid;
            if(game.Mode==PlaygroundMode.Bubbles)
            {
                using var stream=AssetLoader.Open(new Uri("avares://Chiikawa.Companion/Assets/bubble-wand.png"));
                using var wand=new Bitmap(stream);var icon=new Image {Source=wand,Width=32,Height=32,Stretch=Stretch.Uniform};icon.Measure(new Size(32,32));icon.Arrange(new Rect(0,0,32,32));
                using var bitmap=new RenderTargetBitmap(new PixelSize(32,32));bitmap.Render(icon);win.Cursor=new Cursor(bitmap,new PixelPoint(9,9));
            }
            else win.Cursor=new Cursor(StandardCursorType.Cross);
            var held=false;var pointer=new Point();
            Point WorldPoint(PointerEventArgs e) {var local=e.GetPosition(win);var origin=World(win);return origin+new Vector(local.X,local.Y);}
            void Release() {if(!held)return;held=false;if(game.Mode==PlaygroundMode.Snacks)game.ReleaseAim();else if(game.Mode==PlaygroundMode.Ball)game.ReleaseBall(life.Ball);}
            win.PointerPressed+=(_,e)=>
            {
                var p=e.GetCurrentPoint(win);
                if(p.Properties.IsRightButtonPressed) {life.StopPlay();e.Handled=true;return;}
                if(!p.Properties.IsLeftButtonPressed)return;
                if(game.Mode==PlaygroundMode.Ball && !life.Ball.Grab())return;
                held=true;pointer=WorldPoint(e);e.Pointer.Capture(win);
                if(game.Mode==PlaygroundMode.Bubbles)game.Blow(pointer);else {game.BeginAim(pointer);if(game.Mode==PlaygroundMode.Ball)life.Ball.Drag(pointer);}
                view.InvalidateVisual();e.Handled=true;
            };
            win.PointerMoved+=(_,e)=>{pointer=WorldPoint(e);if(held && game.Mode!=PlaygroundMode.Bubbles){game.Pull(pointer);if(game.Mode==PlaygroundMode.Ball)life.Ball.Drag(game.AimPointer);}view.InvalidateVisual();};
            win.PointerReleased+=(_,e)=>{if(held && game.Mode!=PlaygroundMode.Bubbles)game.Pull(WorldPoint(e));Release();e.Pointer.Capture(null);};
            win.PointerCaptureLost+=(_,_)=>Release();win.KeyDown+=(_,e)=>{if(e.Key==Key.Escape)life.StopPlay();};
            win.Closed+=(_,_)=>{held=false;view.Dispose();};
            refreshPlayground=()=>{if(held && game.Mode==PlaygroundMode.Bubbles)game.Blow(pointer);view.InvalidateVisual();};
            SetPosition(win,new Point(game.Bounds.X,game.Bounds.Y));win.Show();
        }
        playgroundWindow.Width=game.Bounds.Width;playgroundWindow.Height=game.Bounds.Height;
        SetPosition(playgroundWindow,new Point(game.Bounds.X,game.Bounds.Y));refreshPlayground?.Invoke();WindowLevel.Maintain(playgroundWindow);
    }
}

public sealed class PlaygroundView : Control, IDisposable
{
    private readonly PlaygroundGame game;
    private readonly Bitmap cookie;
    private static readonly IBrush rim=new SolidColorBrush(Color.FromArgb(215,161,188,230));
    private static readonly IBrush tint=new SolidColorBrush(Color.FromArgb(35,176,223,250));
    public PlaygroundView(PlaygroundGame game)
    {
        this.game=game;IsHitTestVisible=false;
        using var stream=AssetLoader.Open(new Uri("avares://Chiikawa.Companion/Assets/play-cookie.png"));cookie=new Bitmap(stream);
    }
    public void Dispose() => cookie.Dispose();
    public override void Render(DrawingContext dc)
    {
        base.Render(dc);var shift=new Vector(-game.Bounds.X,-game.Bounds.Y);
        foreach(var p in game.Particles)
        {
            if(p.Age<0 || game.Mode==PlaygroundMode.Snacks && p.Popped && !p.Bonked)continue;
            var center=p.Position+shift;var r=p.Radius;
            if(game.Mode==PlaygroundMode.Snacks) {using(dc.PushOpacity(p.Bonked?Math.Max(0,1-p.PopAge/.3):1))dc.DrawImage(cookie,new Rect(center.X-r,center.Y-r,r*2,r*2));continue;}
            using(dc.PushOpacity(p.Popped?Math.Max(0,1-p.PopAge/.3):Math.Min(1,(9-p.Age)/1.5)))
            {
                if(p.Popped)
                {
                    for(var i=0;i<8;i++){var d=new Vector(Math.Cos(i*Math.PI/4),Math.Sin(i*Math.PI/4));dc.DrawLine(new Pen(rim,2),center+d*r,center+d*r*(1.4+p.PopAge*3));}
                }
                else
                {
                    dc.DrawEllipse(tint,new Pen(rim,1.5),center,r,r);
                    dc.DrawEllipse(Brushes.White,null,center+new Vector(-r*.38,-r*.4),r*.15,r*.24);
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(150,246,188,226)),null,center+new Vector(r*.43,r*.46),r*.24,r*.10);
                }
            }
        }
        if(game.Aiming)
        {
            var origin=game.AimOrigin+shift;var pull=game.AimPointer+shift;var band=new Pen(new SolidColorBrush(Color.FromRgb(130,91,106)),3);
            dc.DrawLine(band,origin+new Vector(0,-12),pull);dc.DrawLine(band,origin+new Vector(0,12),pull);dc.DrawEllipse(Brushes.White,band,origin,4,4);
            if(game.Mode==PlaygroundMode.Ball)Sprites.Draw(dc,Sprites.Furniture("ball"),new Rect(pull.X-18,pull.Y-18,36,36));else dc.DrawImage(cookie,new Rect(pull.X-14,pull.Y-14,28,28));
            for(var i=1;i<=6;i++){var t=i*.05;var dot=pull+game.AimVelocity*t+new Vector(0,game.Mode==PlaygroundMode.Ball?0:160*t*t);dc.DrawEllipse(rim,null,dot,2,2);}
        }
    }
}
