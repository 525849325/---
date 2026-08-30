using System.Collections.Generic;
using ImmortalLoot.Config;
using ImmortalLoot.Equipment;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class AffixGeneratorTests
    {
        [Test]
        public void Generate_ThrowsWhenConflictFreeCapacityCannotMeetRequest()
        {
            var pool = new[]
            {
                Affix("a", "same"), Affix("b", "same"), Affix("c", "other")
            };
            var generator = new AffixGenerator(new ImmortalLoot.Core.SystemRandomSource(1));
            Assert.That(() => generator.Generate(pool, 3), Throws.TypeOf<AffixGenerationException>());
        }

        [Test]
        public void RuntimeCatalog_GeneratesTenThousandLegalMythicItems()
        {
            var catalog = new JsonConfigRepository(new ResourcesConfigSource()).LoadAll();
            var generator = new EquipmentGenerator(new ImmortalLoot.Core.SystemRandomSource(20260829), catalog);
            var ids = new HashSet<string>();
            var generated = 0;
            foreach (var definition in catalog.Equipment.Values)
            {
                for (var i = 0; i < 1000; i++)
                {
                    var item = generator.Generate(definition, 1 + i % 100, EquipmentQuality.Mythic, "stat_test");
                    Assert.That(ids.Add(item.InstanceId), Is.True);
                    Assert.That(item.Affixes.Count, Is.EqualTo(5));
                    Assert.That(item.SpecialEffectId, Is.Not.Empty);
                    var conflicts = new HashSet<string>();
                    foreach (var roll in item.Affixes)
                    {
                        Assert.That(catalog.Affixes.ContainsKey(roll.AffixId), Is.True);
                        var source = catalog.Affixes[roll.AffixId];
                        Assert.That(roll.Value, Is.InRange(source.MinValue, source.MaxValue));
                        Assert.That(string.IsNullOrWhiteSpace(source.ConflictGroup) || conflicts.Add(source.ConflictGroup), Is.True);
                    }
                    generated++;
                }
            }
            Assert.That(generated, Is.EqualTo(10000));
            Assert.That(ids.Count, Is.EqualTo(10000));
        }

        private static AffixDefinition Affix(string id, string conflict) => new AffixDefinition
        {
            Id = id, DisplayName = id, ConflictGroup = conflict, MinValue = 1, MaxValue = 2, Weight = 1
        };
    }
}
