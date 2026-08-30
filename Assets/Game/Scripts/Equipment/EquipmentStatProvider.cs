using System;
using System.Collections.Generic;
using ImmortalLoot.Character;
using ImmortalLoot.Config;

namespace ImmortalLoot.Equipment
{
    public sealed class EquipmentStatProvider : IStatModifierProvider
    {
        private readonly GameConfigCatalog _catalog;
        private readonly EquipmentLoadoutService _loadout;

        public EquipmentStatProvider(GameConfigCatalog catalog, EquipmentLoadoutService loadout)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
        }

        public IEnumerable<StatModifier> GetModifiers() => Collect(_catalog, _loadout.Equipped.Values);

        public static IEnumerable<StatModifier> Collect(GameConfigCatalog catalog, IEnumerable<EquipmentInstance> items)
        {
            var equipped = new List<EquipmentInstance>(items);
            var setCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in equipped)
            {
                foreach (var stat in item.BaseStats)
                    yield return new StatModifier(stat.Stat, stat.ModifierType, stat.Value, $"equipment:{item.InstanceId}:base");
                foreach (var affix in item.Affixes)
                    yield return new StatModifier(affix.Stat, affix.ModifierType, affix.Value, $"equipment:{item.InstanceId}:affix:{affix.AffixId}");
                if (!string.IsNullOrWhiteSpace(item.SpecialEffectId) && catalog.SpecialEffects.TryGetValue(item.SpecialEffectId, out var effect))
                    foreach (var stat in effect.Modifiers)
                        yield return new StatModifier(stat.Stat, stat.ModifierType, stat.Value, $"equipment:{item.InstanceId}:effect:{effect.Id}");
                if (!string.IsNullOrWhiteSpace(item.SetId))
                {
                    setCounts.TryGetValue(item.SetId, out var count);
                    setCounts[item.SetId] = count + 1;
                }
            }
            foreach (var bonus in catalog.EquipmentSets.Values)
            {
                setCounts.TryGetValue(bonus.GroupId, out var pieces);
                if (pieces < bonus.RequiredPieces) continue;
                foreach (var stat in bonus.Modifiers)
                    yield return new StatModifier(stat.Stat, stat.ModifierType, stat.Value, $"equipment_set:{bonus.Id}");
            }
        }
    }

    public readonly struct EquipmentComparison
    {
        public CharacterStats Current { get; }
        public CharacterStats Candidate { get; }
        public float AttackDelta => Candidate.Attack - Current.Attack;
        public float HpDelta => Candidate.HP - Current.HP;
        public float DefenseDelta => Candidate.Defense - Current.Defense;
        public EquipmentComparison(CharacterStats current, CharacterStats candidate) { Current = current; Candidate = candidate; }
    }

    public sealed class EquipmentComparisonService
    {
        private readonly GameConfigCatalog _catalog;
        private readonly CharacterStatAggregator _aggregator = new CharacterStatAggregator();

        public EquipmentComparisonService(GameConfigCatalog catalog) => _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

        public EquipmentComparison Compare(CharacterStats baseStats, IReadOnlyDictionary<EquipmentSlot, EquipmentInstance> current, EquipmentInstance candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            var currentItems = new List<EquipmentInstance>(current.Values);
            var candidateSlot = _catalog.GetEquipment(candidate.BaseId).Slot;
            var candidateItems = new List<EquipmentInstance>();
            foreach (var pair in current) if (pair.Key != candidateSlot) candidateItems.Add(pair.Value);
            candidateItems.Add(candidate);
            var before = _aggregator.Calculate(baseStats, EquipmentStatProvider.Collect(_catalog, currentItems));
            var after = _aggregator.Calculate(baseStats, EquipmentStatProvider.Collect(_catalog, candidateItems));
            return new EquipmentComparison(before, after);
        }
    }
}
