using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Momonga.Animation;
using Momonga.Simulation;
using Momonga.Input;
using System.Windows.Controls;
using System.Windows.Media;

namespace Momonga.UI;

public partial class PetWindow : Window
{
    private Point lastPointer;
    private readonly PetAnimator animator;
    private double elapsed;
    private Point pressPoint;
    private HitRegion region;
    private bool stroking;
    private bool cheek;
    private bool moved;
    private bool flickArmed;
    private bool pokeArmed;
    private bool cheekArmed;
    private string strokeArmed = "";
    private readonly CheekDrag cheekDrag = new();
    private readonly CheekSurface cheekSurface = new();
    private Window? cheekWindow;
    public int CheekExpression => cheekDrag.Expression(elapsed);
    internal Window? CheekOverlay => cheekWindow;
    private int? actionFrame;
    private double actionUntil;
    private int? foodFrame;
    private double foodUntil;
    private bool drinkingBeer;
    private DateTime armedUntil;
    private double lastStroke;
    private bool externalDrag;
    public bool IsDragging { get; private set; }
    public bool IsReacting => animator.IsReacting(elapsed);
    public bool IsInteracting => IsMouseCaptured || externalDrag || cheekDrag.Active(elapsed);
    public void BeginExternalDrag() { externalDrag = IsDragging = true; ShowComposite(false); }
    public void EndExternalDrag() { externalDrag = IsDragging = false; DragFinished?.Invoke(); }
    public ImageSource? Portrait => Sprite.Source;
    public bool FlickArmed => flickArmed;
    public bool HasArmedInteraction => flickArmed || pokeArmed || cheekArmed || strokeArmed != "";
    public Func<bool> DirectTouch { get; set; } = () => false;
    private bool flickReaction;
    public void ShowComposite(bool show) => Sprite.Opacity = show ? 0 : 1;
    public event Action? DragFinished;
    public event Action? RecallRequested;
    public event Action? RadialRequested;
    public event Action? HideRequested;
    public event Action<string>? InteractionRequested;

    public PetWindow(string assetSet = "momonga")
    {
        animator = new PetAnimator(assetSet);
        InitializeComponent();
        PreviewMouseRightButtonUp += (_, e) => { RadialRequested?.Invoke(); e.Handled = true; };
        ToolTipService.SetIsEnabled(this, false);
        Render(new Vector(), null, 0);
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { if (IsMouseCaptured) ReleaseMouseCapture(); CancelArmed(); } };
        SourceInitialized += (_, _) => Momonga.Platform.WindowPolicy.Passive(this, false);
    }

    public void Render(Vector movement, Point? cursor, double now, IdleActivity activity = IdleActivity.Calm, PetPose? lifePose = null)
    {
        elapsed = now;
        if (HasArmedInteraction && DateTime.UtcNow > armedUntil && !IsMouseCaptured) CancelArmed();
        var centeredCursor = cursor.HasValue
            ? new Point(cursor.Value.X - Width / 2 + 64, cursor.Value.Y - Height / 2 + 68)
            : (Point?)null;
        cheekDrag.Update(now);
        if (cheekDrag.Active(now))
        {
            cheekSurface.Source = ActionSprites.CheekFrame(cheekDrag.Expression(now)); cheekSurface.Drag = cheekDrag;
            cheekWindow ??= CreateCheekWindow();
            cheekWindow.Width = Width * 2; cheekWindow.Height = Height * 2;
            cheekWindow.Left = Left - Width / 2; cheekWindow.Top = Top - Height / 2;
            if (!cheekWindow.IsVisible) cheekWindow.Show();
            Momonga.Platform.WindowPolicy.Top(cheekWindow);
            Sprite.Visibility = Visibility.Hidden; cheekSurface.InvalidateVisual(); return;
        }
        cheekWindow?.Hide(); Sprite.Visibility = Visibility.Visible;
        Sprite.Chewing=foodFrame.HasValue&&now<foodUntil&&!drinkingBeer;
        Sprite.Sipping=foodFrame.HasValue&&now<foodUntil&&drinkingBeer;
        Sprite.MotionTime=now*.8/.7;
        Sprite.Source = foodFrame.HasValue && now < foodUntil ? drinkingBeer ? FoodSprites.Beer(4-(foodUntil-now)) : FoodSprites.Frame(foodFrame.Value + (int)(now/.35)%2)
            : actionFrame.HasValue && now < actionUntil ? ActionSprites.Frame(actionFrame.Value)
            : flickReaction && animator.IsReacting(now)
            ? InteractionSprites.Frame(now - flickAt < 0.18 ? 14 : 15)
            : lifePose == PetPose.Play ? ActionSprites.Frame(8 + (int)(now/.3)%2)
            : animator.Frame(movement, centeredCursor, IsDragging, now, activity, lifePose);
        if(Sprite.Chewing||Sprite.Sipping)Sprite.InvalidateVisual();
    }

    public void ArmStroke(bool chin) { CancelArmed(); strokeArmed = chin ? "Chin" : "Pet"; armedUntil = DateTime.UtcNow.AddSeconds(30); Mouse.OverrideCursor = Icons.HandCursor(chin ? 1 : 0); Momonga.Platform.ToolCursor.Show(Icons.HandCursorPath(chin ? 1 : 0)); }
    public void AnimateAction(string kind)
    {
        if(kind=="SnackBonk") {foodFrame=actionFrame=null;flickReaction=true;flickAt=elapsed;animator.React(PetPose.Startled,elapsed,.6);return;}
        actionFrame = kind switch { "Pet" => 1, "Chin" => 2, "Praise" => 3, "Soothe" => 7, "Poke" => 10, _ => null }; actionUntil = elapsed + 2;
    }

    private Window CreateCheekWindow()
    {
        var window = new Window { Owner = this, Content = cheekSurface, WindowStyle = WindowStyle.None, AllowsTransparency = true,
            Background = Brushes.Transparent, ShowActivated = false, ShowInTaskbar = false, Topmost = true, ResizeMode = ResizeMode.NoResize };
        window.SourceInitialized += (_, _) => Momonga.Platform.WindowPolicy.Passive(window, true);
        IsVisibleChanged += (_, _) => { if (!IsVisible) window.Hide(); };
        return window;
    }
    public void AnimateFood(Momonga.Inventory.ItemDefinition food) { foodFrame = food.AnimationFrame; drinkingBeer = CharacterSprites.Current == "kurimanju" && food.Id == "beer"; foodUntil = elapsed + (drinkingBeer ? 4 : 3); }
    private double flickAt;
    public void React(PetPose pose) { flickReaction = false; animator.React(pose, elapsed); }
    public void ArmFlick() { CancelArmed(); flickArmed = true; armedUntil = DateTime.UtcNow.AddSeconds(30); Mouse.OverrideCursor = Icons.FlickCursor; Momonga.Platform.ToolCursor.Show(Icons.FlickCursorPath); }
    public void ArmPoke() { CancelArmed(); pokeArmed = true; armedUntil = DateTime.UtcNow.AddSeconds(30); Mouse.OverrideCursor = Icons.PokeCursor; Momonga.Platform.ToolCursor.Show(Icons.PokeCursorPath); }
    public void ArmCheek() { CancelArmed(); cheekArmed = true; armedUntil = DateTime.UtcNow.AddSeconds(30); Mouse.OverrideCursor = Icons.HandCursor(2); Momonga.Platform.ToolCursor.Show(Icons.HandCursorPath(2)); }
    public void CancelArmed() { flickArmed = pokeArmed = cheekArmed = false; strokeArmed = ""; Mouse.OverrideCursor = null; Momonga.Platform.ToolCursor.Restore(); }
    public void RenderBall(BallGame ball, Vector movement, double now)
    {
        if (IsInteracting || IsReacting) return;
        Sprite.Source = ball.Phase == BallPhase.Hiding ? animator.Frame(new Vector(0,-1), null, false, 0)
            : ball.Running && movement.Length > .01 ? animator.Frame(movement, null, false, now * 1.8)
            : ActionSprites.Frame(8 + (int)(now / .22) % 2);
    }
    public void RenderPlayground(PlaygroundGame game, Vector movement, Point center, double now)
    {
        if(IsInteracting || IsReacting)return;
        if(game.Mode==PlaygroundMode.Bubbles) Sprite.Source=movement.Length>.01 ? animator.Frame(movement,null,false,now*1.8) : ActionSprites.Frame(8+(int)(now/.22)%2);
        else if(game.Deadpan) Sprite.Source=animator.FaceFront(PetPose.Annoyed,now);
        else if(game.WatchTarget is Point target && movement.Length<.01)
        {
            var direction=target-center;
            if(direction.Length>.01)Sprite.Source=animator.Frame(new Vector(),new Point(64,68)+direction*(150/direction.Length),false,now);
        }
    }
    public bool ApplyArmed()
    {
        if (!HasArmedInteraction) return false;
        if (strokeArmed != "" || cheekArmed) return false;
        var kind = flickArmed ? "Flick" : "Poke"; CancelArmed(); InteractionRequested?.Invoke(kind);
        if (kind == "Flick") { flickReaction = true; flickAt = elapsed; animator.React(PetPose.Startled, elapsed); }
        return true;
    }

    public void SetScale(double scale)
    {
        if (!double.IsFinite(scale) || IsDragging) return;
        scale = Math.Clamp(scale, 0.5, 2);
        var center = Left + Width / 2;
        var bottom = Top + Height;
        Width = 128 * scale;
        Height = 136 * scale;
        if (double.IsFinite(center)) Left = center - Width / 2;
        if (double.IsFinite(bottom)) Top = bottom - Height;
    }


    private void ResizeWithWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        SetScale(Width / 128 + Math.Sign(e.Delta) * 0.1);
        e.Handled = true;
    }

    public static void RunSizeChecks()
    {
        var pet = new PetWindow { Left = 200, Top = 200 };
        try
        {
            pet.SetScale(2);
            if (pet.Width != 256 || pet.Height != 272 || pet.Left + pet.Width / 2 != 264 || pet.Top + pet.Height != 336)
                throw new InvalidOperationException("Resize lost proportions or foot anchor");
            pet.SetScale(10);
            if (pet.Width != 256) throw new InvalidOperationException("Maximum size was ignored");
            pet.SetScale(-10);
            if (pet.Width != 64 || pet.Height != 68) throw new InvalidOperationException("Minimum size was ignored");
            pet.SetScale(double.NaN);
            if (pet.Width != 64) throw new InvalidOperationException("Invalid size was accepted");
            pet.SetScale(1);
            pet.Render(new Vector(), new Point(130, 68), 1);
            pet.Render(new Vector(), new Point(130, 68), 1.2);
            var rightFacing = pet.Sprite.Source;
            pet.SetScale(2);
            pet.Render(new Vector(), new Point(194, 136), 2);
            if (pet.Sprite.Source != rightFacing)
                throw new InvalidOperationException("Resized pet tracked cursor from wrong center");
        }
        finally { pet.Close(); }
    }

    private void PreviewPose(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem item && Enum.TryParse<PetPose>(item.Tag?.ToString(), out var pose))
            animator.React(pose, elapsed);
    }

    private void BeginDrag(object sender, MouseButtonEventArgs e)
    {
        if (flickArmed || pokeArmed) { ApplyArmed(); e.Handled = true; return; }
        var local = e.GetPosition(this);
        StartGesture(local, PointToScreen(local)); e.Handled = true;
    }
    public bool BeginSceneGesture(Point normalized, Point screen)
    {
        var local = new Point(normalized.X*Width,normalized.Y*Height);
        var hit = PetInteractionDetector.Region(local,Width,Height,CharacterLayers.Pose(Sprite.Source)?.Facing??-1);
        if (!HasArmedInteraction && (!DirectTouch() || hit is not (HitRegion.Head or HitRegion.Chin or HitRegion.LeftCheek or HitRegion.RightCheek))) return false;
        ShowComposite(false); StartGesture(local,screen); return true;
    }
    private void StartGesture(Point local, Point screen)
    {
        region = PetInteractionDetector.Region(local,Width,Height,CharacterLayers.Pose(Sprite.Source)?.Facing??-1);
        var pullArmed = cheekArmed;
        if (cheekArmed) { if (region is not (HitRegion.LeftCheek or HitRegion.RightCheek)) return; cheekArmed = false; }
        pressPoint = lastPointer = screen;
        stroking = (DirectTouch() || strokeArmed != "") && region is HitRegion.Head or HitRegion.Chin;
        if (strokeArmed != "" && (strokeArmed == "Pet" ? region != HitRegion.Head : region != HitRegion.Chin)) return;
        if (stroking) { strokeArmed = region == HitRegion.Chin ? "Chin" : "Pet"; Mouse.OverrideCursor = Icons.HandCursor(region == HitRegion.Chin ? 1 : 0); }
        cheek = !stroking && (DirectTouch() || pullArmed) && region is HitRegion.LeftCheek or HitRegion.RightCheek;
        if (cheek) { cheekDrag.Begin(new Point(local.X / Width, local.Y / Height), region == HitRegion.LeftCheek); Mouse.OverrideCursor = Icons.HandCursor(2); }
        moved = false; CaptureMouse();
    }

    private void ContinueDrag(object sender, MouseEventArgs e)
    {
        if (!IsMouseCaptured) return;
        var current = PointToScreen(e.GetPosition(this));
        ContinueGesture(current);
    }
    internal void ContinueGesture(Point current)
    {
        var source = PresentationSource.FromVisual(this);
        var delta = source?.CompositionTarget?.TransformFromDevice.Transform(current - lastPointer)
                    ?? current - lastPointer;
        moved |= (current - pressPoint).Length > 5;
        if (stroking)
        {
            if (delta.Length > .5 && elapsed - lastStroke > 0.6)
            { InteractionRequested?.Invoke(region == HitRegion.Chin ? "Chin" : "Pet"); lastStroke = elapsed; }
        }
        else if (cheek)
        {
            var offset = source?.CompositionTarget?.TransformFromDevice.Transform(current - pressPoint) ?? current - pressPoint;
            cheekDrag.Move(new Vector(offset.X / Width, offset.Y / Height));
            Mouse.OverrideCursor = Icons.HandCursor(2);
        }
        else if (moved)
        { IsDragging = true; Left += delta.X; Top += delta.Y; }
        lastPointer = current;
    }

    private void EndDrag(object sender, MouseButtonEventArgs e) => ReleaseMouseCapture();
    private void LostDrag(object sender, MouseEventArgs e)
    {
        if (cheek) cheekDrag.Release(elapsed);
        Sprite.RenderTransform = Transform.Identity;
        CancelArmed();
        if (!IsDragging)
        {
            if (cheek && moved) InteractionRequested?.Invoke("CheekPull");
            else if (!moved && !stroking) InteractionRequested?.Invoke(DirectTouch() ? region is HitRegion.Head or HitRegion.Body ? "Pet" : "Poke" : "Talk");
            return;
        }
        IsDragging = false;
        DragFinished?.Invoke();
    }
    private void Recall(object sender, RoutedEventArgs e) => RecallRequested?.Invoke();
    private void HidePet(object sender, RoutedEventArgs e) => HideRequested?.Invoke();
    private void ExitApp(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
