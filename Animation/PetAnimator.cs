using System;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Momonga.Simulation;

namespace Momonga.Animation;

public enum PetPose { Happy, Eat, Sleep, Annoyed, Sulk, Startled, Dragged, Play }

public sealed class PetAnimator
{
    private static readonly System.Collections.Generic.Dictionary<string,int[][][]> exclusions = LoadExclusions();
    private static System.Collections.Generic.Dictionary<string,int[][][]> LoadExclusions()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/sprite-exclusions.json")).Stream;
        return JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string,int[][][]>>(stream)!;
    }
    private readonly BitmapSource[] frames;
    private readonly BitmapSource[] idleFrames;
    private int facing = 2;
    private PetPose? pose;
    private double poseUntil;
    private double nextTurn;
    public bool IsReacting(double now) => pose.HasValue && now < poseUntil;

    public PetAnimator(string assetSet = "momonga")
    {
        frames = assetSet == "momonga" ? LoadFrames(assetSet + "-sprites", assetSet + "-frames", 32) : CharacterSprites.Frames(assetSet, 0, 32);
        for (var i = 0; i < (assetSet == "momonga" ? frames.Length : 0); i++)
        {
            var source = frames[i]; var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen()) drawing.DrawImage(source, new Rect((240 - source.Width) / 2, 235 - source.Height, source.Width, source.Height));
            var padded = new RenderTargetBitmap(240, 240, 96, 96, PixelFormats.Pbgra32); padded.Render(visual); padded.Freeze(); frames[i] = padded;
        }
        for (var row = 0; row < (assetSet == "momonga" ? 3 : 0); row++)
        foreach (var direction in new[] { 1, 7 })
        {
            var index = row * 8 + direction;
            var mirrored = new TransformedBitmap(frames[index], new ScaleTransform(-1, 1));
            mirrored.Freeze(); frames[index] = mirrored;
        }
        if(assetSet=="momonga")for(var i=0;i<frames.Length;i++)UI.CharacterLayers.Register(frames[i],assetSet,"movement",i,i<24?i%8:2);
        idleFrames = assetSet == "momonga" ? LoadFrames(assetSet + "-idle", assetSet + "-idle-frames", 12) : CharacterSprites.Frames(assetSet, 32, 12);
    }

    internal static BitmapSource[] LoadFrames(string imageName, string frameName, int count)
    {
        var atlas = new BitmapImage(new Uri($"pack://application:,,,/Assets/{imageName}.png"));
        using var metadata = Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/{frameName}.json")).Stream;
        var rectangles = JsonSerializer.Deserialize<int[][]>(metadata);
        if (rectangles?.Length != count)
            throw new InvalidOperationException($"Sprite metadata must contain {count} frames");
        var result = new BitmapSource[count];
        for (var i = 0; i < count; i++)
        {
            var rect = rectangles[i];
            if (rect.Length != 4 || rect[0] < 0 || rect[1] < 0 || rect[2] <= 0 || rect[3] <= 0 ||
                rect[0] + rect[2] > atlas.PixelWidth || rect[1] + rect[3] > atlas.PixelHeight)
                throw new InvalidOperationException("Sprite rectangle is outside the atlas");
            BitmapSource frame = new CroppedBitmap(atlas, new Int32Rect(rect[0], rect[1], rect[2], rect[3]));
            if (exclusions.TryGetValue(frameName, out var masks) && masks[i].Length > 0)
            {
                var clip = new GeometryGroup { FillRule = FillRule.EvenOdd };
                clip.Children.Add(new RectangleGeometry(new Rect(0,0,rect[2],rect[3])));
                foreach (var cut in masks[i]) clip.Children.Add(new RectangleGeometry(new Rect(cut[0],cut[1],cut[2],cut[3])));
                var visual = new DrawingVisual(); RenderOptions.SetEdgeMode(visual,EdgeMode.Aliased);
                using (var dc = visual.RenderOpen()) { dc.PushClip(clip); dc.DrawImage(frame,new Rect(0,0,rect[2],rect[3])); }
                var cleaned = new RenderTargetBitmap(rect[2],rect[3],96,96,PixelFormats.Pbgra32); cleaned.Render(visual); frame=cleaned;
            }
            frame.Freeze(); result[i] = frame;
        }
        return result;
    }

    public void React(PetPose action, double now, double seconds = 3) { pose = action; poseUntil = now + seconds; }
    public BitmapSource FaceFront(PetPose expression, double now)
    {
        Frame(new Vector(),new Point(64,218),false,now);
        return facing==2 ? frames[24+(int)expression] : frames[facing];
    }

    public BitmapSource Frame(Vector movement, Point? cursor, bool dragging, double now,
        IdleActivity activity = IdleActivity.Calm, PetPose? lifePose = null)
    {
        if (dragging) return frames[24 + (int)PetPose.Dragged];
        if (IsReacting(now)) return frames[24 + (int)pose!.Value];
        pose = null;
        if (movement.Length > 0.01)
        {
            facing = Direction(movement);
            return frames[(1 + (int)(now / 0.18) % 2) * 8 + facing];
        }
        if (lifePose.HasValue) return frames[24 + (int)lifePose.Value];
        switch (activity)
        {
            case IdleActivity.Doze: return idleFrames[2 + (int)(now / 2) % 2];
            case IdleActivity.Groom: return idleFrames[4 + (int)(now / 0.4) % 2];
            case IdleActivity.Stretch: return idleFrames[6 + (int)(now / 1.2) % 2];
            case IdleActivity.TailHug: return idleFrames[8 + (int)(now / 2) % 4];
            case IdleActivity.Play: return frames[(int)(now / 0.8) % 2 == 0 ? 31 : 24];
        }
        var look = cursor.HasValue ? cursor.Value - new Point(64, 68) : new Vector();
        if (look.Length is <= 20 or >= 350)
        {
            facing = 2;
            return idleFrames[now % 5 < 0.15 ? 1 : 0];
        }
        var desired = Direction(look);
        var difference = (desired - facing + 12) % 8 - 4;
        // Adjacent views cover the turn; a small angular dead zone avoids boundary jitter.
        var angle = Math.Atan2(look.Y, look.X) - facing * Math.PI / 4;
        angle = Math.Atan2(Math.Sin(angle), Math.Cos(angle));
        var atBoundary = Math.Abs(difference) == 1 && Math.Abs(angle) < Math.PI * 35 / 180;
        if (difference != 0 && !atBoundary && now >= nextTurn)
        {
            facing = (facing + Math.Sign(difference) + 8) % 8;
            nextTurn = now + 0.12;
        }
        return frames[facing];
    }

    // Clockwise screen directions: E, SE, S, SW, W, NW, N, NE.
    public static int Direction(Vector movement) =>
        ((int)Math.Floor(Math.Atan2(movement.Y, movement.X) / (Math.PI / 4) + 0.5) + 8) % 8;

    public static void RunChecks()
    {
        foreach (var character in Character.CharacterDefinition.All)
        {
            var data = new Persistence.SaveData { CharacterId = character.CharacterId };
            var dialogue = new Content.DialogueService(data, character.DialogueSetId);
            if (string.IsNullOrWhiteSpace(dialogue.Pick("Greeting"))) throw new InvalidOperationException("Missing character greeting");
            if (character.CharacterId is "kani" or "anoko" or "rilakkuma" or "korilakkuma" or "chiikabu")
                foreach (var tag in new[] { "Hungry", "Happy", "IdleTalk", "PetReaction" })
                    if (dialogue.Pick(tag) != "…") throw new InvalidOperationException("Silent character speaks sentences");
            CharacterSprites.Select(character.AssetSet);
            foreach (var food in new[] { "white-rice","furikake-rice","curry-rice","jiro-ramen","nuts","fruit" })
            {
                var a=InteractionSprites.MealFrame(food,0); var b=InteractionSprites.MealFrame(food,1);
                if (a.PixelWidth != 240 || b.PixelHeight != 240 || !a.IsFrozen || ReferenceEquals(a,b)) throw new InvalidOperationException("Missing dedicated meal animation");
            }
            if (ReferenceEquals(BowlContents.Frame("white-rice"),BowlContents.Frame("white-rice",eating:true))) throw new InvalidOperationException("Eating bowl retains resting utensil");
            if (character.AssetSet == "momonga") continue;
            var drink = (BitmapSource)InteractionSprites.ActorFrame(6);
            if (drink.PixelWidth != 240 || !drink.IsFrozen || ReferenceEquals(drink, CharacterSprites.Frame(62)) || ReferenceEquals(drink, InteractionSprites.ActorFrame(7)) || !double.IsFinite(InteractionSprites.ActorScale(6)))
                throw new InvalidOperationException("Missing dedicated water animation");
            foreach (var f in CharacterSprites.Frames(character.AssetSet, 0, 80))
                if (f.PixelWidth != 240 || f.PixelHeight != 240 || !f.IsFrozen) throw new InvalidOperationException("Inconsistent character frame canvas");
            var actor = InteractionSprites.ActorFrame(4);
            if (actor != CharacterSprites.Frame(60) || character.AssetSet is not ("mymelody" or "kuromi") && FoodSprites.Frame(0) != CharacterSprites.Frame(64))
                throw new InvalidOperationException("Interaction uses another character's artwork");
            if (CharacterSprites.BodyWidth <= 0 || !double.IsFinite(InteractionSprites.ActorScale(0))) throw new InvalidOperationException("Invalid actor scale");
        }
        CharacterSprites.Select("momonga");
        var vectors = new[] { new Vector(1, 0), new Vector(1, 1), new Vector(0, 1), new Vector(-1, 1),
            new Vector(-1, 0), new Vector(-1, -1), new Vector(0, -1), new Vector(1, -1) };
        for (var i = 0; i < vectors.Length; i++)
            if (Direction(vectors[i]) != i) throw new InvalidOperationException("Wrong sprite direction");
        foreach (var source in LoadFrames("momonga-sprites", "momonga-frames", 32))
        {
            var image = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            var stride = image.PixelWidth * 4; var pixels = new byte[stride * image.PixelHeight]; image.CopyPixels(pixels, stride, 0);
            for (var y = 0; y < image.PixelHeight; y++)
            for (var x = 0; x < image.PixelWidth; x++)
                if ((x == 0 || y == 0 || x == image.PixelWidth - 1 || y == image.PixelHeight - 1) && pixels[y * stride + x * 4 + 3] > 100)
                    throw new InvalidOperationException("Sprite crop cuts through opaque artwork");
        }
        var animator = new PetAnimator();
        foreach (var frame in System.Linq.Enumerable.Concat(System.Linq.Enumerable.Concat(animator.frames, animator.idleFrames), System.Linq.Enumerable.Select(System.Linq.Enumerable.Range(0, 16), InteractionSprites.Frame)))
        {
            var rgba = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[rgba.PixelWidth * rgba.PixelHeight * 4];
            rgba.CopyPixels(pixels, rgba.PixelWidth * 4, 0);
            var transparent = false;
            var visible = false;
            for (var i = 3; i < pixels.Length; i += 4)
            {
                transparent |= pixels[i] == 0;
                visible |= pixels[i] > 0;
            }
            if (!transparent || !visible) throw new InvalidOperationException("Missing sprite or alpha transparency");
        }
        var idle = animator.Frame(new Vector(), null, false, 0);
        var stepA = animator.Frame(vectors[2], null, false, 0);
        var stepB = animator.Frame(vectors[2], null, false, 0.18);
        if (ReferenceEquals(idle, stepA) || ReferenceEquals(stepA, stepB))
            throw new InvalidOperationException("Walking frames did not advance");
        foreach (PetPose action in Enum.GetValues<PetPose>())
        {
            animator.React(action, 1);
            if (animator.Frame(new Vector(), null, false, 1) != animator.frames[24 + (int)action])
                throw new InvalidOperationException("Wrong reaction sprite");
        }
        if (animator.IsReacting(4)) throw new InvalidOperationException("Reaction never ended");
        if (animator.Frame(new Vector(), null, true, 5) != animator.frames[30])
            throw new InvalidOperationException("Wrong drag sprite");
        var observer = new PetAnimator();
        var nearbyCursor = new Point(130, 68);
        if (observer.Frame(new Vector(), new Point(500, 68), false, 0.5) != observer.idleFrames[0])
            throw new InvalidOperationException("Pet watched a distant cursor");
        if (observer.Frame(new Vector(), nearbyCursor, false, 1) != observer.frames[1] ||
            observer.Frame(new Vector(), nearbyCursor, false, 1.06) != observer.frames[1] ||
            observer.Frame(new Vector(), nearbyCursor, false, 1.13) != observer.frames[0])
            throw new InvalidOperationException("Cursor turn skipped intermediate view or delay");
        var boundaryCursor = new Point(64 + 80 * Math.Cos(Math.PI / 6), 68 + 80 * Math.Sin(Math.PI / 6));
        if (observer.Frame(new Vector(), boundaryCursor, false, 2) != observer.frames[0])
            throw new InvalidOperationException("Cursor turn jittered near direction boundary");
        observer.Frame(new Vector(1, -1), null, false, 3);
        if (observer.Frame(new Vector(), nearbyCursor, false, 4) != observer.frames[0])
            throw new InvalidOperationException("Cursor turn did not take shortest wrapped route");
        if (observer.Frame(new Vector(), null, false, 5) != observer.idleFrames[1])
            throw new InvalidOperationException("Idle blink frame missing");
        if (observer.Frame(new Vector(), nearbyCursor, false, 6, IdleActivity.Groom) != observer.idleFrames[5] ||
            observer.Frame(new Vector(), nearbyCursor, false, 6.4, IdleActivity.Groom) != observer.idleFrames[4])
            throw new InvalidOperationException("Grooming loop did not advance or cursor interrupted idle activity");
        observer.React(PetPose.Happy, 7);
        if (observer.Frame(new Vector(), null, false, 7, IdleActivity.Doze) != observer.frames[24])
            throw new InvalidOperationException("Idle action interrupted explicit reaction");
    }
}
