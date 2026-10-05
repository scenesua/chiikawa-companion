using System;

namespace Momonga.Character;

public readonly record struct PartMotion(double X,double Y,double ScaleX,double ScaleY)
{
    public static PartMotion Still => new(0,0,1,1);
    public static PartMotion Sip(CharacterPart part,double seconds)
    {
        var phase=seconds%.8;
        var swallow=phase<.4?0:Math.Pow(Math.Sin((phase-.4)*Math.PI/.4),2);
        return part==CharacterPart.Mouth?new(0,.003*swallow,1,1-.08*swallow):Still;
    }
    public static PartMotion Chew(CharacterPart part,double seconds)
    {
        var phase=seconds%.8;
        var cycle=phase<.4?0:Math.Pow(Math.Sin((phase-.4)*Math.PI*2/.4),2);
        return part switch
        {
            CharacterPart.Mouth => new(0,.006*cycle,1,1+.14*cycle),
            CharacterPart.LeftCheek => new(-.003*cycle,0,1+.035*cycle,1),
            CharacterPart.RightCheek => new(.003*cycle,0,1+.035*cycle,1),
            _ => Still
        };
    }
}
