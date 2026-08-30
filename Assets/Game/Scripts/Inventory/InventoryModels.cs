using System;
using System.Collections.Generic;
using ImmortalLoot.Equipment;
using UnityEngine;

namespace ImmortalLoot.Inventory
{
    public enum ItemCategory { Material, Consumable }
    public enum EquipmentSortMode { QualityDescending, LevelDescending, CreateTimeDescending, Slot }

    [Serializable]
    public sealed class ItemStack
    {
        public string ItemId;
        public int Count;
    }

    [Serializable]
    public sealed class InventoryState
    {
        public int EquipmentCapacity = 100;
        public List<EquipmentInstance> Equipment = new List<EquipmentInstance>();
        public EquipmentInstance PendingEquipment;
        public List<ItemStack> Materials = new List<ItemStack>();
        public List<ItemStack> Consumables = new List<ItemStack>();
    }

    public static class InventoryStateCodec
    {
        public static string Serialize(InventoryState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return JsonUtility.ToJson(state);
        }

        public static InventoryState Deserialize(string json, int minimumEquipmentCapacity = 0)
        {
            var state = string.IsNullOrWhiteSpace(json)
                ? new InventoryState()
                : JsonUtility.FromJson<InventoryState>(json) ?? new InventoryState();
            state.EquipmentCapacity = Math.Max(minimumEquipmentCapacity, state.EquipmentCapacity);
            state.Equipment ??= new List<EquipmentInstance>();
            state.Materials ??= new List<ItemStack>();
            state.Consumables ??= new List<ItemStack>();

            // Unity JsonUtility can round-trip a null inline class as an object whose fields all have default values.
            // Treat only that unusable placeholder as absent; valid pending ownership must remain durable.
            if (state.PendingEquipment != null &&
                (string.IsNullOrWhiteSpace(state.PendingEquipment.InstanceId) ||
                 string.IsNullOrWhiteSpace(state.PendingEquipment.BaseId) ||
                 state.PendingEquipment.Level < 1))
                state.PendingEquipment = null;
            return state;
        }
    }

    public readonly struct DecompositionReward
    {
        public long SoftCurrency { get; }
        public int EnhancementMaterial { get; }
        public int EquipmentEssence { get; }

        public DecompositionReward(long softCurrency, int enhancementMaterial, int equipmentEssence)
        { SoftCurrency = softCurrency; EnhancementMaterial = enhancementMaterial; EquipmentEssence = equipmentEssence; }

        public static DecompositionReward operator +(DecompositionReward left, DecompositionReward right) =>
            new DecompositionReward(left.SoftCurrency + right.SoftCurrency,
                left.EnhancementMaterial + right.EnhancementMaterial,
                left.EquipmentEssence + right.EquipmentEssence);
    }
}
