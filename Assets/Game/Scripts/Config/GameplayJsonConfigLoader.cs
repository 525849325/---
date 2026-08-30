using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ImmortalLoot.Config
{
    internal sealed class GameplayConfigBundle
    {
        public Dictionary<string, RealmConfig> Realms;
        public Dictionary<string, SpiritualRootConfig> SpiritualRoots;
        public Dictionary<string, SkillConfig> Skills;
        public Dictionary<string, CultivationMethodConfig> CultivationMethods;
        public Dictionary<string, MonsterConfig> Monsters;
        public Dictionary<string, StageConfig> Stages;
        public Dictionary<string, DropTableConfig> DropTables;
        public Dictionary<string, ShopItemConfig> ShopItems;
        public Dictionary<string, ActivityConfig> Activities;
    }

    internal static class GameplayJsonConfigLoader
    {
        public static GameplayConfigBundle Load(IConfigSource source)
        {
            var realms = Parse<RealmFile>(source, "realms");
            var roots = Parse<SpiritualRootFile>(source, "spiritual_roots");
            var skills = Parse<SkillFile>(source, "skills");
            var methods = Parse<CultivationFile>(source, "cultivation_methods");
            var drops = Parse<DropTableFile>(source, "drop_tables");
            var monsters = Parse<MonsterFile>(source, "monsters");
            var stages = Parse<StageFile>(source, "stages");
            var shop = Parse<ShopFile>(source, "shop");
            var activities = Parse<ActivityFile>(source, "activities");
            RequireVersion(realms.schemaVersion, "realms");
            RequireVersion(roots.schemaVersion, "spiritual_roots");
            RequireVersion(skills.schemaVersion, "skills");
            RequireVersion(methods.schemaVersion, "cultivation_methods");
            RequireVersion(drops.schemaVersion, "drop_tables");
            RequireVersion(monsters.schemaVersion, "monsters");
            RequireVersion(stages.schemaVersion, "stages");
            RequireVersion(shop.schemaVersion, "shop");
            RequireVersion(activities.schemaVersion, "activities");

            var bundle = new GameplayConfigBundle
            {
                Realms = Map(realms.realms, row => row.id, row => new RealmConfig
                {
                    Id = row.id, Name = row.name, Order = row.order, StageCount = row.stageCount,
                    RequiredLevel = row.requiredLevel, RequiredExp = row.requiredExp,
                    BreakthroughCost = row.breakthroughCost, BreakthroughSuccessRate = row.breakthroughSuccessRate,
                    BaseStatBonus = row.baseStatBonus, UnlockSystems = row.unlockSystems ?? Array.Empty<string>(),
                    DailyDungeonBonus = row.dailyDungeonBonus
                }, "realm"),
                SpiritualRoots = Map(roots.spiritualRoots, row => row.id, row => new SpiritualRootConfig
                {
                    Id = row.id, Name = row.name, Element = EnumValue<ElementType>(row.element, row.id),
                    MaxLevel = row.maxLevel, DamageBonusPerLevel = row.damageBonusPerLevel,
                    ResistanceBonusPerLevel = row.resistanceBonusPerLevel, SkillBonusPerLevel = row.skillBonusPerLevel
                }, "spiritual root"),
                Skills = Map(skills.skills, row => row.id, row => new SkillConfig
                {
                    Id = row.id, Name = row.name, Element = EnumValue<ElementType>(row.element, row.id),
                    Type = EnumValue<SkillType>(row.type, row.id), Cooldown = row.cooldown, Multiplier = row.multiplier,
                    TargetType = EnumValue<SkillTargetType>(row.targetType, row.id),
                    EffectType = EnumValue<SkillEffectType>(row.effectType, row.id), EffectValue = row.effectValue,
                    Duration = row.duration, UnlockRequirement = row.unlockRequirement
                }, "skill"),
                CultivationMethods = Map(methods.cultivationMethods, row => row.id, row => new CultivationMethodConfig
                {
                    Id = row.id, Name = row.name, Quality = EnumValue<CultivationQuality>(row.quality, row.id), IsPrimary = row.isPrimary,
                    Element = EnumValue<ElementType>(row.element, row.id), AttackBonus = row.attackBonus,
                    UnlockRealmId = row.unlockRealmId, HealthBonus = row.healthBonus, LifeStealBonus = row.lifeStealBonus,
                    ElementDamageBonus = row.elementDamageBonus, CritBonus = row.critBonus,
                    SkillMultiplierBonus = row.skillMultiplierBonus, AfkEfficiencyBonus = row.afkEfficiencyBonus,
                    BossDamageBonus = row.bossDamageBonus, LowHealthDamageBonus = row.lowHealthDamageBonus
                }, "cultivation method"),
                DropTables = Map(drops.dropTables, row => row.id, row => new DropTableConfig
                {
                    Id = row.id, Name = row.name, RollCount = row.rollCount,
                    Entries = ConvertDrops(row.entries, row.id)
                }, "drop table"),
                Monsters = Map(monsters.monsters, row => row.id, row => new MonsterConfig
                {
                    Id = row.id, Name = row.name, Rank = EnumValue<MonsterRank>(row.rank, row.id),
                    MaxHp = row.maxHp, Attack = row.attack, Defense = row.defense,
                    AttackInterval = row.attackInterval, DropTableId = row.dropTableId,
                    SkillIds = row.skillIds ?? Array.Empty<string>(), EnrageSeconds = row.enrageSeconds
                }, "monster"),
                Stages = Map(stages.stages, row => row.id, row => new StageConfig
                {
                    Id = row.id, Chapter = row.chapter, StageNumber = row.stageNumber, Name = row.name,
                    MonsterGroup = row.monsterGroup ?? Array.Empty<string>(), RecommendedPower = row.recommendedPower,
                    RewardExp = row.rewardExp, RewardSoftCurrency = row.rewardSoftCurrency,
                    RewardBreakthroughMaterial = row.rewardBreakthroughMaterial, FirstClearPremiumCurrency = row.firstClearPremiumCurrency,
                    DropTableId = row.dropTableId, FirstClearDropTableId = row.firstClearDropTableId,
                    AfkRewardRate = row.afkRewardRate, UnlockCondition = row.unlockCondition, IsBossStage = row.isBossStage
                }, "stage"),
                ShopItems = Map(shop.items, row => row.id, row => new ShopItemConfig
                {
                    Id = row.id, ShopId = row.shopId, ItemId = row.itemId,
                    Currency = EnumValue<CurrencyType>(row.currency, row.id), Price = row.price,
                    LimitType = EnumValue<LimitType>(row.limitType, row.id), LimitCount = row.limitCount,
                    RefreshType = EnumValue<RefreshType>(row.refreshType, row.id), UnlockCondition = row.unlockCondition
                }, "shop item"),
                Activities = Map(activities.activities, row => row.id, row => new ActivityConfig
                {
                    Id = row.id, Name = row.name, Type = EnumValue<ActivityType>(row.type, row.id),
                    StartTimeUtc = DateValue(row.startTimeUtc, row.id), EndTimeUtc = DateValue(row.endTimeUtc, row.id),
                    Condition = row.condition, RewardModifier = row.rewardModifier
                }, "activity")
            };
            return bundle;
        }

        private static T Parse<T>(IConfigSource source, string name) where T : class
        {
            var json = source.LoadText(name);
            if (string.IsNullOrWhiteSpace(json)) throw new ConfigException($"Config '{name}' is empty.");
            try { return JsonUtility.FromJson<T>(json) ?? throw new ConfigException($"Config '{name}' produced no data."); }
            catch (Exception exception) when (!(exception is ConfigException))
            { throw new ConfigException($"Config '{name}' is invalid JSON: {exception.Message}"); }
        }

        private static Dictionary<string, TValue> Map<TRow, TValue>(TRow[] rows, Func<TRow, string> id, Func<TRow, TValue> convert, string label)
        {
            if (rows == null || rows.Length == 0) throw new ConfigException($"The {label} config cannot be empty.");
            var result = new Dictionary<string, TValue>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var key = id(row);
                if (string.IsNullOrWhiteSpace(key) || result.ContainsKey(key)) throw new ConfigException($"Duplicate or empty {label} id '{key}'.");
                result.Add(key, convert(row));
            }
            return result;
        }

        private static DropEntryConfig[] ConvertDrops(DropEntryRow[] rows, string tableId)
        {
            if (rows == null || rows.Length == 0) throw new ConfigException($"Drop table '{tableId}' has no entries.");
            var result = new DropEntryConfig[rows.Length];
            for (var i = 0; i < rows.Length; i++) result[i] = new DropEntryConfig
            {
                ItemId = rows[i].itemId, Weight = rows[i].weight, MinCount = rows[i].minCount,
                MaxCount = rows[i].maxCount, MinQuality = rows[i].minQuality,
                MaxQuality = rows[i].maxQuality, Condition = rows[i].condition
            };
            return result;
        }

        private static T EnumValue<T>(string text, string owner) where T : struct
        {
            if (!Enum.TryParse(text, true, out T value)) throw new ConfigException($"Config '{owner}' has invalid {typeof(T).Name} '{text}'.");
            return value;
        }

        private static DateTime DateValue(string text, string owner)
        {
            if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var value))
                throw new ConfigException($"Activity '{owner}' has invalid UTC date '{text}'.");
            return value.ToUniversalTime();
        }

        private static void RequireVersion(int version, string name)
        {
            if (version != 1) throw new ConfigException($"Config '{name}' has unsupported schema version {version}.");
        }
    }

    internal static class GameplayConfigValidator
    {
        public static void Validate(GameplayConfigBundle data)
        {
            var realmOrders = new HashSet<int>();
            foreach (var realm in data.Realms.Values)
            {
                if (!realmOrders.Add(realm.Order) || realm.StageCount <= 0 || realm.BreakthroughSuccessRate < 0f || realm.BreakthroughSuccessRate > 1f)
                    throw new ConfigException($"Realm '{realm.Id}' has invalid order, stages, or breakthrough rate.");
            }
            foreach (var root in data.SpiritualRoots.Values)
                if (root.MaxLevel <= 0) throw new ConfigException($"Spiritual root '{root.Id}' has invalid max level.");
            foreach (var skill in data.Skills.Values)
                if (skill.Cooldown < 0f || skill.Multiplier < 0f || skill.Duration < 0f) throw new ConfigException($"Skill '{skill.Id}' has invalid numeric values.");
            foreach (var method in data.CultivationMethods.Values)
            {
                RequireRef(data.Realms, method.UnlockRealmId, $"Cultivation method '{method.Id}' unlock realm");
                if (method.AttackBonus < 0 || method.HealthBonus < 0 || method.LifeStealBonus < 0 || method.ElementDamageBonus < 0 || method.CritBonus < 0 || method.SkillMultiplierBonus < 0 || method.AfkEfficiencyBonus < 0 || method.BossDamageBonus < 0 || method.LowHealthDamageBonus < 0)
                    throw new ConfigException($"Cultivation method '{method.Id}' has negative bonuses.");
            }
            foreach (var drop in data.DropTables.Values)
            {
                if (drop.RollCount <= 0) throw new ConfigException($"Drop table '{drop.Id}' has invalid roll count.");
                foreach (var entry in drop.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.ItemId) || entry.Weight <= 0 || entry.MinCount < 0 || entry.MaxCount < entry.MinCount)
                        throw new ConfigException($"Drop table '{drop.Id}' has an invalid entry.");
                    var minQuality = ImmortalLoot.Equipment.EquipmentQuality.Common;
                    var maxQuality = ImmortalLoot.Equipment.EquipmentQuality.Common;
                    if (!string.IsNullOrWhiteSpace(entry.MinQuality) && !Enum.TryParse(entry.MinQuality, true, out minQuality))
                        throw new ConfigException($"Drop table '{drop.Id}' has invalid minimum quality '{entry.MinQuality}'.");
                    if (!string.IsNullOrWhiteSpace(entry.MaxQuality) && !Enum.TryParse(entry.MaxQuality, true, out maxQuality))
                        throw new ConfigException($"Drop table '{drop.Id}' has invalid maximum quality '{entry.MaxQuality}'.");
                    if (!string.IsNullOrWhiteSpace(entry.MinQuality) && !string.IsNullOrWhiteSpace(entry.MaxQuality) && (int)minQuality > (int)maxQuality)
                        throw new ConfigException($"Drop table '{drop.Id}' has reversed quality range.");
                }
            }
            foreach (var monster in data.Monsters.Values)
            {
                RequireRef(data.DropTables, monster.DropTableId, $"Monster '{monster.Id}' drop table");
                foreach (var skillId in monster.SkillIds) RequireRef(data.Skills, skillId, $"Monster '{monster.Id}' skill");
                if (monster.MaxHp <= 0 || monster.Attack <= 0 || monster.AttackInterval <= 0) throw new ConfigException($"Monster '{monster.Id}' has invalid combat values.");
            }
            foreach (var stage in data.Stages.Values)
            {
                if (stage.RewardBreakthroughMaterial < 0) throw new ConfigException($"Stage '{stage.Id}' has a negative breakthrough-material reward.");
                RequireRef(data.DropTables, stage.DropTableId, $"Stage '{stage.Id}' drop table");
                RequireRef(data.DropTables, stage.FirstClearDropTableId, $"Stage '{stage.Id}' first-clear table");
                if (stage.MonsterGroup.Length == 0) throw new ConfigException($"Stage '{stage.Id}' has no monsters.");
                foreach (var monsterId in stage.MonsterGroup) RequireRef(data.Monsters, monsterId, $"Stage '{stage.Id}' monster");
            }
            foreach (var item in data.ShopItems.Values)
                if (item.Price < 0 || item.LimitCount < 0) throw new ConfigException($"Shop item '{item.Id}' has invalid price or limit.");
            foreach (var activity in data.Activities.Values)
                if (activity.EndTimeUtc <= activity.StartTimeUtc || activity.RewardModifier <= 0f) throw new ConfigException($"Activity '{activity.Id}' has invalid time or modifier.");
        }

        private static void RequireRef<T>(Dictionary<string, T> values, string id, string label)
        {
            if (string.IsNullOrWhiteSpace(id) || !values.ContainsKey(id)) throw new ConfigException($"{label} references missing id '{id}'.");
        }
    }

    [Serializable] internal sealed class RealmFile { public int schemaVersion; public RealmRow[] realms; }
    [Serializable] internal sealed class RealmRow { public string id; public string name; public int order; public int stageCount; public int requiredLevel; public long requiredExp; public long breakthroughCost; public float breakthroughSuccessRate; public float baseStatBonus; public string[] unlockSystems; public int dailyDungeonBonus; }
    [Serializable] internal sealed class SpiritualRootFile { public int schemaVersion; public SpiritualRootRow[] spiritualRoots; }
    [Serializable] internal sealed class SpiritualRootRow { public string id; public string name; public string element; public int maxLevel; public float damageBonusPerLevel; public float resistanceBonusPerLevel; public float skillBonusPerLevel; }
    [Serializable] internal sealed class SkillFile { public int schemaVersion; public SkillRow[] skills; }
    [Serializable] internal sealed class SkillRow { public string id; public string name; public string element; public string type; public float cooldown; public float multiplier; public string targetType; public string effectType; public float effectValue; public float duration; public string unlockRequirement; }
    [Serializable] internal sealed class CultivationFile { public int schemaVersion; public CultivationRow[] cultivationMethods; }
    [Serializable] internal sealed class CultivationRow { public string id; public string name; public string quality; public bool isPrimary; public string element; public string unlockRealmId; public float attackBonus; public float healthBonus; public float lifeStealBonus; public float elementDamageBonus; public float critBonus; public float skillMultiplierBonus; public float afkEfficiencyBonus; public float bossDamageBonus; public float lowHealthDamageBonus; }
    [Serializable] internal sealed class DropTableFile { public int schemaVersion; public DropTableRow[] dropTables; }
    [Serializable] internal sealed class DropTableRow { public string id; public string name; public int rollCount; public DropEntryRow[] entries; }
    [Serializable] internal sealed class DropEntryRow { public string itemId; public int weight; public int minCount; public int maxCount; public string minQuality; public string maxQuality; public string condition; }
    [Serializable] internal sealed class MonsterFile { public int schemaVersion; public MonsterRow[] monsters; }
    [Serializable] internal sealed class MonsterRow { public string id; public string name; public string rank; public float maxHp; public float attack; public float defense; public float attackInterval; public string dropTableId; public string[] skillIds; public float enrageSeconds; }
    [Serializable] internal sealed class StageFile { public int schemaVersion; public StageRow[] stages; }
    [Serializable] internal sealed class StageRow { public string id; public int chapter; public int stageNumber; public string name; public string[] monsterGroup; public long recommendedPower; public long rewardExp; public long rewardSoftCurrency; public long rewardBreakthroughMaterial; public long firstClearPremiumCurrency; public string dropTableId; public string firstClearDropTableId; public float afkRewardRate; public string unlockCondition; public bool isBossStage; }
    [Serializable] internal sealed class ShopFile { public int schemaVersion; public ShopRow[] items; }
    [Serializable] internal sealed class ShopRow { public string id; public string shopId; public string itemId; public string currency; public long price; public string limitType; public int limitCount; public string refreshType; public string unlockCondition; }
    [Serializable] internal sealed class ActivityFile { public int schemaVersion; public ActivityRow[] activities; }
    [Serializable] internal sealed class ActivityRow { public string id; public string name; public string type; public string startTimeUtc; public string endTimeUtc; public string condition; public float rewardModifier; }
}
