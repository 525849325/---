using System;
using System.Collections.Generic;
using ImmortalLoot.Character;
using ImmortalLoot.Equipment;
using UnityEngine;

namespace ImmortalLoot.Config
{
    public sealed class ResourcesConfigSource : IConfigSource
    {
        private readonly string _root;

        public ResourcesConfigSource(string root = "Config") => _root = root.TrimEnd('/');

        public string LoadText(string configName)
        {
            var asset = Resources.Load<TextAsset>($"{_root}/{configName}");
            if (asset == null) throw new ConfigException($"Missing runtime config: Resources/{_root}/{configName}.json");
            return asset.text;
        }
    }

    public sealed class JsonConfigRepository : IConfigRepository
    {
        private readonly IConfigSource _source;

        public JsonConfigRepository(IConfigSource source) => _source = source ?? throw new ArgumentNullException(nameof(source));

        public GameConfigCatalog LoadAll()
        {
            var affixFile = Parse<AffixFile>(_source.LoadText("affixes"), "affixes");
            var equipmentFile = Parse<EquipmentFile>(_source.LoadText("equipment"), "equipment");
            var qualityFile = Parse<QualityRuleFile>(_source.LoadText("quality_rules"), "quality_rules");
            ConfigValidator.Validate(affixFile, equipmentFile, qualityFile);

            var affixes = new Dictionary<string, AffixDefinition>(StringComparer.Ordinal);
            foreach (var row in affixFile.affixes)
            {
                affixes.Add(row.id, new AffixDefinition
                {
                    Id = row.id,
                    DisplayName = row.displayName,
                    MinValue = row.minValue,
                    MaxValue = row.maxValue,
                    Weight = row.weight,
                    ConflictGroup = row.conflictGroup,
                    Stat = ParseEnum<StatId>(row.stat, $"Affix '{row.id}' stat"),
                    ModifierType = ParseEnum<StatModifierType>(row.modifierType, $"Affix '{row.id}' modifier type")
                });
            }

            var equipment = new Dictionary<string, EquipmentDefinition>(StringComparer.Ordinal);
            foreach (var row in equipmentFile.equipment)
            {
                if (!Enum.TryParse(row.slot, true, out EquipmentSlot slot))
                    throw new ConfigException($"Equipment '{row.id}' has invalid slot '{row.slot}'.");
                var definition = new EquipmentDefinition
                {
                    Id = row.id,
                    DisplayName = row.displayName,
                    Slot = slot,
                    BaseAttack = row.baseAttack
                };
                definition.SetId = row.setId;
                if (row.specialEffectPool != null) definition.SpecialEffectPool.AddRange(row.specialEffectPool);
                if (row.baseStats != null)
                {
                    foreach (var stat in row.baseStats) definition.BaseStats.Add(new EquipmentStatDefinition
                    {
                        Stat = ParseEnum<StatId>(stat.stat, $"Equipment '{row.id}' base stat"),
                        ModifierType = ParseEnum<StatModifierType>(stat.modifierType, $"Equipment '{row.id}' base modifier type"),
                        BaseValue = stat.baseValue,
                        ValuePerLevel = stat.valuePerLevel
                    });
                }
                foreach (var affixId in row.affixPool)
                {
                    if (!affixes.TryGetValue(affixId, out var affix))
                        throw new ConfigException($"Equipment '{row.id}' references missing affix '{affixId}'.");
                    definition.AffixPool.Add(affix);
                }
                equipment.Add(row.id, definition);
            }

            var rules = new Dictionary<EquipmentQuality, AffixCountRange>();
            foreach (var row in qualityFile.rules)
            {
                if (!Enum.TryParse(row.quality, true, out EquipmentQuality quality))
                    throw new ConfigException($"Invalid equipment quality '{row.quality}'.");
                rules.Add(quality, new AffixCountRange(row.minAffixes, row.maxAffixes));
            }
            var requiredCapacity = 0;
            foreach (var rule in rules.Values) requiredCapacity = Math.Max(requiredCapacity, rule.Max);
            foreach (var definition in equipment.Values)
            {
                var capacity = AffixGenerator.CountMaximumLegalAffixes(definition.AffixPool);
                if (capacity < requiredCapacity) throw new ConfigException($"Equipment '{definition.Id}' supports only {capacity} conflict-free affixes, but quality rules require {requiredCapacity}.");
                if (!rules.ContainsKey(EquipmentQuality.Mythic) || definition.SpecialEffectPool.Count == 0)
                    throw new ConfigException($"Equipment '{definition.Id}' must define a Mythic special effect pool.");
            }
            var specialEffects = ConvertBonuses(equipmentFile.specialEffects, "special effect");
            var equipmentSets = ConvertBonuses(equipmentFile.sets, "equipment set");
            foreach (var definition in equipment.Values)
            {
                if (!string.IsNullOrWhiteSpace(definition.SetId) && !ContainsBonusGroup(equipmentSets, definition.SetId))
                    throw new ConfigException($"Equipment '{definition.Id}' references missing set '{definition.SetId}'.");
                foreach (var effectId in definition.SpecialEffectPool)
                    if (!specialEffects.ContainsKey(effectId)) throw new ConfigException($"Equipment '{definition.Id}' references missing special effect '{effectId}'.");
            }
            var gameplay = GameplayJsonConfigLoader.Load(_source);
            GameplayConfigValidator.Validate(gameplay);
            return new GameConfigCatalog(
                equipment, rules, gameplay.Realms, gameplay.SpiritualRoots, gameplay.Skills,
                gameplay.CultivationMethods, gameplay.Monsters, gameplay.Stages,
                gameplay.DropTables, gameplay.ShopItems, gameplay.Activities,
                affixes, specialEffects, equipmentSets);
        }

        private static T Parse<T>(string json, string name) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ConfigException($"Config '{name}' is empty.");
            try
            {
                var value = JsonUtility.FromJson<T>(json);
                return value ?? throw new ConfigException($"Config '{name}' produced no data.");
            }
            catch (Exception exception) when (!(exception is ConfigException))
            {
                throw new ConfigException($"Config '{name}' is invalid JSON: {exception.Message}");
            }
        }

        private static T ParseEnum<T>(string value, string label) where T : struct
        {
            if (!Enum.TryParse(value, true, out T result)) throw new ConfigException($"{label} has invalid value '{value}'.");
            return result;
        }

        private static Dictionary<string, EquipmentBonusDefinition> ConvertBonuses(EquipmentBonusRow[] rows, string label)
        {
            var result = new Dictionary<string, EquipmentBonusDefinition>(StringComparer.Ordinal);
            if (rows == null) return result;
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.id) || result.ContainsKey(row.id)) throw new ConfigException($"Duplicate or empty {label} id '{row.id}'.");
                var bonus = new EquipmentBonusDefinition { Id = row.id, DisplayName = row.displayName, GroupId = string.IsNullOrWhiteSpace(row.groupId) ? row.id : row.groupId, RequiredPieces = row.requiredPieces };
                if (row.modifiers != null) foreach (var modifier in row.modifiers) bonus.Modifiers.Add(new EquipmentStatRoll
                {
                    Stat = ParseEnum<StatId>(modifier.stat, $"{label} '{row.id}' stat"),
                    ModifierType = ParseEnum<StatModifierType>(modifier.modifierType, $"{label} '{row.id}' modifier type"),
                    Value = modifier.value
                });
                result.Add(row.id, bonus);
            }
            return result;
        }

        private static bool ContainsBonusGroup(Dictionary<string, EquipmentBonusDefinition> bonuses, string groupId)
        {
            foreach (var bonus in bonuses.Values) if (bonus.GroupId == groupId) return true;
            return false;
        }
    }

    internal static class ConfigValidator
    {
        public static void Validate(AffixFile affixes, EquipmentFile equipment, QualityRuleFile qualityRules)
        {
            if (affixes.schemaVersion != 1 || equipment.schemaVersion != 1 || qualityRules.schemaVersion != 1)
                throw new ConfigException("Unsupported config schema version. Expected version 1.");
            if (affixes.affixes == null || affixes.affixes.Length == 0) throw new ConfigException("Affix config cannot be empty.");
            if (equipment.equipment == null || equipment.equipment.Length == 0) throw new ConfigException("Equipment config cannot be empty.");
            if (qualityRules.rules == null || qualityRules.rules.Length == 0) throw new ConfigException("Quality rules cannot be empty.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in affixes.affixes)
            {
                if (string.IsNullOrWhiteSpace(row.id) || !ids.Add(row.id)) throw new ConfigException($"Duplicate or empty affix id '{row.id}'.");
                if (row.minValue > row.maxValue || row.weight < 0) throw new ConfigException($"Affix '{row.id}' has an invalid range or weight.");
            }
            ids.Clear();
            foreach (var row in equipment.equipment)
            {
                if (string.IsNullOrWhiteSpace(row.id) || !ids.Add(row.id)) throw new ConfigException($"Duplicate or empty equipment id '{row.id}'.");
                if (row.affixPool == null || row.affixPool.Length == 0) throw new ConfigException($"Equipment '{row.id}' has an empty affix pool.");
            }
            foreach (var row in qualityRules.rules)
            {
                if (row.minAffixes < 0 || row.maxAffixes < row.minAffixes)
                    throw new ConfigException($"Quality '{row.quality}' has an invalid affix count range.");
            }
        }
    }

    [Serializable] internal sealed class AffixFile { public int schemaVersion; public AffixRow[] affixes; }
    [Serializable] internal sealed class AffixRow { public string id; public string displayName; public float minValue; public float maxValue; public int weight; public string conflictGroup; public string stat; public string modifierType; }
    [Serializable] internal sealed class EquipmentFile { public int schemaVersion; public EquipmentRow[] equipment; public EquipmentBonusRow[] specialEffects; public EquipmentBonusRow[] sets; }
    [Serializable] internal sealed class EquipmentRow { public string id; public string displayName; public string slot; public float baseAttack; public string setId; public string[] specialEffectPool; public EquipmentBaseStatRow[] baseStats; public string[] affixPool; }
    [Serializable] internal sealed class EquipmentBaseStatRow { public string stat; public string modifierType; public float baseValue; public float valuePerLevel; }
    [Serializable] internal sealed class EquipmentBonusRow { public string id; public string displayName; public string groupId; public int requiredPieces; public EquipmentModifierRow[] modifiers; }
    [Serializable] internal sealed class EquipmentModifierRow { public string stat; public string modifierType; public float value; }
    [Serializable] internal sealed class QualityRuleFile { public int schemaVersion; public QualityRuleRow[] rules; }
    [Serializable] internal sealed class QualityRuleRow { public string quality; public int minAffixes; public int maxAffixes; }
}
