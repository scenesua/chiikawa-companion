using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Momonga.Simulation;

namespace Momonga.UI;

public sealed class NeedsStrip : UniformGrid
{
    private readonly ProgressBar[] bars = new ProgressBar[5];
    private readonly TextBlock[] labels = new TextBlock[5];
    private readonly string[] names = { "식사", "물", "기운", "관심", "재미" };
    public NeedsStrip()
    {
        Columns = 5;
        for (var i = 0; i < 5; i++)
        {
            var panel = new StackPanel { Margin = new Thickness(6, 4, 6, 4) };
            panel.Children.Add(Icons.Image(10 + i, 22));
            panel.Children.Add(Theme.Text(names[i], 12, true));
            bars[i] = new ProgressBar { Minimum = 0, Maximum = 100, Height = 6, BorderThickness = new Thickness(0), Background = Theme.Brush("Soft") };
            labels[i] = Theme.Text("좋음", 10); labels[i].Foreground = Theme.Brush("Muted");
            panel.Children.Add(bars[i]); panel.Children.Add(labels[i]); Children.Add(panel);
        }
    }
    public void Refresh(PetState pet)
    {
        var values = new[] { 100 - pet.Hunger, 100 - pet.Thirst, pet.Energy, pet.Social, pet.Fun };
        for (var i = 0; i < 5; i++)
        {
            bars[i].Value = values[i]; bars[i].Foreground = Theme.Brush(values[i] < 30 ? "Critical" : values[i] < 60 ? "Warning" : "Healthy");
            labels[i].Text = values[i] < 30 ? "필요해" : values[i] < 60 ? "보통" : "좋음";
            AutomationProperties.SetName(bars[i], names[i] + ", " + labels[i].Text);
            bars[i].ToolTip = names[i] + " · " + labels[i].Text;
        }
    }
}
