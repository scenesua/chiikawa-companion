using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Momonga.Persistence;
using Momonga.Platform;

namespace Momonga.UI;

public sealed class HabitatZoneWindow : Window
{
    private readonly Border mat;
    private readonly TextBlock caption;
    public bool Editing { get; private set; }
    public event Action? Changed;
    public HabitatZoneWindow(HabitatZone zone)
    {
        Title = "모몽가 서식지"; Width = zone.Width; Height = zone.Height; Left = zone.X; Top = zone.Y;
        MinWidth = 280; MinHeight = 280; MaxWidth = 4000; MaxHeight = 4000; WindowStyle = WindowStyle.None; AllowsTransparency = true;
        Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        caption = Theme.Text("HOME", 12, true); caption.Margin = new Thickness(14); caption.VerticalAlignment = VerticalAlignment.Top;
        mat = new Border { CornerRadius = new CornerRadius(32), BorderBrush = Theme.Brush("Border"), BorderThickness = new Thickness(1), Child = caption };
        Content = mat;
        SourceInitialized += (_, _) => SetEditing(false);
        MouseLeftButtonDown += (_, e) => { if (Editing && e.LeftButton == MouseButtonState.Pressed) { DragMove(); Changed?.Invoke(); } };
        LocationChanged += (_, _) => { if (IsLoaded) Changed?.Invoke(); };
        SizeChanged += (_, _) => { if (IsLoaded) Changed?.Invoke(); };
    }
    public void SetEditing(bool edit)
    {
        Editing = edit; ResizeMode = edit ? ResizeMode.CanResizeWithGrip : ResizeMode.NoResize;
        var color = ((SolidColorBrush)Theme.Brush("Soft")).Color; color.A = edit ? (byte)210 : (byte)35;
        mat.Background = new SolidColorBrush(color);
        mat.BorderThickness = new Thickness(edit ? 2 : 1);
        caption.Text = edit ? "HOME · 드래그 / 모서리로 크기 조절" : "HOME";
        mat.IsHitTestVisible = edit; WindowPolicy.Passive(this, !edit);
    }
}
