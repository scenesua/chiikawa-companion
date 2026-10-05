using System.Windows.Media.Imaging;
using System;
using System.Text.Json;
using System.Windows;
namespace Momonga.Animation;
public static class InteractionSprites
{
    private static readonly System.Collections.Generic.Dictionary<string, BitmapSource[]> meals = new();
    private static readonly System.Collections.Generic.Dictionary<string, double> mealScales = new();
    public static int MealIndex(string food) => food switch
    { "furikake-rice" => 2, "curry-rice" => 4, "jiro-ramen" => 6, "nuts" => 8, "fruit" => 10, "meal" or "free-rice" or "white-rice" => 0, _ => throw new ArgumentException("Unknown meal", nameof(food)) };
    public static BitmapSource MealFrame(string food, int beat)
    {
        var id = CharacterSprites.Current;
        if (!meals.TryGetValue(id, out var result))
        {
            var source = PetAnimator.LoadFrames(id + "-meals", id + "-meal-frames", 12);
            var maxWidth = System.Linq.Enumerable.Max(source, f => f.Width);
            var maxHeight = System.Linq.Enumerable.Max(source, f => f.Height);
            var scale = Math.Min(228 / maxWidth, 228 / maxHeight);
            result = new BitmapSource[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var visual = new System.Windows.Media.DrawingVisual();
                using (var dc = visual.RenderOpen()) dc.DrawImage(source[i], new Rect((240 - source[i].Width * scale) / 2, 235 - maxHeight * scale, source[i].Width * scale, source[i].Height * scale));
                var padded = new RenderTargetBitmap(240,240,96,96,System.Windows.Media.PixelFormats.Pbgra32); padded.Render(visual); padded.Freeze(); result[i] = padded;
            }
            meals[id] = result;
            // Tail-bearing characters have a narrower body than their whole silhouette.
            var bodyFraction = id == "momonga" ? .70 : id == "anoko" ? .78 : 1;
            mealScales[id] = .72 / (maxWidth * scale / 240 * bodyFraction);
        }
        var index=MealIndex(food)+(beat&1);
        Momonga.UI.CharacterLayers.Register(result[index],id,"meal",index,2);
        return result[index];
    }
    public static double MealScale(string food) { _ = MealFrame(food,0); return mealScales[CharacterSprites.Current]; }
    private static readonly BitmapSource[] momongaHug = PetAnimator.LoadFrames("momonga-idle", "momonga-idle-frames",12);
    public static System.Windows.Media.ImageSource PlayFrame(string toy,int beat) => toy == "doll"
        ? CharacterSprites.Current == "momonga" ? momongaHug[8+(beat&1)] : CharacterSprites.Frame(40+(beat&1))
        : ActionSprites.Frame(8+(beat&1));
    private static readonly BitmapSource[] frames = PetAnimator.LoadFrames("momonga-interactions", "momonga-interaction-frames", 16);
    public static BitmapSource Frame(int index) => CharacterSprites.Current == "momonga" ? frames[index] : CharacterSprites.Frame(index >= 14 ? 76 + index - 14 : 56 + index % 8);
    public static readonly double[][] FurnitureAnchors = LoadAnchors();
    private static readonly BitmapSource[] actors = PetAnimator.LoadFrames("momonga-scene-actors", "momonga-scene-actor-frames", 8);
    private static readonly string[] drinkIds = { "chiikawa", "hachiware", "usagi", "kurimanju", "shisa", "kani", "rakko", "anoko", "goblin", "chiikabu", "ode", "rilakkuma", "korilakkuma", "mymelody", "kuromi", "dekatsuyo" };
    private static readonly System.Collections.Generic.Dictionary<string, BitmapSource[]> drinks = new();
    private static readonly System.Collections.Generic.Dictionary<string, double> drinkScales = new();
    private static BitmapSource DrinkFrame(int beat)
    {
        var id = CharacterSprites.Current;
        if (!drinks.TryGetValue(id, out var result))
        {
            var position = Array.IndexOf(drinkIds, id);
            var atlas = PetAnimator.LoadFrames("drinking-" + position / 4, "drinking-" + position / 4 + "-frames", 8);
            var source = new[] { atlas[position % 4 * 2], atlas[position % 4 * 2 + 1] };
            var maxWidth = Math.Max(source[0].Width, source[1].Width);
            var maxHeight = Math.Max(source[0].Height, source[1].Height);
            var scale = Math.Min(228 / maxWidth, 228 / maxHeight);
            result = new BitmapSource[2];
            for (var i = 0; i < 2; i++)
            {
                var visual = new System.Windows.Media.DrawingVisual();
                using (var dc = visual.RenderOpen()) dc.DrawImage(source[i], new Rect((240 - source[i].Width * scale) / 2, 235 - maxHeight * scale, source[i].Width * scale, source[i].Height * scale));
                var padded = new RenderTargetBitmap(240, 240, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); padded.Render(visual); padded.Freeze(); result[i] = padded;
            }
            drinks[id] = result;
            drinkScales[id] = .72 / (maxWidth * scale / 240);
        }
        Momonga.UI.CharacterLayers.Register(result[beat&1],id,"drink",beat&1,2);
        return result[beat & 1];
    }
    public static System.Windows.Media.ImageSource ActorFrame(int index) => CharacterSprites.Current == "momonga" ? actors[index] : index >= 6 ? DrinkFrame(index - 6) : CharacterSprites.Frame(56 + index);
    private static readonly double[] actorWidths = LoadActorWidths();
    public static double ActorScale(int index)
    {
        if (CharacterSprites.Current != "momonga" && index >= 6) { _ = DrinkFrame(0); return drinkScales[CharacterSprites.Current]; }
        return .72 / (CharacterSprites.Current == "momonga" ? actorWidths[index] : CharacterSprites.BodyWidth);
    }
    private static double[] LoadActorWidths()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/momonga-actor-widths.json")).Stream;
        return JsonSerializer.Deserialize<double[]>(stream)!;
    }
    private static double[][] LoadAnchors()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/momonga-furniture-anchors.json")).Stream;
        return JsonSerializer.Deserialize<double[][]>(stream)!;
    }
}
