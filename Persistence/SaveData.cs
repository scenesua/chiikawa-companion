using System;
using System.Collections.Generic;
using Momonga.Simulation;

namespace Momonga.Persistence;

public sealed class SaveData
{
    public int Version { get; set; } = 3;
    public string CharacterId { get; set; } = "momonga";
    public PetState Pet { get; set; } = new();
    public int ActivityPoints { get; set; } = 15000;
    public Dictionary<string, int> Inventory { get; set; } = new()
    { ["meal"] = 2, ["cookie"] = 1, ["food-bowl"] = 1, ["water-bowl"] = 1, ["cushion"] = 1 };
    public List<HabitatItem> Items { get; set; } = new();
    public Dictionary<string,double> LeftoverFood { get; set; } = new();
    public HabitatZone Zone { get; set; } = new();
    public List<string> DialogueHistory { get; set; } = new();
    public List<InteractionRecord> InteractionHistory { get; set; } = new();
    public Dictionary<string, double> Cooldowns { get; set; } = new();
    public double SimulationSeconds { get; set; }
    public double LastSnackTime { get; set; } = -1000;
    public double QuietSeconds { get; set; }
    public double PetX { get; set; } = 600;
    public double PetY { get; set; } = 600;
    public string PetMonitorId { get; set; } = "";
    public Settings Settings { get; set; } = new();
    public DateTimeOffset LastSaveTimestamp { get; set; } = DateTimeOffset.UtcNow;
    public int HourPoints { get; set; }
    public DateTimeOffset HourStarted { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Settings
{
    public bool DirectTouch { get; set; }
    public double PetScale { get; set; } = 1;
    public bool FullscreenCourtesy { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public int PointsPerInput { get; set; } = 1;
    public double SpeechFontSize { get; set; } = 13;
    public double BubbleScale { get; set; } = .85;
}

public sealed class HabitatZone
{
    public string Id { get; set; } = "home";
    public string MonitorId { get; set; } = "";
    public double X { get; set; } = 100;
    public double Y { get; set; } = 600;
    public double Width { get; set; } = 420;
    public double Height { get; set; } = 300;
}

public sealed class HabitatItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ItemId { get; set; } = "";
    public string ZoneId { get; set; } = "home";
    public string MonitorId { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Scale { get; set; } = 1;
    public double Rotation { get; set; }
    public bool Locked { get; set; }
    public bool Active { get; set; }
    public string FoodId { get; set; } = "meal";
    public int FoodQuantity { get; set; }
    public double FoodPortion { get; set; } = 1;
    public double Water { get; set; } = 100;
}

public sealed record InteractionRecord(string Kind, double Time);
