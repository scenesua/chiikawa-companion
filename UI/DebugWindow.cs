using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Momonga.UI;

public sealed class DebugWindow : Window
{
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly TextBlock status = new() { FontFamily = new System.Windows.Media.FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap };
    private bool exiting;

    public DebugWindow(Func<string> describe, Action<string, double> set, Action<string> command, IEnumerable<string> behaviors)
    {
        Title = "모몽가 · 디버그"; Width = 540; Height = 680; MinWidth = 400; MinHeight = 420;
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(status);
        var field = new ComboBox { Margin = new Thickness(0, 12, 0, 4), ItemsSource = new[]
            { "Hunger", "Thirst", "Energy", "Social", "Fun", "Mood", "Stress", "Annoyance", "Curiosity", "Affection", "Trust" }, SelectedIndex = 0 };
        var value = new Slider { Minimum = 0, Maximum = 100, Value = 50, TickFrequency = 10, IsSnapToTickEnabled = false };
        panel.Children.Add(field); panel.Children.Add(value);
        panel.Children.Add(Button("선택 수치 설정", () => set((string)field.SelectedItem, value.Value)));
        var force = new ComboBox { ItemsSource = behaviors, SelectedIndex = 0, Margin = new Thickness(0, 10, 0, 4) };
        panel.Children.Add(force); panel.Children.Add(Button("행동 강제 실행", () => command("Force:" + force.SelectedItem)));
        var buttons = new WrapPanel();
        foreach (var (label, action) in new[] { ("AP +15000", "AddAP"), ("간식 요구", "Force:AskSnack"), ("밥 채우기", "FillFood"),
            ("밥 비우기", "EmptyFood"), ("물 채우기", "FillWater"), ("물 비우기", "EmptyWater"), ("펫 방석 근처/멀리", "ToggleInside"),
            ("방석 방해금지", "ToggleSign"), ("10분 진행", "Advance:600"), ("1시간 진행", "Advance:3600") })
            buttons.Children.Add(Button(label, () => command(action)));
        panel.Children.Add(buttons);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        refresh.Tick += (_, _) => status.Text = describe();
        IsVisibleChanged += (_, _) => { refresh.IsEnabled = IsVisible; if (IsVisible) status.Text = describe(); };
        Closing += (_, e) => { if (!exiting) { e.Cancel = true; Hide(); } };
        Closed += (_, _) => refresh.Stop();
    }

    private static Button Button(string label, Action action)
    {
        var button = new Button { Content = label, Margin = new Thickness(3), Padding = new Thickness(8, 5, 8, 5) };
        button.Click += (_, _) => action(); return button;
    }
    public void Finish() { exiting = true; Close(); }
}
