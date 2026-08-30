using System.Collections.Generic;
using ImmortalLoot.Core;
using ImmortalLoot.Config;
using ImmortalLoot.Equipment;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class EquipmentGeneratorTests
    {
        [Test]
        public void Generate_ProducesUniqueIdsAndLegalAffixes()
        {
            var definition = CreateDefinition();
            var catalog = new GameConfigCatalog(
                new Dictionary<string, EquipmentDefinition> { { definition.Id, definition } },
                new Dictionary<EquipmentQuality, AffixCountRange> { { EquipmentQuality.Legendary, new AffixCountRange(4, 5) } });
            var generator = new EquipmentGenerator(new SystemRandomSource(42), catalog);
            var ids = new HashSet<string>();
            for (var i = 0; i < 1000; i++)
            {
                var item = generator.Generate(definition, 10, EquipmentQuality.Legendary, "test");
                Assert.That(ids.Add(item.InstanceId), Is.True);
                Assert.That(item.Affixes.Count, Is.InRange(4, 5));
                foreach (var roll in item.Affixes) Assert.That(roll.Value, Is.InRange(1f, 10f));
            }
        }

        private static EquipmentDefinition CreateDefinition()
        {
            var pool = new List<AffixDefinition>();
            for (var i = 0; i < 8; i++) pool.Add(new AffixDefinition { Id = "a" + i, DisplayName = "A" + i, MinValue = 1, MaxValue = 10, Weight = 1, ConflictGroup = "g" + i });
            return new EquipmentDefinition { Id = "weapon_test", DisplayName = "Test", AffixPool = pool };
        }
    }
}
