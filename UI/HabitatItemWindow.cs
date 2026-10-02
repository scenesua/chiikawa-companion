using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Momonga.Inventory;
using Momonga.Persistence;
using Momonga.Platform;
using Momonga.Animation;

namespace Momonga.UI;

public sealed class HabitatItemWindow : Window
{
    public HabitatItem Item { get; }
    public Func<string,string>? FoodName { get; set; }
    private readonly Image symbol;
    private readonly TextBlock label;
    private readonly Border shape;
    private readonly Canvas canvas = new();
    private readonly Image foodContents = new() { IsHitTestVisible = false, Stretch = Stretch.Uniform };
    private readonly Image bedFront = new() { IsHitTestVisible=false, Stretch=Stretch.Uniform };
    private Rect petArea;
    public bool IsComposite => composite;
    internal Rect FurnitureBounds => new(Canvas.GetLeft(foodContents),Canvas.GetTop(foodContents),foodContents.Width,foodContents.Height);
    internal double ActorWidth => symbol.Width;
    private readonly Action<string, HabitatItem> command;
    private readonly bool bed;
    private readonly Image quietBadge = new() { Source = PetAnimator.LoadFrames("wood-quiet-sign", "wood-quiet-sign-frames", 1)[0], Stretch = Stretch.Uniform };
    private readonly ImageSource ordinarySource;
    private Size baseSize = new(80, 80);
    private bool composite;
    private bool petPress, petDragging;
    private bool itemDragging;
    private Point pressScreen, lastScreen;
    public Point SceneOffset { get; private set; }
    public event Action? PetClicked;
    public event Action? PetMenuRequested;
    public Func<bool>? ArmedClick { get; set; }
    public Func<Point,Point,bool>? GestureStart { get; set; }
    public event Action? PetDragStarted;
    public event Action<Vector>? PetDragDelta;
    public event Action? PetDragFinished;
    public void SetBaseSize(Size size) => baseSize = size;
    public event Action? Moved;
    public HabitatItemWindow(HabitatItem item, ItemDefinition definition, Action<string, HabitatItem> command)
    {
        Item = item; bed = definition.Category == "Bed"; this.command = command; Title = definition.Name; Width = Height = 80 * item.Scale;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true; ResizeMode = ResizeMode.NoResize;
        symbol = item.ItemId == "water-bowl" ? Icons.WaterBowl() : Icons.Image(item.ItemId switch { "food-bowl" => 1, "quiet-sign" => 5, "ball" => 2, "doll" => 0, _ => 16 }, 64);
        ordinarySource = symbol.Source;
        canvas.Children.Add(foodContents);
        canvas.Children.Add(symbol);
        canvas.Children.Add(bedFront); Panel.SetZIndex(bedFront,2); Panel.SetZIndex(quietBadge,3);
        label = Theme.Text(definition.Name, 10); label.TextAlignment = TextAlignment.Center; label.Visibility = Visibility.Collapsed;
        shape = new Border { CornerRadius = new CornerRadius(20), Background = Brushes.Transparent, BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0), Child = canvas,
            RenderTransformOrigin = new Point(0.5, 0.5) };
        var root = new Grid(); root.Children.Add(shape);
        quietBadge.HorizontalAlignment = HorizontalAlignment.Right; quietBadge.VerticalAlignment = VerticalAlignment.Top; quietBadge.IsHitTestVisible = false;
        canvas.Children.Add(quietBadge); Content = root; SourceInitialized += (_, _) => WindowPolicy.Passive(this, false);
        MouseLeftButtonDown += (_, e) =>
        {
            if (composite && petArea.Contains(e.GetPosition(this)))
            {
                e.Handled = true; if (ArmedClick?.Invoke() == true) return;
                var local = e.GetPosition(this);
                if (GestureStart?.Invoke(new Point((local.X-petArea.X)/petArea.Width, (local.Y-petArea.Y)/petArea.Height), PointToScreen(local)) == true) return;
                petPress = true; petDragging = false; pressScreen = lastScreen = PointToScreen(e.GetPosition(this)); CaptureMouse(); return;
            }
            if (e.ClickCount == 2) { command("Use", item); e.Handled = true; }
            else if (!item.Locked) { itemDragging = true; lastScreen = PointToScreen(e.GetPosition(this)); CaptureMouse(); e.Handled = true; }
        };
        ContextMenuOpening += (_, e) =>
        {
            e.Handled = true;
            if (composite && petArea.Contains(Mouse.GetPosition(this))) PetMenuRequested?.Invoke();
            else command("Menu", Item);
        };
        MouseMove += (_, e) =>
        {
            if (itemDragging && IsMouseCaptured)
            {
                var pointer = PointToScreen(e.GetPosition(this));
                var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice;
                var delta = transform?.Transform(pointer - lastScreen) ?? pointer - lastScreen;
                MoveItem(delta);
                lastScreen = pointer; return;
            }
            if (!petPress || !IsMouseCaptured) return;
            var current = PointToScreen(e.GetPosition(this));
            if (!petDragging && (current - pressScreen).Length > 5) { petDragging = true; PetDragStarted?.Invoke(); }
            if (petDragging)
            {
                var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice;
                PetDragDelta?.Invoke(transform?.Transform(current - lastScreen) ?? current - lastScreen);
            }
            lastScreen = current;
        };
        MouseLeftButtonUp += (_, _) => { if (petPress || itemDragging) ReleaseMouseCapture(); };
        LostMouseCapture += (_, _) =>
        {
            if (itemDragging) { itemDragging = false; Moved?.Invoke(); }
            if (!petPress) return; petPress = false;
            if (petDragging) PetDragFinished?.Invoke(); else PetClicked?.Invoke(); petDragging = false;
        };
        ContextMenu = new ContextMenu(); Refresh();
    }
    internal void MoveItem(Vector delta)
    {
        Item.X += delta.X; Item.Y += delta.Y;
        Left = Item.X + SceneOffset.X; Top = Item.Y + SceneOffset.Y;
    }
    public void Refresh(bool protest = false, int? sceneFrame = null, double depletion = 0, double actorWidth = 128)
    {
        composite = sceneFrame.HasValue;
        Width = baseSize.Width * 2; Height = baseSize.Width * 3;
        SceneOffset = new Point(-baseSize.Width / 2, baseSize.Height - Height);
        Left = Item.X + SceneOffset.X; Top = Item.Y + SceneOffset.Y;
        var targetWidth = baseSize.Width * 0.84 * (Item.ItemId == "cushion" ? 1.19 : 1);
        var furniture = bed || Item.ItemId is "ball" or "doll" ? FurnitureSprites.Item(Item.ItemId) : Item.ItemId == "food-bowl"
            ? Item.FoodQuantity > 0 ? BowlContents.Frame(Item.FoodId, Item.FoodPortion < .99999 || depletion >= .5, sceneFrame is 4 or 5) : BowlContents.Empty
            : Item.ItemId == "water-bowl" ? BowlContents.Water(Item.Water - depletion) : ordinarySource;
        var targetHeight = targetWidth * furniture.Height / furniture.Width;
        var left = baseSize.Width / 2 + baseSize.Width * 0.08;
        var top = Height - 7 - targetHeight;
        symbol.Stretch = Stretch.Uniform;
        foodContents.Source = furniture is System.Windows.Media.Imaging.BitmapSource bitmap && Item.ItemId != "quiet-sign" ? FurnitureTheme.Frame(bitmap,bed) : furniture;
        var bowl = Item.ItemId is "food-bowl" or "water-bowl";
        foodContents.Width = bowl ? targetWidth * 360 / 280 : targetWidth;
        foodContents.Height = bowl ? foodContents.Width : targetHeight;
        foodContents.Visibility = Visibility.Visible;
        Canvas.SetLeft(foodContents, bowl ? left-targetWidth*40/280 : left);
        Canvas.SetTop(foodContents, bowl ? Height-7-foodContents.Height*355/360 : top);
        bedFront.Visibility = bed && composite ? Visibility.Visible : Visibility.Collapsed;
        if (bed && composite)
        {
            bedFront.Source=foodContents.Source; bedFront.Width=targetWidth; bedFront.Height=targetHeight;
            bedFront.Clip=new RectangleGeometry(new Rect(0,targetHeight*.68,targetWidth,targetHeight*.32));
            Canvas.SetLeft(bedFront,left); Canvas.SetTop(bedFront,top);
        }
        if (sceneFrame.HasValue)
        {
            var play = sceneFrame.Value >= 8;
            var box = InteractionSprites.FurnitureAnchors[play ? 0 : sceneFrame.Value];
            var eating = sceneFrame.Value is 4 or 5;
            var baseline = !eating && CharacterSprites.Current == "momonga" ? box[1] : 235d / 240;
            var source = eating ? InteractionSprites.MealFrame(Item.FoodId,sceneFrame.Value - 4) : play ? InteractionSprites.PlayFrame(Item.ItemId,sceneFrame.Value % 2) : InteractionSprites.ActorFrame(sceneFrame.Value);
            // Actor and furniture render separately so furniture geometry never changes with a pose.
            symbol.Width = bed ? targetWidth * 1.15
                : actorWidth * (eating ? InteractionSprites.MealScale(Item.FoodId) : InteractionSprites.ActorScale(play ? 0 : sceneFrame.Value));
            symbol.Height = symbol.Width * source.Height / source.Width;
            var actorLeft = left + targetWidth / 2 - symbol.Width / 2;
            var rimHeight = targetWidth * (bed ? .16 : .34);
            var actorTop = Height - 7 - rimHeight - baseline * symbol.Height;
            Canvas.SetLeft(symbol, actorLeft); Canvas.SetTop(symbol, actorTop);
            symbol.Source = source;
            petArea = new Rect(actorLeft, actorTop, symbol.Width, baseline * symbol.Height);
            symbol.Visibility = Visibility.Visible;
            if (play)
            {
                var doll = Item.ItemId == "doll";
                foodContents.RenderTransformOrigin = new Point(.5,.5);
                foodContents.RenderTransform = new RotateTransform(doll ? (sceneFrame.Value % 2 == 0 ? -6 : 6) : Environment.TickCount64 / 12d % 360);
                Panel.SetZIndex(foodContents, doll ? 2 : 0);
                Canvas.SetTop(symbol, actorTop + (doll ? targetWidth*.12 : 0));
                petArea = new Rect(actorLeft,actorTop+(doll ? targetWidth*.12 : 0),symbol.Width,baseline*symbol.Height);
            }
        }
        else
        {
            symbol.Visibility = Visibility.Collapsed;
        }
        if (sceneFrame is not (8 or 9 or 10 or 11)) { foodContents.RenderTransform = Transform.Identity; Panel.SetZIndex(foodContents,0); }
        quietBadge.Visibility = bed && Item.Active ? Visibility.Visible : Visibility.Collapsed;
        quietBadge.Width = baseSize.Width * .7;
        quietBadge.Height = quietBadge.Width * quietBadge.Source.Height / quietBadge.Source.Width;
        Canvas.SetLeft(quietBadge, left + (targetWidth - quietBadge.Width) / 2);
        Canvas.SetTop(quietBadge, Height - 7 - quietBadge.Height * .8);
        shape.RenderTransform = new RotateTransform(Item.Rotation + (protest ? Math.Sin(Environment.TickCount64 / 160.0) * 5 : 0));
        if (Item.ItemId == "quiet-sign") { symbol.Opacity = Item.Active ? 1 : 0.4; label.Text = Item.Active ? "방해금지" : "팻말 접힘"; }
        if (Item.ItemId == "food-bowl") label.Text = Item.FoodQuantity > 0 ? $"{FoodName?.Invoke(Item.FoodId) ?? "밥"} · 남은 양 {Item.FoodPortion:P0}" : "빈 밥그릇";
        if (Item.ItemId == "water-bowl") label.Text = Item.Water > 0 ? "물 있어요" : "물 없음";
        ToolTip = label.Text + (Item.Locked ? " · 잠김" : " · 드래그 / 우클릭");
    }
}

