using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Momonga.Simulation;

namespace Momonga.UI;

public sealed class InventoryWindow : CompanionPanelWindow
{
    public InventoryWindow(LifeSimulation life, Action changed, string? filter = null, Momonga.Persistence.HabitatItem? targetBowl = null, Action? back = null, Action? shop = null) : base(filter == "Food" ? "밥 넣기" : filter == "Snack" ? "간식 고르기" : "작은 보관함", "밥은 그릇에, 간식은 직접 주세요", true, back)
    {
        void Update()
        {
            Body.Children.Clear();
            var cards = new WrapPanel(); Body.Children.Add(cards);
            foreach (var item in life.Catalog.Values.Where(i => life.Shop.Quantity(i.Id) > 0 && i.Id != "quiet-sign" && (filter == null || i.Category == filter || filter == "Snack" && i.Category == "Drink")))
            {
                var panel = new StackPanel { Width = 112 };
                panel.Children.Add(new Image { Source = item.Category == "Food" ? Momonga.Animation.BowlContents.Frame(item.Id) : item.Category is "Snack" or "Drink" ? Momonga.Animation.FoodSprites.Frame(item.AnimationFrame) : Momonga.Animation.FurnitureSprites.Item(item.Id), Width = 100, Height = 85, Stretch = System.Windows.Media.Stretch.Uniform });
                panel.Children.Add(Theme.Text(item.Name + " · " + life.Shop.Quantity(item.Id) + "개", 15, true));
                if (life.Data.LeftoverFood.TryGetValue(item.Id,out var leftover)) panel.Children.Add(Theme.Text($"남긴 밥 {leftover:P0} 포함",11));
                if (item.Category == "Food")
                {
                    foreach (var bowl in life.Data.Items.Where(i => i.ItemId == "food-bowl" && (targetBowl == null || i == targetBowl)).ToArray())
                    {
                        var index = life.Data.Items.Where(i => i.ItemId == "food-bowl").ToList().IndexOf(bowl) + 1;
                        panel.Children.Add(Theme.Button("밥그릇 " + index + "에 넣기", () =>
                        { Notice.Text = life.Shop.FillFood(bowl, item.Id) ? "밥을 넣었어요." : "그릇에 다른 밥이 있거나 가득 찼어요."; changed(); Update(); }));
                    }
                    if (!life.Data.Items.Any(i => i.ItemId == "food-bowl")) panel.Children.Add(Theme.Text("먼저 밥그릇을 배치해주세요.", 12));
                }
                else if (item.Category is "Snack" or "Drink") panel.Children.Add(Theme.Button("간식 주기", () =>
                { life.GiveSnack(item.Id); changed(); Update(); }));
                else
                {
                    panel.Children.Add(Theme.Text("배치 가능 " + life.Shop.AvailableFurniture(item.Id) + "개", 12));
                    var place = Theme.Button("바탕화면에 놓기", () => { Notice.Text = life.Shop.Place(item.Id) != null ? "바탕화면에 놓았어요." : "배치할 물건이 없거나 최대 30개까지 놓을 수 있어요."; changed(); Update(); });
                    place.IsEnabled = life.Shop.AvailableFurniture(item.Id) > 0; panel.Children.Add(place);
                }
                var card=Theme.Card(panel); card.Padding=new Thickness(10); card.Margin=new Thickness(3); cards.Children.Add(card);
            }
            if (cards.Children.Count == 0) Body.Children.Add(Theme.Text("보관 중인 아이템이 없어요. 상점에서 골라주세요."));
            if (shop != null) Body.Children.Add(Theme.Button("상점 열기", () => { Close(); shop(); }));
        }
        Update();
    }
}

