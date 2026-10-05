#if CROSS_PLATFORM
using Avalonia;
#else
using System.Windows;
#endif

namespace Momonga.Input;

public enum HitRegion { Head, Face, LeftCheek, RightCheek, Body, Tail, Chin, Outside }

public static class PetInteractionDetector
{
    public static HitRegion Region(Point point, double width, double height,int facing=-1)
    {
        if(width<=0||height<=0)return HitRegion.Outside;
        return CharacterAnatomy.Current.Region(new Point(point.X/width,point.Y/height),facing is not (5 or 6 or 7));
    }
}
