using System;
using System.Collections.Generic;
using ImmortalLoot.Battle;
using ImmortalLoot.Analytics;
using ImmortalLoot.Config;
using ImmortalLoot.Core;
using ImmortalLoot.Equipment;
using ImmortalLoot.Inventory;
using ImmortalLoot.Player;
using ImmortalLoot.Realm;

internal static class DomainSmokeTests
{
    private static int Main()
    {
        try
        {
            VerifyBattle();
            VerifyValidationTelemetry();
            VerifySaveFailurePolicy();
            VerifyInventoryOverflowProtection();
            VerifyRealmProgressionAndStats();
            VerifyTenThousandEquipmentItems();
            Console.WriteLine("PASS: all domain smoke tests completed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception);
            return 1;
        }
    }

    private static void VerifyBattle()
    {
        var defeatedCount = 0;
        var battle = new AutoBattleEngine(
            new BattleActor("player", new ImmortalLoot.Character.CharacterStats { HP = 100, Attack = 10 }, 0.5f),
            new BattleActor("enemy", new ImmortalLoot.Character.CharacterStats { HP = 20, Attack = 1 }, 2f),
            new DamageCalculator(new DamageFormulaConfig(), new FixedRandom()));
        battle.Finished += state => { if (state == BattleState.Victory) defeatedCount++; };
        for (var i = 0; i < 10 && battle.State == BattleState.Running; i++) battle.Tick(0.25f);
        Require(!battle.Enemy.IsAlive, "enemy should be defeated automatically");
        Require(defeatedCount == 1, "victory event must fire exactly once");
    }

    private static void VerifyValidationTelemetry()
    {
        var sink = new MemoryValidationSink();
        var tracker = new ValidationFunnelTracker(sink, "verification-session", () => new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc));
        tracker.TrackOnce("first_equipment_drop", 42.5, 3, 120, "Rare", 18);
        tracker.TrackOnce("first_equipment_drop", 99, 9, 999, "Mythic", 800);
        Require(sink.Events.Count == 1, "funnel milestone must be emitted once per session");
        Require(sink.Events[0].sessionId == "verification-session", "funnel event must carry its correlation session");
        Require(sink.Events[0].elapsedSeconds == 42.5 && sink.Events[0].itemQuality == "Rare", "funnel event fields must remain structured");
    }

    private static void VerifySaveFailurePolicy()
    {
        Exception observed = null;
        var succeeded = PlayerSaveAttempt.Execute(
            () => throw new System.IO.IOException("simulated disk failure"),
            exception => observed = exception);
        Require(!succeeded, "save failure must degrade to a failed attempt instead of escaping");
        Require(observed is System.IO.IOException, "save failure callback must receive the original exception");

        var writes = 0;
        succeeded = PlayerSaveAttempt.Execute(() => writes++, _ => throw new InvalidOperationException("unexpected failure callback"));
        Require(succeeded && writes == 1, "successful save attempt must execute exactly once");

        var unexpectedEscaped = false;
        try { PlayerSaveAttempt.Execute(() => throw new InvalidOperationException("unsupported save schema")); }
        catch (InvalidOperationException) { unexpectedEscaped = true; }
        Require(unexpectedEscaped, "save guard must not hide programming or schema failures as recoverable storage faults");

        succeeded = PlayerSaveAttempt.Execute(
            () => throw new System.IO.IOException("simulated disk failure"),
            _ => throw new InvalidOperationException("simulated UI reporting failure"));
        Require(!succeeded, "best-effort failure reporting must not rethrow after a recoverable storage fault");
    }

    private static void VerifyInventoryOverflowProtection()
    {
        var equipped = Equipment("equipped", EquipmentQuality.Common, 1);
        var locked = Equipment("locked", EquipmentQuality.Fine, 2);
        locked.IsLocked = true;
        var legendary = Equipment("legendary", EquipmentQuality.Legendary, 3);
        var safe = Equipment("safe", EquipmentQuality.Rare, 4);
        var protectedIds = new HashSet<string> { equipped.InstanceId };
        var selected = InventoryOverflowPolicy.SelectDiscardCandidate(
            new[] { equipped, locked, legendary, safe }, protectedIds);
        Require(ReferenceEquals(selected, safe), "overflow must select only an unprotected sub-Legendary item");
        Require(InventoryOverflowPolicy.SelectDiscardCandidate(
            new[] { equipped, locked, legendary }, protectedIds) == null,
            "overflow must refuse replacement when every item is protected");
    }

    private static void VerifyRealmProgressionAndStats()
    {
        var realms = new Dictionary<string, RealmConfig>
        {
            {
                "realm_body_tempering",
                new RealmConfig
                {
                    Id = "realm_body_tempering", Name = "Body Tempering", Order = 1, StageCount = 10,
                    RequiredLevel = 1, RequiredExp = 100, BreakthroughCost = 500,
                    BreakthroughSuccessRate = 1f, BaseStatBonus = 0.05f
                }
            },
            {
                "realm_qi_coalescence",
                new RealmConfig
                {
                    Id = "realm_qi_coalescence", Name = "Qi Coalescence", Order = 2, StageCount = 10,
                    RequiredLevel = 10, RequiredExp = 1200, BreakthroughCost = 2000,
                    BreakthroughSuccessRate = 0.95f, BaseStatBonus = 0.08f
                }
            }
        };
        var catalog = new GameConfigCatalog(
            new Dictionary<string, EquipmentDefinition>(),
            new Dictionary<EquipmentQuality, AffixCountRange>(),
            realms);
        var state = new RealmProgressState
        {
            RealmId = "realm_body_tempering", RealmStage = 1, PlayerLevel = 1,
            Experience = 7, CultivationExperience = 100, BreakthroughMaterial = 500
        };
        var stats = new ImmortalLoot.Character.CharacterStatService();
        stats.AddProvider(new RealmStatProvider(catalog, state));
        var before = stats.Calculate(new ImmortalLoot.Character.CharacterStats { HP = 100, Attack = 100, Defense = 100 });
        var result = new RealmProgressionService(
            catalog,
            new RealmFormulaConfig
            {
                MinorCostScale = 1f, MinorExpScale = 1f, MinorFailureLossRatio = 0.25f,
                TribulationFailureLossRatio = 0.25f, FailureCooldownSeconds = 300
            },
            state,
            new FixedRandom(),
            new FixedClock()).BeginBreakthrough();
        var after = stats.Calculate(new ImmortalLoot.Character.CharacterStats { HP = 100, Attack = 100, Defense = 100 });

        Require(result.Status == RealmBreakthroughStatus.AdvancedStage, "funded realm breakthrough should advance one stage");
        Require(state.RealmStage == 2 && state.Experience == 7 && state.CultivationExperience == 90 && state.BreakthroughMaterial == 450,
            "realm breakthrough must consume cultivation experience without corrupting level experience");
        Require(after.Attack > before.Attack && after.HP > before.HP,
            "realm stat provider must turn a successful breakthrough into visible character growth");
    }

    private static void VerifyTenThousandEquipmentItems()
    {
        var definition = CreateDefinition();
        var rules = new Dictionary<EquipmentQuality, AffixCountRange>
        {
            { EquipmentQuality.Common, new AffixCountRange(1, 1) },
            { EquipmentQuality.Fine, new AffixCountRange(2, 2) },
            { EquipmentQuality.Rare, new AffixCountRange(2, 3) },
            { EquipmentQuality.Epic, new AffixCountRange(3, 4) },
            { EquipmentQuality.Legendary, new AffixCountRange(4, 5) },
            { EquipmentQuality.Mythic, new AffixCountRange(5, 5) }
        };
        var catalog = new GameConfigCatalog(
            new Dictionary<string, EquipmentDefinition> { { definition.Id, definition } }, rules);
        var generator = new EquipmentGenerator(new SystemRandomSource(20260829), catalog);
        var ids = new HashSet<string>();
        var qualityCounts = new Dictionary<EquipmentQuality, int>();
        foreach (EquipmentQuality quality in Enum.GetValues(typeof(EquipmentQuality))) qualityCounts[quality] = 0;

        for (var i = 0; i < 10000; i++)
        {
            var quality = RollQuality(i);
            var item = generator.Generate(definition, 1 + i % 100, quality, "verification");
            Require(ids.Add(item.InstanceId), "equipment instance IDs must be unique");
            var range = rules[quality];
            Require(item.Affixes.Count >= range.Min && item.Affixes.Count <= range.Max, "affix count outside quality rule");
            var conflictGroups = new HashSet<string>();
            foreach (var affix in item.Affixes)
            {
                var source = definition.AffixPool.Find(value => value.Id == affix.AffixId);
                Require(source != null, "generated an affix outside the legal pool");
                Require(affix.Value >= source.MinValue && affix.Value <= source.MaxValue, "affix value outside configured range");
                Require(string.IsNullOrEmpty(source.ConflictGroup) || conflictGroups.Add(source.ConflictGroup), "conflicting affixes generated together");
            }
            qualityCounts[quality]++;
        }

        Console.WriteLine("Generated 10000 unique legal equipment instances.");
        foreach (var pair in qualityCounts) Console.WriteLine($"  {pair.Key,-10}: {pair.Value}");
    }

    private static EquipmentQuality RollQuality(int index)
    {
        var roll = index % 1000;
        if (roll < 5) return EquipmentQuality.Mythic;
        if (roll < 35) return EquipmentQuality.Legendary;
        if (roll < 150) return EquipmentQuality.Epic;
        if (roll < 450) return EquipmentQuality.Rare;
        if (roll < 800) return EquipmentQuality.Fine;
        return EquipmentQuality.Common;
    }

    private static EquipmentDefinition CreateDefinition() => new EquipmentDefinition
    {
        Id = "verification_weapon",
        DisplayName = "Verification Weapon",
        AffixPool = new List<AffixDefinition>
        {
            Affix("attack_flat", "attack", 3, 9, 100),
            Affix("attack_pct", "attack", 2, 6, 70),
            Affix("crit", "crit", 1, 4, 45),
            Affix("fire", "element", 3, 8, 35),
            Affix("lightning", "element", 3, 8, 35),
            Affix("lifesteal", "sustain", 1, 3, 25),
            Affix("boss_damage", "boss", 2, 7, 20),
            Affix("skill_damage", "skill", 2, 7, 20)
        }
    };

    private static EquipmentInstance Equipment(string id, EquipmentQuality quality, int level) => new EquipmentInstance
    {
        InstanceId = id,
        BaseId = "verification_weapon",
        DisplayName = id,
        Quality = quality,
        Level = level,
        CreateTimeUtc = new DateTime(2026, 8, 30, 0, 0, level, DateTimeKind.Utc)
    };

    private static AffixDefinition Affix(string id, string conflict, float min, float max, int weight) =>
        new AffixDefinition { Id = id, DisplayName = id, ConflictGroup = conflict, MinValue = min, MaxValue = max, Weight = weight };

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FixedRandom : IRandomSource
    {
        public int Range(int minInclusive, int maxExclusive) => minInclusive;
        public float Value() => 0.99f;
    }

    private sealed class FixedClock : IServerClock
    {
        public DateTime UtcNow => new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc);
    }

    private sealed class MemoryValidationSink : IValidationEventSink
    {
        public readonly List<ValidationEvent> Events = new List<ValidationEvent>();
        public void Write(ValidationEvent value) => Events.Add(value);
    }
}

namespace UnityEngine
{
    internal static class JsonUtility
    {
        public static string ToJson(object value) => "{}";
        public static T FromJson<T>(string value) => System.Activator.CreateInstance<T>();
    }
    internal static class Debug { public static void LogWarning(object value) { } }
}
