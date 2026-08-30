using System;
using System.Collections.Generic;
using ImmortalLoot.Battle;
using ImmortalLoot.Analytics;
using ImmortalLoot.Config;
using ImmortalLoot.Core;
using ImmortalLoot.Equipment;

internal static class DomainSmokeTests
{
    private static int Main()
    {
        try
        {
            VerifyBattle();
            VerifyValidationTelemetry();
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

    private sealed class MemoryValidationSink : IValidationEventSink
    {
        public readonly List<ValidationEvent> Events = new List<ValidationEvent>();
        public void Write(ValidationEvent value) => Events.Add(value);
    }
}

namespace UnityEngine
{
    internal static class JsonUtility { public static string ToJson(object value) => "{}"; }
    internal static class Debug { public static void LogWarning(object value) { } }
}
