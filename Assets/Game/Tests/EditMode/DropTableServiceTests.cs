using System.Collections.Generic;
using ImmortalLoot.Config;
using ImmortalLoot.Core;
using ImmortalLoot.Drop;
using ImmortalLoot.Equipment;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class DropTableServiceTests
    {
        [Test]
        public void StageTable_TenThousandRollsFollowWeightsAndProduceLegalEquipment()
        {
            var service = CreateService(20260829);
            var currency = 0;
            var equipment = 0;
            for (var i = 0; i < 10000; i++)
            {
                var result = service.Roll("drop_stage_1", new DropContext(DropSourceType.Stage, 10, "stage_1_1"))[0];
                if (result.Equipment == null)
                {
                    currency++;
                    Assert.That(result.Count, Is.InRange(8, 14));
                }
                else
                {
                    equipment++;
                    Assert.That(result.Equipment.Level, Is.EqualTo(10));
                    Assert.That(result.Quality.Value, Is.InRange(EquipmentQuality.Fine, EquipmentQuality.Epic));
                }
            }
            Assert.That(currency, Is.InRange(5700, 6300));
            Assert.That(equipment, Is.EqualTo(10000 - currency));
        }

        [Test]
        public void BossTable_UsesConfiguredRollCountAndHigherQualityFloor()
        {
            var results = CreateService(42).Roll("drop_boss_1", new DropContext(DropSourceType.Boss, 20, "monster_stone_nightmare"));
            Assert.That(results.Count, Is.EqualTo(2));
            foreach (var result in results)
                if (result.Equipment != null) Assert.That(result.Quality.Value, Is.GreaterThanOrEqualTo(EquipmentQuality.Rare));
        }

        [Test]
        public void FirstClearCondition_BlocksIneligibleClaim()
        {
            var service = CreateService(1);
            Assert.That(() => service.Roll("drop_first_clear_1", new DropContext(DropSourceType.Stage, 1, "stage_1_1")), Throws.InvalidOperationException);
            var result = service.Roll("drop_first_clear_1", new DropContext(DropSourceType.FirstClear, 1, "stage_1_1", true))[0];
            Assert.That(result.ItemId, Is.EqualTo("premium_currency"));
            Assert.That(result.Count, Is.EqualTo(10));
        }

        [Test]
        public void PityPolicy_CanGuaranteeConfiguredEntryWithoutChangingService()
        {
            var catalog = Catalog();
            var random = new SystemRandomSource(9);
            var forced = catalog.DropTables["drop_stage_1"].Entries[1];
            var service = new DropTableService(catalog, new EquipmentGenerator(random, catalog), random, new ForcePolicy(forced));
            var result = service.Roll("drop_stage_1", new DropContext(DropSourceType.Stage, 5, "stage_1_1"))[0];
            Assert.That(result.Equipment, Is.Not.Null);
            Assert.That(result.ItemId, Is.EqualTo("weapon_cloudsteel_blade"));
        }

        private static DropTableService CreateService(int seed)
        {
            var catalog = Catalog();
            var random = new SystemRandomSource(seed);
            return new DropTableService(catalog, new EquipmentGenerator(random, catalog), random);
        }

        private static GameConfigCatalog Catalog() => new JsonConfigRepository(new ResourcesConfigSource()).LoadAll();

        private sealed class ForcePolicy : IDropPityPolicy
        {
            private readonly DropEntryConfig _entry;
            public ForcePolicy(DropEntryConfig entry) => _entry = entry;
            public DropEntryConfig SelectGuaranteedEntry(DropTableConfig table, DropContext context) => _entry;
            public void RecordResult(DropTableConfig table, DropContext context, DropResult result) { }
        }
    }
}
