using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using Momonga.Animation;
using Momonga.Platform;
using Momonga.Simulation;

namespace Momonga.UI;

public sealed class BallWindow : Window
{
    private readonly BallGame ball;
    public BallWindow(BallGame ball)
    {
        this.ball = ball;
        Width = Height = ball.Radius * 2;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize; ShowInTaskbar = ShowActivated = false; Topmost = true;
        Content = new Image { Source = FurnitureSprites.Item("ball"), Stretch = Stretch.Uniform, IsHitTestVisible = false };
        // Only the playground owns input; this window just draws the moving ball.
        SourceInitialized += (_, _) => WindowPolicy.Passive(this, true);
    }
    public void Refresh()
    {
        Width = Height = ball.Radius * 2; Left = ball.Position.X - ball.Radius; Top = ball.Position.Y - ball.Radius;
        Opacity = ball.Opacity;
    }
}
