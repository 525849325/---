using System;
using System.Collections.Generic;
using System.Diagnostics;
using ImmortalLoot.Battle;
using ImmortalLoot.Character;
using ImmortalLoot.Config;
using ImmortalLoot.Core;
using ImmortalLoot.Equipment;
using ImmortalLoot.Inventory;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class PerformanceSmokeTests
    {
        [Test, Timeout(15000)]
        public void LongRun_OneThousandTwentyUnitEncountersAndInventoryRotationStayBounded()
        {
            var calculator = new DamageCalculator(DamageFormulaConfigLoader.Load(new ResourcesConfigSource()), new FixedRandom());
            var watch = Stopwatch.StartNew();
            for (var encounter = 0; encounter < 1000; encounter++)
            {
                var player = new BattleActor("player", new CharacterStats { HP = 10000, Attack = 500, CritDamage = 1.5f }, 1f);
                var enemies = new List<BattleActor>(20);
                for (var index = 0; index < 20; index++)
                    enemies.Add(new BattleActor("enemy_" + index, new CharacterStats { HP = 100, Attack = 1, CritDamage = 1.5f }, 2f));
                var battle = new AutoBattleEngine(player, enemies, calculator) { SuppressPresentationEvents = true };
                battle.SkipToResult();
                Assert.That(battle.State, Is.EqualTo(BattleState.Victory));
            }

            var catalog = new JsonConfigRepository(new ResourcesConfigSource()).LoadAll();
            var generator = new EquipmentGenerator(new FixedRandom(), catalog);
            var inventory = new InventoryService(new InventoryState { EquipmentCapacity = 120 }, catalog);
            for (var index = 0; index < 2000; index++)
            {
                if (inventory.State.Equipment.Count == inventory.State.EquipmentCapacity)
                    inventory.RemoveEquipment(inventory.State.Equipment[0].InstanceId, out _);
                inventory.AddEquipment(generator.Generate(catalog.GetEquipment("weapon_cloudsteel_blade"), 1 + index / 100, EquipmentQuality.Rare, "LongRun"));
            }
            watch.Stop();
            Assert.That(inventory.State.Equipment.Count, Is.EqualTo(120));
            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)), "Desktop domain smoke exceeded its intentionally generous budget.");
        }

        private sealed class FixedRandom : IRandomSource
        {
            public float Value() => 0.99f;
            public int Range(int minInclusive, int maxExclusive) => minInclusive;
            public float Range(float minInclusive, float maxInclusive) => minInclusive;
        }
    }
}
