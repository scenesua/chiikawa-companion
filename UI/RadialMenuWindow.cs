using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Momonga.Simulation;

namespace Momonga.UI;

public sealed class RadialMenuWindow : Window
{
    private readonly LifeSimulation life;
    private readonly Action<string> command;
    private readonly Canvas ring = new() { Width = 420, Height = 332, Background = Brushes.Transparent };
    private readonly NeedsStrip needs = new();
    private readonly TextBlock balance = Theme.Text("", 12, true);
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly Func<ImageSource?> portrait;
    private readonly Momonga.Persistence.HabitatItem? item;
    private Point center;
    private Border statusCard = null!;
    private string category = "";
    private bool closing;

    public RadialMenuWindow(LifeSimulation life, Func<ImageSource?> portrait, Action<string> command, Momonga.Persistence.HabitatItem? item = null, Rect? workArea = null)
    {
        this.item = item; this.life = life; this.command = command; this.portrait = portrait;
        Title = life.Character.DisplayName + " · 상호작용"; var work = workArea ?? SystemParameters.WorkArea; Left = work.Left; Top = work.Top; Width = work.Width; Height = work.Height;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true;
        ring.Width = Width; ring.Height = Height; var panel = ring;
        var status = new StackPanel(); balance.Margin = new Thickness(8, 0, 8, 3);
        var walletRow = new StackPanel { Orientation = Orientation.Horizontal };
        walletRow.Children.Add(new Image { Source = Momonga.Animation.CharacterSprites.Emblem, Width = 28, Height = 28, Stretch = Stretch.Uniform, Margin = new Thickness(0,0,6,0) });
        walletRow.Children.Add(Icons.Image(15)); walletRow.Children.Add(balance); status.Children.Add(walletRow); status.Children.Add(needs);
        statusCard = Theme.Card(status); statusCard.Width = 360;
        Content = panel; Build();
        ring.MouseDown += (_, e) => { if (e.OriginalSource == ring) Close(); };
        void RefreshStatus() { needs.Refresh(life.Data.Pet); balance.Text = "" + life.Data.ActivityPoints + " AP   ·   " +
            (life.Quiet ? "방석 곁에서 조용히 쉬는 중" : "함께 있는 시간"); }
        refresh.Tick += (_, _) => RefreshStatus();
        Loaded += (_, _) => { RefreshStatus(); refresh.Start(); };
        Closed += (_, _) => refresh.Stop();
        Closing += (_, _) => closing = true;
        Deactivated += (_, _) => { if (!closing) Close(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { if (category != "") { category = ""; Build(); } else Close(); } };
    }

    private void Build()
    {
        ring.Children.Clear();
        var scale = life.Data.Settings.PetScale;
        center = item == null ? new Point(life.Data.PetX + 64 * scale - Left, life.Data.PetY + 68 * scale - Top)
            : new Point(item.X + life.Habitat.SizeOf(item).Width / 2 - Left, item.Y + life.Habitat.SizeOf(item).Height / 2 - Top);
        var choices = item != null ? ItemActions() : category == "" ? new[] { ("애정", "@애정"), ("돌보기", "@돌보기"), ("놀기", "@놀기"),
            ("장난", "@장난"), ("대화", "@대화"), ("생활", "@생활"), ("더보기", "@더보기") } : Actions(category);
        if (category != "" && item == null) choices = choices.Append(("뒤로", "@뒤로")).ToArray();
        var radius = Math.Max(175, 64 * scale + 70);
        var available = new System.Collections.Generic.List<double>();
        for (var attempt = 0; attempt < 12; attempt++, radius += 18)
        {
            available.Clear();
            foreach (var angle in Enumerable.Range(0,144).Select(i => -Math.PI/2 + i*Math.PI/72))
            {
                var box = new Rect(center.X + Math.Cos(angle)*radius-55, center.Y + Math.Sin(angle)*radius-24,110,48);
                if (box.Left < 0 || box.Right > Width || box.Top < 0 || box.Bottom > Height) continue;
                if (available.Any(a => box.IntersectsWith(new Rect(center.X+Math.Cos(a)*radius-55,center.Y+Math.Sin(a)*radius-24,110,48)))) continue;
                available.Add(angle);
            }
            if (available.Count >= choices.Length) break;
        }
        var angles = available.ToArray();
        for (var i = 0; i < choices.Length; i++)
        {
            var choice = choices[i]; var angle = angles.Length == 0 ? -Math.PI / 2 + i * Math.PI * 2 / choices.Length : angles[Math.Min(angles.Length-1, i*angles.Length/choices.Length)];
            var button = Theme.Button(choice.Item1, () =>
            {
                if (choice.Item2.StartsWith("@")) { category = choice.Item2 == "@뒤로" ? "" : choice.Item2[1..]; Build(); }
                else { Close(); command(choice.Item2); }
            }); button.Width = 104; button.Height = 42; button.Padding = new Thickness(5);
            button.ToolTip = choice.Item1;
            if (category == "" && item == null) button.Content = Icons.Label(i, choice.Item1);
            else if (choice.Item2 is "Settings" or "Shop" or "Inventory" or "HabitatEdit" or "Debug") button.Content = Icons.Label(choice.Item2 switch { "Settings" => 7, "Shop" => 8, "Inventory" => 9, "HabitatEdit" => 16, _ => 17 }, choice.Item1);
            Canvas.SetLeft(button, Math.Clamp(center.X + Math.Cos(angle) * radius - 52, 0, Width - 104)); Canvas.SetTop(button, Math.Clamp(center.Y + Math.Sin(angle) * radius - 21, 0, Height - 42));
            ring.Children.Add(button);
        }
        Canvas.SetLeft(statusCard, Math.Clamp(center.X - 180, 0, Math.Max(0,Width - 360)));
        Canvas.SetTop(statusCard, center.Y + radius + 175 < Height ? center.Y + radius + 35 : Math.Max(0,center.Y - radius - 200));
        ring.Children.Add(statusCard);
    }

    private (string,string)[] ItemActions()
    {
        var common = new[] { ("크기 설정", "Settings"), (item!.Locked ? "잠금 해제" : "위치 잠금", "Lock"), ("회전", "Rotate"), ("치우기", "Remove") };
        var actions = item.ItemId switch
        {
            "food-bowl" => new[] { ("밥 먹기", "Use"), ("밥 넣기", "Food"), ("밥 상점", "ShopFood"), ("무료 흰밥", "FreeFood") },
            "water-bowl" => new[] { ("물 마시기", "Use"), ("물 채우기", "Water") },
            _ => life.Catalog[item.ItemId].Category == "Bed" ? new[] { ("잠자기", "Use"), ("깨우기", "Wake"), (item.Active ? "방해금지 끄기" : "방해금지", "Sign") } : new[] { ("놀기", "Use"), ("놀이 끝내기", "StopPlay") }
        }; return actions.Concat(common).ToArray();
    }
    private (string, string)[] Actions(string selected) => selected switch
    {
        "애정" => new[] { ("머리 복복복", "Pet"), ("턱 복복복", "Chin"), ("칭찬", "Praise"), ("달래기", "Soothe") },
        "돌보기" => new[] { ("밥 넣기", "Food"), ("간식 주기", "Snack"), ("물 채우기", "Water"), ("보관함", "Inventory"), ("간식 거절", "Refuse") },
        "놀기" => new[] { ("공 놀이", "BallPlay"), ("비눗방울", "BubblePlay"), ("간식 받기", "SnackPlay"), ("같이 놀기", "Play"), ("놀이 끝내기", "StopPlay"), ("커서 놀이", "CursorPlay") },
        "장난" => new[] { ("콕 찌르기", "Poke"), ("볼 당기기", "CheekArm"), ("딱밤 준비", "FlickArm") },
        "대화" => new[] { ("이름 부르기", "Talk:Greeting"), ("뭐 해?", "Talk:IdleTalk"), ("배고파?", "Talk:Hungry"), ("졸려?", "Talk:Sleepy") },
        "생활" => new[] { ("이리 와", "Come"), ("밥 먹어", "Force:Eat"), ("물 마셔", "Force:Drink"), ("자러 가", "Force:Sleep"), ("깨우기", "Wake"), ("잠깐 쉬기", "Rest") },
        _ => new[] { ("캐릭터 변경", "Characters"), ("설정", "Settings"), ("상점", "Shop"), ("보관함", "Inventory"), 
            ("디버그", "Debug"), ("잠시 숨기기", "Hide"), ("종료", "Quit") }
    };
}
