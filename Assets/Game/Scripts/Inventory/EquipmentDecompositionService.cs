using System;
using System.Collections.Generic;
using ImmortalLoot.Config;
using ImmortalLoot.Equipment;
using UnityEngine;

namespace ImmortalLoot.Inventory
{
    [Serializable]
    public sealed class DecompositionFormulaConfig
    {
        public int BaseGoldPerLevel;
        public float BaseMaterialPerLevel;
        public float[] QualityMultipliers;
        public int[] EssenceByQuality;
    }

    public static class DecompositionFormulaLoader
    {
        public static DecompositionFormulaConfig Load(IConfigSource source)
        {
            var wrapper = JsonUtility.FromJson<FormulaFile>(source.LoadText("inventory_formula"));
            if (wrapper == null || wrapper.schemaVersion != 1 || wrapper.qualityMultipliers == null || wrapper.qualityMultipliers.Length != 6 || wrapper.essenceByQuality == null || wrapper.essenceByQuality.Length != 6)
                throw new ConfigException("Inventory formula config is invalid.");
            return new DecompositionFormulaConfig
            {
                BaseGoldPerLevel = wrapper.baseGoldPerLevel,
                BaseMaterialPerLevel = wrapper.baseMaterialPerLevel,
                QualityMultipliers = wrapper.qualityMultipliers,
                EssenceByQuality = wrapper.essenceByQuality
            };
        }

        [Serializable]
        private sealed class FormulaFile
        {
            public int schemaVersion;
            public int baseGoldPerLevel;
            public float baseMaterialPerLevel;
            public float[] qualityMultipliers;
            public int[] essenceByQuality;
        }
    }

    public sealed class EquipmentDecompositionService
    {
        private readonly InventoryService _inventory;
        private readonly DecompositionFormulaConfig _formula;

        public EquipmentDecompositionService(InventoryService inventory, DecompositionFormulaConfig formula)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _formula = formula ?? throw new ArgumentNullException(nameof(formula));
        }

        public DecompositionReward Decompose(string instanceId)
        {
            var item = _inventory.State.Equipment.Find(value => value.InstanceId == instanceId);
            if (item == null) throw new InvalidOperationException($"Equipment '{instanceId}' was not found.");
            if (item.IsLocked) throw new InvalidOperationException("Locked equipment cannot be decomposed.");
            _inventory.RemoveEquipment(instanceId, out _);
            return Calculate(item);
        }

        public DecompositionReward DecomposeAll(EquipmentQuality maximumQuality = EquipmentQuality.Epic)
        {
            if (maximumQuality >= EquipmentQuality.Legendary) maximumQuality = EquipmentQuality.Epic;
            var targets = new List<string>();
            foreach (var item in _inventory.State.Equipment)
                if (!item.IsLocked && item.Quality <= maximumQuality) targets.Add(item.InstanceId);
            var total = new DecompositionReward();
            foreach (var id in targets) total += Decompose(id);
            return total;
        }

        public DecompositionReward Calculate(EquipmentInstance item)
        {
            var index = (int)item.Quality;
            var multiplier = _formula.QualityMultipliers[index];
            return new DecompositionReward(
                (long)Math.Round(_formula.BaseGoldPerLevel * item.Level * multiplier),
                Math.Max(1, (int)Math.Round(_formula.BaseMaterialPerLevel * item.Level * multiplier)),
                _formula.EssenceByQuality[index]);
        }
    }
}
