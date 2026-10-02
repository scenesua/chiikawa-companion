using System.Windows;

namespace Momonga.Input;

public enum HitRegion { Head, Face, LeftCheek, RightCheek, Body, Tail, Chin }

public static class PetInteractionDetector
{
    public static HitRegion Region(Point point, double width, double height)
    {
        var x = point.X / width; var y = point.Y / height;
        if (Animation.CharacterSprites.Current != "momonga")
        {
            var face = Animation.CharacterSprites.FaceY;
            if (y < face - .10) return HitRegion.Head;
            if (y < face + .10 && x is > .16 and < .40) return HitRegion.LeftCheek;
            if (y < face + .10 && x is > .60 and < .84) return HitRegion.RightCheek;
            if (y < face + .21 && x is > .30 and < .70) return HitRegion.Chin;
            return y < face + .12 ? HitRegion.Face : HitRegion.Body;
        }
        if (x > .82 && y > .4) return HitRegion.Tail;
        if (y < .40) return HitRegion.Head;
        if (y < .65 && x is > .12 and < .38) return HitRegion.LeftCheek;
        if (y < .65 && x is > .55 and < .75) return HitRegion.RightCheek;
        if (y is >= .61 and < .80 && x is > .26 and < .72) return HitRegion.Chin;
        return y < 0.7 ? HitRegion.Face : HitRegion.Body;
    }
}
