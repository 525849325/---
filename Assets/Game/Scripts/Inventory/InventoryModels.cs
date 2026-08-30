using System;
using System.Collections.Generic;
using ImmortalLoot.Equipment;

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
        public List<ItemStack> Materials = new List<ItemStack>();
        public List<ItemStack> Consumables = new List<ItemStack>();
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
