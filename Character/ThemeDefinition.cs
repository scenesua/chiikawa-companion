namespace Momonga.Character;

public sealed record ThemeDefinition
{
    public string Name { get; init; } = "Cloud Lavender";
    public string Background { get; init; } = "#FAF8FF";
    public string Surface { get; init; } = "#FFFFFF";
    public string Ink { get; init; } = "#39334C";
    public string Muted { get; init; } = "#756D89";
    public string Accent { get; init; } = "#8A78B4";
    public string Soft { get; init; } = "#EEE7FA";
    public string Border { get; init; } = "#D9D0EA";
    public string Healthy { get; init; } = "#66AFA3";
    public string Warning { get; init; } = "#C89445";
    public string Critical { get; init; } = "#CE738B";
    public double CornerRadius { get; init; } = 18;
}

