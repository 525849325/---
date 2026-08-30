using System;
using System.Collections.Generic;
using ImmortalLoot.Character;

namespace ImmortalLoot.Equipment
{
    public enum EquipmentQuality { Common, Fine, Rare, Epic, Legendary, Mythic }
    public enum EquipmentSlot { Weapon, Helmet, Armor, Gloves, Boots, Necklace, Ring1, Ring2, Belt, Artifact }

    [Serializable]
    public sealed class AffixDefinition
    {
        public string Id;
        public string DisplayName;
        public float MinValue;
        public float MaxValue;
        public int Weight;
        public string ConflictGroup;
        public StatId Stat;
        public StatModifierType ModifierType;
    }

    [Serializable]
    public sealed class AffixRoll
    {
        public string AffixId;
        public string DisplayName;
        public float Value;
        public StatId Stat;
        public StatModifierType ModifierType;
    }

    [Serializable]
    public sealed class EquipmentStatDefinition
    {
        public StatId Stat;
        public StatModifierType ModifierType;
        public float BaseValue;
        public float ValuePerLevel;
    }

    [Serializable]
    public sealed class EquipmentStatRoll
    {
        public StatId Stat;
        public StatModifierType ModifierType;
        public float Value;
    }

    [Serializable]
    public sealed class EquipmentBonusDefinition
    {
        public string Id;
        public string DisplayName;
        public string GroupId;
        public int RequiredPieces;
        public List<EquipmentStatRoll> Modifiers = new List<EquipmentStatRoll>();
    }

    [Serializable]
    public sealed class EquipmentDefinition
    {
        public string Id;
        public string DisplayName;
        public EquipmentSlot Slot;
        public float BaseAttack;
        public string SetId;
        public List<string> SpecialEffectPool = new List<string>();
        public List<EquipmentStatDefinition> BaseStats = new List<EquipmentStatDefinition>();
        public List<AffixDefinition> AffixPool = new List<AffixDefinition>();
    }

    [Serializable]
    public sealed class EquipmentInstance
    {
        public string InstanceId;
        public string BaseId;
        public string DisplayName;
        public int Level;
        public EquipmentQuality Quality;
        public DateTime CreateTimeUtc;
        public string Source;
        public string SetId;
        public string SpecialEffectId;
        public bool IsLocked;
        public List<EquipmentStatRoll> BaseStats = new List<EquipmentStatRoll>();
        public List<AffixRoll> Affixes = new List<AffixRoll>();
    }
}
