using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Momonga.Animation;
using Momonga.Character;

namespace Momonga.UI;

public sealed class CharacterPickerWindow : CompanionPanelWindow
{
    public CharacterPickerWindow(string current, Action<string> select, Action? back = null)
        : base("캐릭터 변경", "함께 지낼 친구를 골라줘", true, back)
    {
        foreach (var group in CharacterDefinition.All.GroupBy(c => c.Category))
        {
            var grid = new WrapPanel();
            foreach (var character in group)
            {
                var content = new StackPanel();
                content.Children.Add(new Image { Source = CharacterSprites.Portrait(character.AssetSet), Width = 110, Height = 105, Stretch = Stretch.Uniform });
                var name = Theme.Text(character.DisplayName, 13, true); name.TextAlignment = TextAlignment.Center; content.Children.Add(name);
                var card = Theme.Button("", () => { Close(); select(character.CharacterId); });
                card.Content = content; card.Width = 140; card.Padding = new Thickness(6); card.ToolTip = character.Description;
                card.IsEnabled = current != character.CharacterId;
                grid.Children.Add(card);
            }
            Body.Children.Add(new Expander { Header = Theme.Text(group.Key + " · " + group.Count(), 15, true), Content = grid, IsExpanded = true, Margin = new Thickness(0, 4, 0, 10) });
        }
        Notice.Text = "AP와 배치한 물건은 그대로 함께해요.";
    }
}
