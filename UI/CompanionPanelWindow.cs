using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Momonga.UI;

public class CompanionPanelWindow : Window
{
    public StackPanel Body { get; } = new() { Margin = new Thickness(20, 6, 20, 20) };
    public TextBlock Notice { get; } = Theme.Text("", 12);
    public void PlaceHud(Window pet)
    {
        if (Content is not Canvas canvas || canvas.Children[0] is not Border card) return;
        var work = Platform.MonitorService.At(pet, new Point(pet.Left, pet.Top)).Work;
        Left = work.Left; Top = work.Top; Width = work.Width; Height = work.Height;
        card.Height = System.Math.Min(560, work.Height - 24);
        Canvas.SetLeft(card, System.Math.Clamp(pet.Left + pet.Width / 2 - work.Left - card.Width / 2, 12, System.Math.Max(12, work.Width - card.Width - 12)));
        Canvas.SetTop(card, System.Math.Clamp(pet.Top - work.Top - card.Height - 12, 12, System.Math.Max(12, work.Height - card.Height - 12)));
    }

    public CompanionPanelWindow(string title, string subtitle, bool hud = false, System.Action? back = null)
    {
        Title = title; Width = 440; Height = 600; MinWidth = 360; MinHeight = 350;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = System.Windows.Media.Brushes.Transparent;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        var grid = new Grid(); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new Grid { Margin = new Thickness(22, 16, 14, 12) };
        header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var heading = new StackPanel(); heading.Children.Add(Theme.Text(title, 21, true));
        var caption = Theme.Text(subtitle, 12); caption.Foreground = Theme.Brush("Muted"); heading.Children.Add(caption);
        var identity = new StackPanel { Orientation = Orientation.Horizontal };
        identity.Children.Add(new Image { Source = Animation.CharacterSprites.Emblem, Width = 30, Height = 38, Margin = new Thickness(0,0,10,0), Stretch = System.Windows.Media.Stretch.Uniform });
        identity.Children.Add(heading); header.Children.Add(identity);
        var close = Theme.Button("", () => { Close(); back?.Invoke(); }); close.Content = Icons.Image(hud ? 19 : 18); close.ToolTip = hud ? "뒤로가기" : "닫기 (Esc)"; Grid.SetColumn(close, 1); header.Children.Add(close);
        heading.MouseLeftButtonDown += (_, e) => { if (!hud && e.LeftButton == MouseButtonState.Pressed) DragMove(); };
        grid.Children.Add(header);
        var scroll = new ScrollViewer { Content = Body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 1); grid.Children.Add(scroll);
        Notice.Margin = new Thickness(22, 8, 22, 16); Notice.Foreground = Theme.Brush("Muted");
        Grid.SetRow(Notice, 2); grid.Children.Add(Notice);
        Content = new Border { Background = Theme.Brush("Background"), BorderBrush = Theme.Brush("Border"), BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)Application.Current.FindResource("PetRadius"), Child = grid };
        if (hud)
        {
            var work = SystemParameters.WorkArea;
            var card = (Border)Content; card.Width = 360; card.Height = System.Math.Min(560, work.Height - 24);
            var backdrop = new Canvas { Background = System.Windows.Media.Brushes.Transparent };
            Content = null; backdrop.Children.Add(card); Content = backdrop;
            Width = work.Width; Height = work.Height; Left = work.Left; Top = work.Top;
            MinWidth = MinHeight = 0; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true;
            Canvas.SetLeft(card, System.Math.Clamp(work.Width * .65 - 180, 12, System.Math.Max(12, work.Width - 372)));
            Canvas.SetTop(card, System.Math.Max(12, work.Height - card.Height - 24));
            backdrop.MouseDown += (_, e) => { if (e.OriginalSource == backdrop) Close(); };
            var closing = false; Closing += (_, _) => closing = true;
            Deactivated += (_, _) => { if (!closing) Close(); };
        }
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }
}
