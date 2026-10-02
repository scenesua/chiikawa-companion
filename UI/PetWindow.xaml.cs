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
    private double cheekPull;
    private double cheekReleasedAt, cheekReleasedPull;
    public int CheekExpression => cheekPull < Width*.08 ? 4 : cheekPull < Width*.18 ? 5 : 6;
    private int? actionFrame;
    private double actionUntil;
    private int? foodFrame;
    private double foodUntil;
    private DateTime armedUntil;
    private double lastStroke;
    private bool externalDrag;
    public bool IsDragging { get; private set; }
    public bool IsReacting => animator.IsReacting(elapsed);
    public bool IsInteracting => IsMouseCaptured || externalDrag;
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
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) CancelArmed(); };
        SourceInitialized += (_, _) => Momonga.Platform.WindowPolicy.Passive(this, false);
    }

    public void Render(Vector movement, Point? cursor, double now, IdleActivity activity = IdleActivity.Calm, PetPose? lifePose = null)
    {
        elapsed = now;
        if (HasArmedInteraction && DateTime.UtcNow > armedUntil && !IsMouseCaptured) CancelArmed();
        var centeredCursor = cursor.HasValue
            ? new Point(cursor.Value.X - Width / 2 + 64, cursor.Value.Y - Height / 2 + 68)
            : (Point?)null;
        var releasedPull = cheekReleasedPull * Math.Exp(-Math.Max(0, now - cheekReleasedAt) * 18);
        if (cheek && IsMouseCaptured || releasedPull > .1)
        {
            var pull = IsMouseCaptured ? cheekPull : releasedPull;
            CheekMesh.Source = ActionSprites.Frame(pull < Width*.08 ? 4 : pull < Width*.18 ? 5 : 6);
            CheekMesh.LeftCheek = region == HitRegion.LeftCheek; CheekMesh.Pull = pull;
            CheekMesh.Visibility = Visibility.Visible; Sprite.Visibility = Visibility.Hidden; CheekMesh.InvalidateVisual(); return;
        }
        CheekMesh.Visibility = Visibility.Collapsed; Sprite.Visibility = Visibility.Visible;
        Sprite.Source = foodFrame.HasValue && now < foodUntil ? FoodSprites.Frame(foodFrame.Value + (int)(now/.35)%2)
            : actionFrame.HasValue && now < actionUntil ? ActionSprites.Frame(actionFrame.Value)
            : lifePose == PetPose.Play ? ActionSprites.Frame(8 + (int)(now/.3)%2)
            : flickReaction && animator.IsReacting(now)
            ? InteractionSprites.Frame(now - flickAt < 0.18 ? 14 : 15)
            : animator.Frame(movement, centeredCursor, IsDragging, now, activity, lifePose);
    }

    public void ArmStroke(bool chin) { CancelArmed(); strokeArmed = chin ? "Chin" : "Pet"; armedUntil = DateTime.UtcNow.AddSeconds(30); Mouse.OverrideCursor = Icons.HandCursor(chin ? 1 : 0); Momonga.Platform.ToolCursor.Show(Icons.HandCursorPath(chin ? 1 : 0)); }
    public void AnimateAction(string kind) { actionFrame = kind switch { "Pet" => 1, "Chin" => 2, "Praise" => 3, "Soothe" => 7, "Poke" => 10, _ => null }; actionUntil = elapsed + 2; }
    public void AnimateFood(Momonga.Inventory.ItemDefinition food) { foodFrame = food.AnimationFrame; foodUntil = elapsed + 3; }
    private double flickAt;
    public void React(PetPose pose) { flickReaction = false; animator.React(pose, elapsed); }
    public void ArmFlick() { CancelArmed(); flickArmed = true; armedUntil = DateTime.UtcNow.AddSeconds(30); Mouse.OverrideCursor = Icons.FlickCursor; Momonga.Platform.ToolCursor.Show(Icons.FlickCursorPath); }
    public void ArmPoke() { CancelArmed(); pokeArmed = true; armedUntil = DateTime.UtcNow.AddSeconds(30); Mouse.OverrideCursor = Icons.PokeCursor; Momonga.Platform.ToolCursor.Show(Icons.PokeCursorPath); }
    public void ArmCheek() { CancelArmed(); cheekArmed = true; armedUntil = DateTime.UtcNow.AddSeconds(30); Mouse.OverrideCursor = Icons.HandCursor(2); Momonga.Platform.ToolCursor.Show(Icons.HandCursorPath(2)); }
    public void CancelArmed() { flickArmed = pokeArmed = cheekArmed = false; strokeArmed = ""; Mouse.OverrideCursor = null; Momonga.Platform.ToolCursor.Restore(); }
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
        var hit = PetInteractionDetector.Region(local,Width,Height);
        if (!HasArmedInteraction && (!DirectTouch() || hit is not (HitRegion.Head or HitRegion.Chin or HitRegion.LeftCheek or HitRegion.RightCheek))) return false;
        ShowComposite(false); StartGesture(local,screen); return true;
    }
    private void StartGesture(Point local, Point screen)
    {
        region = PetInteractionDetector.Region(local, Width, Height);
        var pullArmed = cheekArmed;
        if (cheekArmed) { if (region is not (HitRegion.LeftCheek or HitRegion.RightCheek)) return; cheekArmed = false; }
        pressPoint = lastPointer = screen;
        stroking = (DirectTouch() || strokeArmed != "") && region is HitRegion.Head or HitRegion.Chin;
        if (strokeArmed != "" && (strokeArmed == "Pet" ? region != HitRegion.Head : region != HitRegion.Chin)) return;
        if (stroking) { strokeArmed = region == HitRegion.Chin ? "Chin" : "Pet"; Mouse.OverrideCursor = Icons.HandCursor(region == HitRegion.Chin ? 1 : 0); }
        cheekPull = 0;
        cheekReleasedPull = 0;
        cheek = !stroking && (DirectTouch() || pullArmed) && region is HitRegion.LeftCheek or HitRegion.RightCheek;
        if (cheek) Mouse.OverrideCursor = Icons.HandCursor(2);
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
            var outward = region == HitRegion.LeftCheek ? -offset.X : offset.X;
            cheekPull = Math.Clamp(outward, 0, Width * .25);
            Mouse.OverrideCursor = Icons.HandCursor(2);
        }
        else if (moved)
        { IsDragging = true; Left += delta.X; Top += delta.Y; }
        lastPointer = current;
    }

    private void EndDrag(object sender, MouseButtonEventArgs e) => ReleaseMouseCapture();
    private void LostDrag(object sender, MouseEventArgs e)
    {
        if (cheek) { cheekReleasedPull = cheekPull; cheekReleasedAt = elapsed; }
        Sprite.RenderTransform = Transform.Identity;
        CheekMesh.Visibility = Visibility.Collapsed; Sprite.Visibility = Visibility.Visible; CancelArmed();
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
