using System.Security.Cryptography;
using System.Text.Json;
using ImmortalLoot.Server.Persistence;
using ImmortalLoot.Server.Config;

namespace ImmortalLoot.Server.Services;

public sealed record ServerAffixRoll(string Id, double Value);
public sealed record ServerEquipmentDrop(string InstanceId, string BaseId, string Slot, int Level, string Quality, IReadOnlyList<ServerAffixRoll> Affixes);

public sealed class ServerEquipmentDropService(GameDbContext db, IServerClock clock, ServerGameConfigCatalog catalog)
{
    public ServerStageConfig Stage(string stageId) => catalog.Stages.SingleOrDefault(value => value.Id == stageId) ?? throw new KeyNotFoundException($"Stage '{stageId}' was not found.");

    public ServerEquipmentDrop GenerateTracked(Guid playerId, string stageId)
    {
        var stage = Stage(stageId);
        var table = catalog.DropTables[stage.DropTableId];
        var equipmentEntries = table.Entries.Where(entry => catalog.Equipment.Any(value => value.Id == entry.ItemId) && string.IsNullOrEmpty(entry.Condition)).ToArray();
        if (equipmentEntries.Length == 0) throw new InvalidOperationException($"Drop table '{table.Id}' has no equipment entry.");
        var entry = WeightedEntry(equipmentEntries);
        var definition = catalog.Equipment.Single(value => value.Id == entry.ItemId);
        var qualities = new[] { "Common", "Fine", "Rare", "Epic", "Legendary", "Mythic" };
        var minimum = Array.IndexOf(qualities, entry.MinQuality);
        var maximum = Array.IndexOf(qualities, entry.MaxQuality);
        if (minimum < 0 || maximum < minimum) throw new InvalidOperationException($"Drop table '{table.Id}' has an invalid quality range.");
        var qualityIndex = WeightedQuality(minimum, maximum);
        var quality = qualities[qualityIndex];
        var qualityRule = catalog.QualityRules[quality];
        var affixCount = RandomNumberGenerator.GetInt32(qualityRule.MinAffixes, qualityRule.MaxAffixes + 1);
        var available = definition.AffixPool.Select(value => catalog.Affixes[value]).ToList();
        var usedConflictGroups = new HashSet<string>(StringComparer.Ordinal);
        var rolls = new List<ServerAffixRoll>();
        for (var index = 0; index < affixCount; index++)
        {
            var legal = available.Where(value => value.ConflictGroup.Length == 0 || !usedConflictGroups.Contains(value.ConflictGroup)).ToList();
            if (legal.Count == 0) throw new InvalidOperationException($"Equipment '{definition.Id}' cannot satisfy {affixCount} legal affixes.");
            var selected = Weighted(legal);
            available.Remove(selected);
            if (selected.ConflictGroup.Length > 0) usedConflictGroups.Add(selected.ConflictGroup);
            var unit = RandomNumberGenerator.GetInt32(1_000_001) / 1_000_000.0;
            rolls.Add(new ServerAffixRoll(selected.Id, Math.Round(selected.MinValue + (selected.MaxValue - selected.MinValue) * unit, 4)));
        }
        var drop = new ServerEquipmentDrop(Guid.NewGuid().ToString("N"), definition.Id, definition.Slot, Math.Max(1, StageNumber(stageId)), quality, rolls);
        db.PlayerEquipment.Add(new PlayerEquipment
        {
            PlayerId = playerId, InstanceId = drop.InstanceId, BaseId = drop.BaseId, Slot = drop.Slot,
            Level = drop.Level, Quality = drop.Quality, IsLocked = qualityIndex >= 4, InstanceJson = JsonSerializer.Serialize(drop), CreatedAtUtc = clock.UtcNow
        });
        db.EquipmentLogs.Add(new EquipmentLog { PlayerId = playerId, InstanceId = drop.InstanceId, Action = "Drop", ReferenceId = stageId });
        return drop;
    }

    private static ServerDropEntryConfig WeightedEntry(IReadOnlyList<ServerDropEntryConfig> entries)
    {
        var total = entries.Sum(value => value.Weight);
        var roll = RandomNumberGenerator.GetInt32(total);
        foreach (var value in entries) { roll -= value.Weight; if (roll < 0) return value; }
        return entries[^1];
    }

    private static int WeightedQuality(int minimum, int maximum)
    {
        var count = maximum - minimum + 1;
        var total = count * (count + 1) / 2;
        var roll = RandomNumberGenerator.GetInt32(total);
        for (var offset = 0; offset < count; offset++) { roll -= count - offset; if (roll < 0) return minimum + offset; }
        return maximum;
    }

    private static ServerAffixConfig Weighted(IReadOnlyList<ServerAffixConfig> values)
    {
        var total = values.Sum(value => value.Weight);
        var roll = RandomNumberGenerator.GetInt32(total);
        foreach (var value in values) { roll -= value.Weight; if (roll < 0) return value; }
        return values[^1];
    }

    private static int StageNumber(string stageId) => int.TryParse(stageId.Split('_').LastOrDefault(), out var value) ? value : 1;
}
