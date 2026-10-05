using Avalonia;
using Avalonia.Controls;
using Momonga.Animation;

namespace Momonga.Mac;

public sealed partial class CompanionController
{
    private Window? ballWindow;
    private void CloseBall() { ballWindow?.Close(); ballWindow=null; }
    private void SyncBall()
    {
        var ball=life.Ball;
        if(!ball.Active || hidden) {CloseBall();return;}
        if(ballWindow==null)
        {
            var win=TransparentWindow(ball.Radius*2,ball.Radius*2);ballWindow=win;
            win.Content=new SpriteView {Sprite=Sprites.Furniture("ball"),IsHitTestVisible=false};
            SetPosition(win,new Point(ball.Position.X-ball.Radius,ball.Position.Y-ball.Radius));win.Show();
            WindowLevel.IgnoreMouse(win);
        }
        ballWindow.Width=ballWindow.Height=ball.Radius*2;ballWindow.Opacity=ball.Opacity;
        SetPosition(ballWindow,new Point(ball.Position.X-ball.Radius,ball.Position.Y-ball.Radius));
        WindowLevel.Maintain(ballWindow);
        if(ball.BehindPet)WindowLevel.Maintain(pet);
    }
}
