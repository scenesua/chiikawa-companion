using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Momonga.Inventory;

namespace Momonga.Persistence;

public sealed class SaveService
{
    public string Path { get; }
    public string Status { get; private set; } = "저장 대기";
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool canWrite = true;
    private static readonly JsonSerializerOptions options = new() { WriteIndented = true };
    public SaveService(string? path = null) => Path = path ?? System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MomongaDesktopCompanion", "save.json");

    public SaveData Load(IReadOnlyDictionary<string, ItemDefinition> catalog)
    {
        if (!File.Exists(Path)) return new SaveData();
        try
        {
            var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(Path)) ?? throw new JsonException("Empty save");
            Validate(data, catalog);
            data.Pet.Offline(DateTimeOffset.UtcNow - data.LastSaveTimestamp);
            Status = "저장 불러옴"; return data;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            var backup = Path + ".corrupt-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff") + ".json";
            try { File.Copy(Path, backup, false); Status = "손상 저장 백업 후 기본값 복구"; }
            catch (Exception copyError) when (copyError is IOException or UnauthorizedAccessException) { canWrite = false; Status = "기존 파일 보호를 위해 저장 중단 · 백업 실패: " + copyError.Message; }
            return new SaveData();
        }
    }

    public static void Validate(SaveData data, IReadOnlyDictionary<string, ItemDefinition> catalog)
    {
        if (data.Version is not (1 or 2 or 3) || data.Pet == null || data.Settings == null || data.Zone == null || data.Inventory == null ||
            data.Items == null || data.LeftoverFood == null || data.Cooldowns == null || data.DialogueHistory == null || data.InteractionHistory == null)
            throw new JsonException("Invalid save structure");
        if (data.ActivityPoints < 0 || data.ActivityPoints > 1_000_000 || data.Items.Count > 30 || data.Inventory.Count > 200 ||
            data.Inventory.Any(p => !catalog.ContainsKey(p.Key) || p.Value is < 0 or > 9999) || data.Cooldowns.Count > 100 ||
            data.Cooldowns.Values.Any(v => !double.IsFinite(v))) throw new JsonException("Invalid inventory or economy");
        if (!double.IsFinite(data.SimulationSeconds) || data.SimulationSeconds < 0 || !double.IsFinite(data.PetX) || !double.IsFinite(data.PetY) ||
            !double.IsFinite(data.QuietSeconds) || data.QuietSeconds < 0 || !double.IsFinite(data.LastSnackTime)) throw new JsonException("Invalid clock or position");
        if (data.LeftoverFood.Count > 200 || data.LeftoverFood.Any(p => !catalog.TryGetValue(p.Key,out var item) || item.Category != "Food" || !double.IsFinite(p.Value) || p.Value <= 0 || p.Value >= 1 || data.Inventory.GetValueOrDefault(p.Key) >= 9999)) throw new JsonException("Invalid leftover food");
        var zone = data.Zone;
        if (!double.IsFinite(zone.X) || !double.IsFinite(zone.Y) || !double.IsFinite(zone.Width) || !double.IsFinite(zone.Height) ||
            zone.Width < 280 || zone.Height < 280 || zone.Width > 4000 || zone.Height > 4000 || string.IsNullOrWhiteSpace(zone.Id))
            throw new JsonException("Invalid habitat");
        var ids = new HashSet<string>();
        foreach (var item in data.Items)
        {
            if (item == null || !catalog.TryGetValue(item.ItemId, out var definition) || definition.Consumable || !ids.Add(item.Id) ||
                item.ZoneId != zone.Id || !double.IsFinite(item.X) || !double.IsFinite(item.Y) || !double.IsFinite(item.Scale) ||
                item.Scale is < 0.5 or > 1.5 || !double.IsFinite(item.Rotation) || !double.IsFinite(item.Water) ||
                item.Water is < 0 or > 100 || item.FoodQuantity is < 0 or > 5 || !double.IsFinite(item.FoodPortion) || item.FoodPortion is < 0 or > 1 || item.FoodQuantity > 0 && item.FoodPortion <= 0 || !catalog.TryGetValue(item.FoodId, out var food) || food.Category != "Food")
                throw new JsonException("Invalid habitat item");
            if (data.Version < 3) { item.X += zone.X; item.Y += zone.Y; }
            if (item.FoodQuantity > 1)
            {
                var stored = data.Inventory.GetValueOrDefault(item.FoodId);
                if (stored + item.FoodQuantity - 1 + (data.LeftoverFood.ContainsKey(item.FoodId) ? 1 : 0) > 9999) throw new JsonException("Food migration would overflow inventory");
                data.Inventory[item.FoodId] = stored + item.FoodQuantity - 1;
                item.FoodQuantity = 1;
            }
        }
        if (data.Items.GroupBy(i => i.ItemId).Any(g => g.Count() > data.Inventory.GetValueOrDefault(g.Key)))
            throw new JsonException("Unowned placed furniture");
        if (data.DialogueHistory.Any(s => s == null || s.Length > 200) || data.InteractionHistory.Any(i => i == null || !double.IsFinite(i.Time)))
            throw new JsonException("Invalid history");
        data.DialogueHistory = data.DialogueHistory.TakeLast(20).ToList();
        data.InteractionHistory = data.InteractionHistory.TakeLast(40).ToList();
        if (!Momonga.Character.CharacterDefinition.Ids.Contains(data.CharacterId)) data.CharacterId = "momonga";
        data.Settings.PetScale = double.IsFinite(data.Settings.PetScale) ? Math.Clamp(data.Settings.PetScale, 0.5, 2) : 1;
        if (data.Version == 1) { data.ActivityPoints = (int)Math.Min(1_000_000L, (long)data.ActivityPoints * 100); data.HourPoints = 0; data.Version = 2; }
        var oldSigns = data.Items.Where(i => i.ItemId == "quiet-sign").ToArray();
        if (oldSigns.Length > 0)
        {
            var bed = data.Items.FirstOrDefault(i => catalog[i.ItemId].Category == "Bed");
            if (bed != null) bed.Active |= oldSigns.Any(i => i.Active);
            foreach (var sign in oldSigns) data.Items.Remove(sign);
        }
        data.Version = 3;
        data.Settings.PointsPerInput = Math.Clamp(data.Settings.PointsPerInput, 1, 3);
        data.Settings.SpeechFontSize = double.IsFinite(data.Settings.SpeechFontSize) ? Math.Clamp(data.Settings.SpeechFontSize, 10, 22) : 13;
        data.Settings.BubbleScale = double.IsFinite(data.Settings.BubbleScale) ? Math.Clamp(data.Settings.BubbleScale, .6, 1.5) : .85;
        data.HourPoints = Math.Clamp(data.HourPoints, 0, 60000);
        data.Pet.Clamp();
    }

    public Task SaveAsync(SaveData data)
    {
        if (!canWrite) return Task.CompletedTask;
        data.LastSaveTimestamp = DateTimeOffset.UtcNow;
        var snapshot = JsonSerializer.Serialize(data, options);
        return WriteAsync(snapshot);
    }
    private async Task WriteAsync(string snapshot)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                var temp = Path + ".tmp";
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var bytes = Encoding.UTF8.GetBytes(snapshot); stream.Write(bytes); stream.Flush(true);
                }
                if (File.Exists(Path)) File.Replace(temp, Path, Path + ".bak");
                else File.Move(temp, Path);
            }).ConfigureAwait(false);
            Status = "저장됨";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Status = "저장 실패: " + error.Message; }
        finally { gate.Release(); }
    }
}
