using System;
using System.Collections.Generic;
using ImmortalLoot.Config;
using ImmortalLoot.Core;
using ImmortalLoot.Equipment;

namespace ImmortalLoot.Drop
{
    public enum DropSourceType { Monster, Elite, Boss, Stage, FirstClear, Afk, Activity }

    public readonly struct DropContext
    {
        public DropSourceType Source { get; }
        public int PlayerLevel { get; }
        public bool IsFirstClear { get; }
        public string SourceId { get; }

        public DropContext(DropSourceType source, int playerLevel, string sourceId, bool isFirstClear = false)
        {
            Source = source; PlayerLevel = Math.Max(1, playerLevel); SourceId = sourceId ?? string.Empty; IsFirstClear = isFirstClear;
        }
    }

    public sealed class DropResult
    {
        public string ItemId;
        public int Count;
        public EquipmentQuality? Quality;
        public EquipmentInstance Equipment;
    }

    public interface IDropPityPolicy
    {
        DropEntryConfig SelectGuaranteedEntry(DropTableConfig table, DropContext context);
        void RecordResult(DropTableConfig table, DropContext context, DropResult result);
    }

    public sealed class NoDropPityPolicy : IDropPityPolicy
    {
        public DropEntryConfig SelectGuaranteedEntry(DropTableConfig table, DropContext context) => null;
        public void RecordResult(DropTableConfig table, DropContext context, DropResult result) { }
    }

    public sealed class DropTableService
    {
        private readonly GameConfigCatalog _catalog;
        private readonly EquipmentGenerator _equipment;
        private readonly IRandomSource _random;
        private readonly IDropPityPolicy _pity;

        public DropTableService(GameConfigCatalog catalog, EquipmentGenerator equipment, IRandomSource random, IDropPityPolicy pity = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _pity = pity ?? new NoDropPityPolicy();
        }

        public IReadOnlyList<DropResult> Roll(string dropTableId, DropContext context)
        {
            if (!_catalog.DropTables.TryGetValue(dropTableId, out var table)) throw new ConfigException($"Drop table '{dropTableId}' was not found.");
            var eligible = new List<DropEntryConfig>();
            foreach (var entry in table.Entries) if (ConditionMatches(entry.Condition, context)) eligible.Add(entry);
            if (eligible.Count == 0) throw new InvalidOperationException($"Drop table '{dropTableId}' has no entries eligible for this context.");

            var results = new List<DropResult>(table.RollCount);
            for (var roll = 0; roll < table.RollCount; roll++)
            {
                var guaranteed = _pity.SelectGuaranteedEntry(table, context);
                var entry = guaranteed != null && ConditionMatches(guaranteed.Condition, context) ? guaranteed : PickWeighted(eligible);
                var result = CreateResult(entry, context);
                results.Add(result);
                _pity.RecordResult(table, context, result);
            }
            return results;
        }

        private DropResult CreateResult(DropEntryConfig entry, DropContext context)
        {
            var count = entry.MinCount == entry.MaxCount ? entry.MinCount : _random.Range(entry.MinCount, entry.MaxCount + 1);
            var result = new DropResult { ItemId = entry.ItemId, Count = count };
            if (!_catalog.Equipment.TryGetValue(entry.ItemId, out var equipmentDefinition)) return result;
            var min = ParseQuality(entry.MinQuality, entry.ItemId);
            var max = ParseQuality(entry.MaxQuality, entry.ItemId);
            var quality = RollQuality(min, max);
            result.Quality = quality;
            result.Count = 1;
            result.Equipment = _equipment.Generate(equipmentDefinition, context.PlayerLevel, quality, $"{context.Source}:{context.SourceId}");
            return result;
        }

        private EquipmentQuality RollQuality(EquipmentQuality min, EquipmentQuality max)
        {
            if (min == max) return min;
            var count = (int)max - (int)min + 1;
            var totalWeight = 0;
            for (var i = 0; i < count; i++) totalWeight += count - i;
            var roll = _random.Range(0, totalWeight);
            for (var i = 0; i < count; i++)
            {
                roll -= count - i;
                if (roll < 0) return (EquipmentQuality)((int)min + i);
            }
            return max;
        }

        private DropEntryConfig PickWeighted(List<DropEntryConfig> entries)
        {
            var total = 0;
            foreach (var entry in entries) total += Math.Max(0, entry.Weight);
            if (total <= 0) throw new InvalidOperationException("Eligible drop entries have no positive weight.");
            var roll = _random.Range(0, total);
            foreach (var entry in entries)
            {
                roll -= Math.Max(0, entry.Weight);
                if (roll < 0) return entry;
            }
            return entries[entries.Count - 1];
        }

        private static bool ConditionMatches(string condition, DropContext context)
        {
            if (string.IsNullOrWhiteSpace(condition)) return true;
            if (condition.Equals("FirstClear", StringComparison.OrdinalIgnoreCase)) return context.IsFirstClear;
            if (condition.Equals("Boss", StringComparison.OrdinalIgnoreCase)) return context.Source == DropSourceType.Boss;
            return false;
        }

        private static EquipmentQuality ParseQuality(string value, string itemId)
        {
            if (!Enum.TryParse(value, true, out EquipmentQuality quality)) throw new ConfigException($"Equipment drop '{itemId}' has invalid quality '{value}'.");
            return quality;
        }
    }
}
