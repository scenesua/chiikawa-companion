using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Momonga.Platform;

namespace Momonga.UI;
public sealed class SpeechBubbleWindow : Window
{
    private readonly TextBlock words = Theme.Text("", 13, true);
    private readonly Momonga.Persistence.Settings settings;
    private readonly DispatcherTimer typing = new() { Interval = TimeSpan.FromMilliseconds(38) };
    private string line = "";
    private int[] elements = Array.Empty<int>();
    private int shown;
    private Window? anchor;
    public string VisibleText => words.Text;
    public static double Duration(string text) => StringInfo.ParseCombiningCharacters(text).Length * 0.038 + 4;
    public SpeechBubbleWindow(Momonga.Persistence.Settings? settings = null)
    {
        this.settings = settings ?? new();
        Width = 62; Height = 64; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true; ResizeMode = ResizeMode.NoResize;
        words.Margin = new Thickness(0); words.TextAlignment = TextAlignment.Center;
        var panel = new Grid();
        var tail = new Path { Data = Geometry.Parse("M 0 0 L 24 0 L 10 19 Z"), Fill = Brushes.White, Stroke = Theme.Brush("Ink"), StrokeThickness = 2,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(12,0,0,1) };
        panel.Children.Add(tail);
        panel.Children.Add(new Border { Background = Brushes.White, BorderBrush = Theme.Brush("Ink"), BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(22,25,20,24), Padding = new Thickness(10,7,10,7), Margin = new Thickness(2,2,2,16), Child = words });
        // Cover the shared tail edge to keep one continuous comic outline.
        panel.Children.Add(new Border { Width = 20, Height = 4, Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(12,0,0,16) });
        Content = panel;
        typing.Tick += (_, _) => RevealNext();
        IsVisibleChanged += (_, _) => { if (!IsVisible) typing.Stop(); };
        Closed += (_, _) => typing.Stop();
        SourceInitialized += (_, _) => WindowPolicy.Passive(this, true);
    }
    public void Say(string text, Window pet)
    {
        typing.Stop(); anchor = pet; line = text; elements = StringInfo.ParseCombiningCharacters(text); shown = 0;
        words.Text = ""; ResizeBubble(); Show(); if (elements.Length > 0) typing.Start();
    }
    public void RevealNext()
    {
        if (shown >= elements.Length) { typing.Stop(); return; }
        shown++; words.Text = line[..(shown == elements.Length ? line.Length : elements[shown])]; ResizeBubble();
        if (shown == elements.Length) typing.Stop();
    }
    public void Follow()
    {
        if (anchor == null) return;
        var work = MonitorService.At(anchor, new Point(anchor.Left, anchor.Top)).Work;
        Left = Math.Clamp(anchor.Left + anchor.Width / 2 - Width / 2, work.Left, Math.Max(work.Left, work.Right - Width));
        Top = Math.Clamp(anchor.Top - Height + 12, work.Top, Math.Max(work.Top, work.Bottom - Height));
    }
    public void RefreshSettings() => ResizeBubble();
    public void RevealAll() { typing.Stop(); shown = elements.Length; words.Text = line; ResizeBubble(); }
    private void ResizeBubble()
    {
        var work = anchor == null ? SystemParameters.WorkArea : MonitorService.At(anchor, new Point(anchor.Left, anchor.Top)).Work;
        var maximum = Math.Max(40, Math.Min(180, work.Width - 44));
        words.FontSize = settings.SpeechFontSize;
        ((FrameworkElement)Content).LayoutTransform = new ScaleTransform(settings.BubbleScale, settings.BubbleScale);
        words.Width = double.NaN;
        words.Measure(new Size(maximum, double.PositiveInfinity));
        var textWidth = Math.Clamp(words.DesiredSize.Width, 18, maximum);
        words.Width = textWidth; words.Measure(new Size(textWidth, double.PositiveInfinity));
        Width = (textWidth + 28) * settings.BubbleScale; Height = Math.Max(42, words.DesiredSize.Height + 36) * settings.BubbleScale;
        Follow();
    }
}
