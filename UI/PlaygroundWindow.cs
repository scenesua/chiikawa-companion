using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Momonga.Platform;
using Momonga.Simulation;

namespace Momonga.UI;

public sealed class PlaygroundWindow : Window
{
    private readonly PlaygroundGame game;
    private readonly PlaygroundView scene;
    private readonly BallGame? ball;
    private bool held;
    private Point pointer;
    public PlaygroundMode Mode { get; }
    public PlaygroundWindow(PlaygroundGame game, Action stop, BallGame? ball = null)
    {
        this.game=game; this.ball=ball; Mode=game.Mode; scene=new PlaygroundView(game);
        WindowStyle=WindowStyle.None; AllowsTransparency=true; Background=new SolidColorBrush(Color.FromArgb(1,0,0,0));
        ResizeMode=ResizeMode.NoResize; ShowInTaskbar=ShowActivated=false; Topmost=true;
        var grid=new Grid(); grid.Children.Add(scene);
        var tip=Theme.Text(Mode==PlaygroundMode.Bubbles ? "클릭하거나 누르고 있기 · 우클릭으로 놀이 끝내기" : Mode==PlaygroundMode.Ball ? "당겼다 놓아서 공 던지기 · 우클릭으로 놀이 끝내기" : "당겼다 놓아서 간식 날리기 · 우클릭으로 놀이 끝내기",12);
        var card=Theme.Card(tip); card.HorizontalAlignment=HorizontalAlignment.Left; card.VerticalAlignment=VerticalAlignment.Top;card.Margin=new Thickness(16);
        grid.Children.Add(card); Content=grid;
        Cursor=Mode==PlaygroundMode.Bubbles ? Icons.BubbleWand : Cursors.Cross;
        SourceInitialized+=(_,_)=>WindowPolicy.Passive(this,false);
        MouseRightButtonUp+=(_,e)=>{stop();e.Handled=true;};
        MouseLeftButtonDown+=(_,e)=>
        {
            if(Mode==PlaygroundMode.Ball && !ball!.Grab())return;
            held=true; pointer=World(e); CaptureMouse();
            if(Mode==PlaygroundMode.Bubbles)game.Blow(pointer);else {game.BeginAim(pointer);if(Mode==PlaygroundMode.Ball)ball!.Drag(pointer);}
            Refresh();e.Handled=true;
        };
        MouseMove+=(_,e)=>{pointer=World(e);if(held && Mode!=PlaygroundMode.Bubbles){game.Pull(pointer);if(Mode==PlaygroundMode.Ball)ball!.Drag(game.AimPointer);}scene.InvalidateVisual();};
        MouseLeftButtonUp+=(_,e)=>{if(held && Mode!=PlaygroundMode.Bubbles)game.Pull(World(e));Release();ReleaseMouseCapture();e.Handled=true;};
        LostMouseCapture+=(_,_)=>Release();
        KeyDown+=(_,e)=>{if(e.Key==Key.Escape)stop();};
        Closed+=(_,_)=>{held=false;Mouse.OverrideCursor=null;ToolCursor.Restore();};
        Refresh();
    }
    private Point World(MouseEventArgs e) => e.GetPosition(this)+new Vector(Left,Top);
    private void Release() { if(!held)return;held=false;if(Mode==PlaygroundMode.Snacks)game.ReleaseAim();else if(Mode==PlaygroundMode.Ball)game.ReleaseBall(ball!); }
    public void Refresh()
    {
        Left=game.Bounds.Left;Top=game.Bounds.Top;Width=game.Bounds.Width;Height=game.Bounds.Height;
        if(held && Mode==PlaygroundMode.Bubbles)game.Blow(pointer);
        scene.InvalidateVisual();
    }
}

public sealed class PlaygroundView : FrameworkElement
{
    private readonly PlaygroundGame game;
    private readonly BitmapImage cookie=new(new Uri("pack://application:,,,/Assets/play-cookie.png"));
    private static readonly Brush rim=new SolidColorBrush(Color.FromArgb(215,161,188,230));
    private static readonly Brush tint=new SolidColorBrush(Color.FromArgb(35,176,223,250));
    public PlaygroundView(PlaygroundGame game) {this.game=game;IsHitTestVisible=false;}
    protected override void OnRender(DrawingContext dc)
    {
        var shift=new Vector(-game.Bounds.Left,-game.Bounds.Top);
        foreach(var p in game.Particles)
        {
            if(p.Age<0 || game.Mode==PlaygroundMode.Snacks && p.Popped && !p.Bonked)continue;
            var center=p.Position+shift;var r=p.Radius;
            if(game.Mode==PlaygroundMode.Snacks) {dc.PushOpacity(p.Bonked?Math.Max(0,1-p.PopAge/.3):1);dc.DrawImage(cookie,new Rect(center.X-r,center.Y-r,r*2,r*2));dc.Pop();continue;}
            dc.PushOpacity(p.Popped ? Math.Max(0,1-p.PopAge/.3) : Math.Min(1,(9-p.Age)/1.5));
            if(p.Popped)
            {
                for(var i=0;i<8;i++)
                {var direction=new Vector(Math.Cos(i*Math.PI/4),Math.Sin(i*Math.PI/4));dc.DrawLine(new Pen(rim,2),center+direction*r,center+direction*r*(1.4+p.PopAge*3));}
            }
            else
            {
                dc.DrawEllipse(tint,new Pen(rim,1.5),center,r,r);
                dc.DrawEllipse(Brushes.White,null,center+new Vector(-r*.38,-r*.40),r*.15,r*.24);
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(150,246,188,226)),null,center+new Vector(r*.43,r*.46),r*.24,r*.10);
            }
            dc.Pop();
        }
        if(game.Aiming)
        {
            var origin=game.AimOrigin+shift;var pull=game.AimPointer+shift;
            var band=new Pen(new SolidColorBrush(Color.FromRgb(130,91,106)),3);
            dc.DrawLine(band,origin+new Vector(0,-12),pull);dc.DrawLine(band,origin+new Vector(0,12),pull);
            dc.DrawEllipse(Brushes.White,band,origin,4,4);
            dc.DrawImage(game.Mode==PlaygroundMode.Ball?Momonga.Animation.FurnitureSprites.Item("ball"):cookie,new Rect(pull.X-18,pull.Y-18,36,36));
            for(var i=1;i<=6;i++) {var t=i*.05;var dot=pull+game.AimVelocity*t+new Vector(0,game.Mode==PlaygroundMode.Ball?0:160*t*t);dc.DrawEllipse(rim,null,dot,2,2);}
        }
    }
}
