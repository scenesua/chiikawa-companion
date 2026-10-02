using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Momonga.Persistence;

namespace Momonga.Content;

public sealed class DialogueService
{
    private readonly Dictionary<string, string[]> lines;
    private readonly SaveData data;
    private readonly Random random = new();
    public DialogueService(SaveData data, string dialogueSetId = "momonga")
    {
        this.data = data;
        using var stream = Application.GetResourceStream(new Uri($"pack://application:,,,/Content/{dialogueSetId}-dialogue.json")).Stream;
        lines = JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream) ?? throw new InvalidOperationException("Missing dialogue");
    }
    public string Pick(string tag)
    {
        if (!lines.TryGetValue(tag, out var choices)) lines.TryGetValue(tag.Split(':')[0], out choices);
        if (choices == null || choices.Length == 0) return "…";
        var recent = data.DialogueHistory.TakeLast(3).ToArray();
        var candidates = choices.Where(c => !recent.Contains(c)).ToArray();
        if (candidates.Length == 0) candidates = choices.Where(c => c != data.DialogueHistory.LastOrDefault()).ToArray();
        var line = candidates.Length > 0 ? candidates[random.Next(candidates.Length)] : choices[0];
        data.DialogueHistory.Add(line); if (data.DialogueHistory.Count > 20) data.DialogueHistory.RemoveAt(0); return line;
    }
}
