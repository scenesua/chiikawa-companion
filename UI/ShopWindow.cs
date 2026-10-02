using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Momonga.Simulation;

namespace Momonga.UI;

public sealed class ShopWindow : CompanionPanelWindow
{
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromSeconds(1) };
    public ShopWindow(LifeSimulation life, Action changed, string? filter = null, Action? back = null, Action? inventory = null) : base("작은 생활 상점", "평소 컴퓨터를 쓰면 생활비가 쌓여요", true, back)
    {
        var wallet = Theme.Text("", 17, true); var walletRow = new StackPanel { Orientation = Orientation.Horizontal }; walletRow.Children.Add(Icons.Image(15)); walletRow.Children.Add(wallet); Body.Children.Add(walletRow);
        if (inventory != null) Body.Children.Add(Theme.Button("보관함에서 사용하기", () => { Close(); inventory(); }));
        var buttons = new System.Collections.Generic.Dictionary<string, Button>();
        foreach (var category in new[] { "Food", "Snack", "Drink", "Toy", "Bed", "Habitat" }.Where(c => filter == null || c == filter))
        {
            Body.Children.Add(Theme.Text(category switch { "Food" => "밥", "Snack" => "간식", "Drink" => "음료", "Toy" => "놀이", "Bed" => "쉬는 곳", _ => "집 꾸미기" }, 16, true));
            var cards = new WrapPanel(); Body.Children.Add(cards);
            foreach (var item in life.Catalog.Values.Where(i => i.Category == category && i.Id != "quiet-sign"))
            {
                var row = new StackPanel { Width = 112 };
                row.Children.Add(new Image { Source = category == "Food" ? Momonga.Animation.BowlContents.Frame(item.Id) : category is "Snack" or "Drink" ? Momonga.Animation.FoodSprites.Frame(item.AnimationFrame) : Momonga.Animation.FurnitureSprites.Item(item.Id), Width = 100, Height = 85, Stretch = System.Windows.Media.Stretch.Uniform });
                row.Children.Add(Theme.Text(item.Name + "\n" + item.Price + " AP", 13));
                var buy = Theme.Button("구매", () =>
                {
                    Notice.Text = life.Shop.Buy(item.Id) ? item.Name + "을 보관함에 넣었어요." : "AP가 부족하거나 보관함이 가득 찼어요.";
                    changed(); Update();
                }); row.Children.Add(buy); buttons[item.Id] = buy;
                var card=Theme.Card(row); card.Padding=new Thickness(10); card.Margin=new Thickness(3); cards.Children.Add(card);
            }
        }
        void Update()
        {
            wallet.Text = " " + life.Data.ActivityPoints + " AP";
            foreach (var pair in buttons) pair.Value.IsEnabled = life.Data.ActivityPoints >= life.Catalog[pair.Key].Price && life.Shop.Quantity(pair.Key) < 9999;
        }
        refresh.Tick += (_, _) => Update(); Loaded += (_, _) => { Update(); refresh.Start(); };
        Closed += (_, _) => refresh.Stop();
    }
}

