using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Momonga.Character;
using System.Windows.Data;

namespace Momonga.UI;

public static class Theme
{
    public static void Apply(ThemeDefinition theme)
    {
        foreach (var name in new[] { "Background", "Surface", "Ink", "Muted", "Accent", "Soft", "Border", "Healthy", "Warning", "Critical" })
        {
            var color = (Color)ColorConverter.ConvertFromString((string)typeof(ThemeDefinition).GetProperty(name)!.GetValue(theme)!);
            var brush = Application.Current.Resources["Pet" + name] is SolidColorBrush existing && !existing.IsFrozen
                ? existing : new SolidColorBrush();
            // WPF cannot freeze a bound brush; open panels retain a live palette when the character changes.
            BindingOperations.SetBinding(brush, SolidColorBrush.ColorProperty, new Binding { Source = color });
            Application.Current.Resources["Pet" + name] = brush;
        }
        if (!double.IsFinite(theme.CornerRadius) || theme.CornerRadius < 4 || theme.CornerRadius > 32)
            throw new InvalidOperationException("Invalid theme corner radius");
        Application.Current.Resources["PetRadius"] = new CornerRadius(theme.CornerRadius);
    }
    public static Brush Brush(string token) => (Brush)Application.Current.FindResource("Pet" + token);
    public static TextBlock Text(string text, double size = 14, bool bold = false) => new()
    { Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = Brush("Ink"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) };
    public static Button Button(string text, Action action)
    {
        var button = new Button { Content = text, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(3) };
        button.Click += (_, _) => action(); return button;
    }
    public static Border Card(UIElement content)
    {
        var card = new Border { Background = Brush("Surface"), BorderBrush = Brush("Border"), BorderThickness = new Thickness(1),
            Padding = new Thickness(14), Margin = new Thickness(0, 5, 0, 5), Child = content };
        card.SetResourceReference(Border.CornerRadiusProperty, "PetRadius"); return card;
    }
}
