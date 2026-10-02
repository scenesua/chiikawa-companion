using System;
using Momonga.Persistence;

namespace Momonga.Economy;

public sealed class ActivityPointService(SaveData data)
{
    public string ActivityState { get; private set; } = "Idle";
    private double continuousActive;
    private DateTimeOffset burstStarted;
    private int burstCount;

    public void Update(double seconds, bool active, DateTimeOffset wallTime, int keys = 0, int clicks = 0)
    {
        seconds = Math.Clamp(seconds, 0, 1);
        if (wallTime - data.HourStarted >= TimeSpan.FromHours(1) || wallTime < data.HourStarted)
        { data.HourStarted = wallTime; data.HourPoints = 0; }
        continuousActive = active ? continuousActive + seconds : 0;
        ActivityState = !active ? "Idle" : continuousActive >= 3600 ? "LongActiveSession" : "Active";
        keys = Math.Clamp(keys, 0, 10000); clicks = Math.Clamp(clicks, 0, 10000);
        if (wallTime - burstStarted >= TimeSpan.FromSeconds(1) || wallTime < burstStarted) { burstStarted = wallTime; burstCount = 0; }
        // ponytail: generic 30 events/sec ceiling avoids reading individual key identities.
        var count = Math.Min(keys + clicks, Math.Max(0, 30 - burstCount)); burstCount += count;
        var points = count * data.Settings.PointsPerInput;
        points = Math.Min(points, 1_000_000 - data.ActivityPoints);
        data.ActivityPoints += points; data.HourPoints += points;
    }
}
