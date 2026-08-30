using System;

namespace ImmortalLoot.Config
{
    public enum ElementType { None, Metal, Wood, Water, Fire, Earth, Wind, Lightning, Yin, Yang }
    public enum SkillType { Active, Passive }
    public enum SkillTargetType { Self, SingleEnemy, AllEnemies }
    public enum SkillEffectType { Damage, DamageOverTime, Buff, Debuff, Heal }
    public enum MonsterRank { Normal, Elite, Boss }
    public enum CurrencyType { SoftCurrency, PremiumCurrency }
    public enum LimitType { None, Daily, Weekly, Lifetime }
    public enum RefreshType { None, Daily, Weekly }
    public enum ActivityType { AfkRewardMultiplier, DropMultiplier, BossChallenge, Ranking }
    public enum CultivationQuality { Normal, Rare, Epic, Legendary, Ancient }

    [Serializable]
    public sealed class RealmConfig
    {
        public string Id;
        public string Name;
        public int Order;
        public int StageCount;
        public int RequiredLevel;
        public long RequiredExp;
        public long BreakthroughCost;
        public float BreakthroughSuccessRate;
        public float BaseStatBonus;
        public string[] UnlockSystems;
        public int DailyDungeonBonus;
    }

    [Serializable]
    public sealed class SpiritualRootConfig
    {
        public string Id;
        public string Name;
        public ElementType Element;
        public int MaxLevel;
        public float DamageBonusPerLevel;
        public float ResistanceBonusPerLevel;
        public float SkillBonusPerLevel;
    }

    [Serializable]
    public sealed class SkillConfig
    {
        public string Id;
        public string Name;
        public ElementType Element;
        public SkillType Type;
        public float Cooldown;
        public float Multiplier;
        public SkillTargetType TargetType;
        public SkillEffectType EffectType;
        public float EffectValue;
        public float Duration;
        public string UnlockRequirement;
    }

    [Serializable]
    public sealed class CultivationMethodConfig
    {
        public string Id;
        public string Name;
        public CultivationQuality Quality;
        public bool IsPrimary;
        public ElementType Element;
        public string UnlockRealmId;
        public float AttackBonus;
        public float HealthBonus;
        public float LifeStealBonus;
        public float ElementDamageBonus;
        public float CritBonus;
        public float SkillMultiplierBonus;
        public float AfkEfficiencyBonus;
        public float BossDamageBonus;
        public float LowHealthDamageBonus;
    }

    [Serializable]
    public sealed class MonsterConfig
    {
        public string Id;
        public string Name;
        public MonsterRank Rank;
        public float MaxHp;
        public float Attack;
        public float Defense;
        public float AttackInterval;
        public string DropTableId;
        public string[] SkillIds;
        public float EnrageSeconds;
    }

    [Serializable]
    public sealed class StageConfig
    {
        public string Id;
        public int Chapter;
        public int StageNumber;
        public string Name;
        public string[] MonsterGroup;
        public long RecommendedPower;
        public long RewardExp;
        public long RewardSoftCurrency;
        public long FirstClearPremiumCurrency;
        public string DropTableId;
        public string FirstClearDropTableId;
        public float AfkRewardRate;
        public string UnlockCondition;
        public bool IsBossStage;
    }

    [Serializable]
    public sealed class DropEntryConfig
    {
        public string ItemId;
        public int Weight;
        public int MinCount;
        public int MaxCount;
        public string MinQuality;
        public string MaxQuality;
        public string Condition;
    }

    [Serializable]
    public sealed class DropTableConfig
    {
        public string Id;
        public string Name;
        public int RollCount;
        public DropEntryConfig[] Entries;
    }

    [Serializable]
    public sealed class ShopItemConfig
    {
        public string Id;
        public string ShopId;
        public string ItemId;
        public CurrencyType Currency;
        public long Price;
        public LimitType LimitType;
        public int LimitCount;
        public RefreshType RefreshType;
        public string UnlockCondition;
    }

    [Serializable]
    public sealed class ActivityConfig
    {
        public string Id;
        public string Name;
        public ActivityType Type;
        public DateTime StartTimeUtc;
        public DateTime EndTimeUtc;
        public string Condition;
        public float RewardModifier;
    }
}
