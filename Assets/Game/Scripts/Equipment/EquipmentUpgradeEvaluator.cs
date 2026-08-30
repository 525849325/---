using System;
using System.Collections.Generic;
using ImmortalLoot.Character;
using ImmortalLoot.Config;

namespace ImmortalLoot.Equipment
{
    public readonly struct EquipmentUpgradeDecision
    {
        public bool ShouldEquip { get; }
        public long PowerGain { get; }
        public EquipmentUpgradeDecision(bool shouldEquip, long powerGain)
        {
            ShouldEquip = shouldEquip;
            PowerGain = Math.Max(0, powerGain);
        }
    }

    public sealed class EquipmentUpgradeEvaluator
    {
        private readonly EquipmentComparisonService _comparison;
        private readonly PowerCalculator _power;

        public EquipmentUpgradeEvaluator(GameConfigCatalog catalog, PowerCalculator power)
        {
            _comparison = new EquipmentComparisonService(catalog ?? throw new ArgumentNullException(nameof(catalog)));
            _power = power ?? throw new ArgumentNullException(nameof(power));
        }

        public EquipmentUpgradeDecision Evaluate(CharacterStats baseStats, IReadOnlyDictionary<EquipmentSlot, EquipmentInstance> current, EquipmentInstance candidate)
        {
            var comparison = _comparison.Compare(baseStats ?? throw new ArgumentNullException(nameof(baseStats)), current ?? throw new ArgumentNullException(nameof(current)), candidate);
            var before = _power.Calculate(comparison.Current);
            var after = _power.Calculate(comparison.Candidate);
            return new EquipmentUpgradeDecision(after > before, after - before);
        }
    }
}
