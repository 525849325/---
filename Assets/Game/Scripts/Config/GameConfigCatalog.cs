using System;
using System.Collections.Generic;
using ImmortalLoot.Equipment;

namespace ImmortalLoot.Config
{
    public sealed class GameConfigCatalog
    {
        private readonly Dictionary<string, EquipmentDefinition> _equipment;
        private readonly Dictionary<EquipmentQuality, AffixCountRange> _qualityRules;
        private readonly Dictionary<string, RealmConfig> _realms;
        private readonly Dictionary<string, SpiritualRootConfig> _spiritualRoots;
        private readonly Dictionary<string, SkillConfig> _skills;
        private readonly Dictionary<string, CultivationMethodConfig> _cultivationMethods;
        private readonly Dictionary<string, MonsterConfig> _monsters;
        private readonly Dictionary<string, StageConfig> _stages;
        private readonly Dictionary<string, DropTableConfig> _dropTables;
        private readonly Dictionary<string, ShopItemConfig> _shopItems;
        private readonly Dictionary<string, ActivityConfig> _activities;
        private readonly Dictionary<string, AffixDefinition> _affixes;
        private readonly Dictionary<string, EquipmentBonusDefinition> _specialEffects;
        private readonly Dictionary<string, EquipmentBonusDefinition> _equipmentSets;

        public IReadOnlyDictionary<string, EquipmentDefinition> Equipment => _equipment;
        public IReadOnlyDictionary<EquipmentQuality, AffixCountRange> QualityRules => _qualityRules;
        public IReadOnlyDictionary<string, RealmConfig> Realms => _realms;
        public IReadOnlyDictionary<string, SpiritualRootConfig> SpiritualRoots => _spiritualRoots;
        public IReadOnlyDictionary<string, SkillConfig> Skills => _skills;
        public IReadOnlyDictionary<string, CultivationMethodConfig> CultivationMethods => _cultivationMethods;
        public IReadOnlyDictionary<string, MonsterConfig> Monsters => _monsters;
        public IReadOnlyDictionary<string, StageConfig> Stages => _stages;
        public IReadOnlyDictionary<string, DropTableConfig> DropTables => _dropTables;
        public IReadOnlyDictionary<string, ShopItemConfig> ShopItems => _shopItems;
        public IReadOnlyDictionary<string, ActivityConfig> Activities => _activities;
        public IReadOnlyDictionary<string, AffixDefinition> Affixes => _affixes;
        public IReadOnlyDictionary<string, EquipmentBonusDefinition> SpecialEffects => _specialEffects;
        public IReadOnlyDictionary<string, EquipmentBonusDefinition> EquipmentSets => _equipmentSets;

        public GameConfigCatalog(
            Dictionary<string, EquipmentDefinition> equipment,
            Dictionary<EquipmentQuality, AffixCountRange> qualityRules,
            Dictionary<string, RealmConfig> realms = null,
            Dictionary<string, SpiritualRootConfig> spiritualRoots = null,
            Dictionary<string, SkillConfig> skills = null,
            Dictionary<string, CultivationMethodConfig> cultivationMethods = null,
            Dictionary<string, MonsterConfig> monsters = null,
            Dictionary<string, StageConfig> stages = null,
            Dictionary<string, DropTableConfig> dropTables = null,
            Dictionary<string, ShopItemConfig> shopItems = null,
            Dictionary<string, ActivityConfig> activities = null,
            Dictionary<string, AffixDefinition> affixes = null,
            Dictionary<string, EquipmentBonusDefinition> specialEffects = null,
            Dictionary<string, EquipmentBonusDefinition> equipmentSets = null)
        {
            _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            _qualityRules = qualityRules ?? throw new ArgumentNullException(nameof(qualityRules));
            _realms = realms ?? new Dictionary<string, RealmConfig>(StringComparer.Ordinal);
            _spiritualRoots = spiritualRoots ?? new Dictionary<string, SpiritualRootConfig>(StringComparer.Ordinal);
            _skills = skills ?? new Dictionary<string, SkillConfig>(StringComparer.Ordinal);
            _cultivationMethods = cultivationMethods ?? new Dictionary<string, CultivationMethodConfig>(StringComparer.Ordinal);
            _monsters = monsters ?? new Dictionary<string, MonsterConfig>(StringComparer.Ordinal);
            _stages = stages ?? new Dictionary<string, StageConfig>(StringComparer.Ordinal);
            _dropTables = dropTables ?? new Dictionary<string, DropTableConfig>(StringComparer.Ordinal);
            _shopItems = shopItems ?? new Dictionary<string, ShopItemConfig>(StringComparer.Ordinal);
            _activities = activities ?? new Dictionary<string, ActivityConfig>(StringComparer.Ordinal);
            _affixes = affixes ?? new Dictionary<string, AffixDefinition>(StringComparer.Ordinal);
            _specialEffects = specialEffects ?? new Dictionary<string, EquipmentBonusDefinition>(StringComparer.Ordinal);
            _equipmentSets = equipmentSets ?? new Dictionary<string, EquipmentBonusDefinition>(StringComparer.Ordinal);
        }

        public EquipmentDefinition GetEquipment(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !_equipment.TryGetValue(id, out var value))
                throw new ConfigException($"Equipment config '{id}' was not found.");
            return value;
        }

        public AffixCountRange GetQualityRule(EquipmentQuality quality)
        {
            if (!_qualityRules.TryGetValue(quality, out var value))
                throw new ConfigException($"Quality rule '{quality}' was not found.");
            return value;
        }
    }

    public readonly struct AffixCountRange
    {
        public int Min { get; }
        public int Max { get; }

        public AffixCountRange(int min, int max)
        {
            Min = min;
            Max = max;
        }
    }
}
