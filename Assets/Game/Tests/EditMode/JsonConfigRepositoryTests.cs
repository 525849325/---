using ImmortalLoot.Config;
using ImmortalLoot.Equipment;
using NUnit.Framework;
using UnityEngine;

namespace ImmortalLoot.Tests
{
    public sealed class JsonConfigRepositoryTests
    {
        [Test]
        public void LoadAll_ResolvesEquipmentAffixReferencesAndQualityRules()
        {
            var catalog = new JsonConfigRepository(new ResourcesConfigSource()).LoadAll();
            var weapon = catalog.GetEquipment("weapon_cloudsteel_blade");
            Assert.That(catalog.Equipment.Count, Is.EqualTo(10));
            Assert.That(weapon.AffixPool.Count, Is.EqualTo(7));
            Assert.That(weapon.BaseStats.Count, Is.EqualTo(1));
            Assert.That(catalog.SpecialEffects.Count, Is.EqualTo(3));
            Assert.That(catalog.EquipmentSets.Count, Is.EqualTo(2));
            Assert.That(catalog.GetQualityRule(EquipmentQuality.Mythic).Min, Is.EqualTo(5));
            Assert.That(catalog.Realms.Count, Is.EqualTo(10));
            Assert.That(catalog.SpiritualRoots.Count, Is.EqualTo(9));
            Assert.That(catalog.Skills.Count, Is.EqualTo(3));
            Assert.That(catalog.CultivationMethods.Count, Is.EqualTo(6));
            Assert.That(catalog.Monsters["monster_stone_nightmare"].SkillIds, Does.Contain("skill_blood_tide"));
            Assert.That(catalog.Monsters["monster_stone_nightmare"].Attack, Is.EqualTo(24f));
            Assert.That(catalog.Monsters["monster_stone_nightmare"].EnrageSeconds, Is.EqualTo(8f));
            Assert.That(catalog.Stages["stage_1_10"].IsBossStage, Is.True);
            Assert.That(catalog.DropTables.Count, Is.EqualTo(4));
            Assert.That(catalog.ShopItems.Count, Is.EqualTo(2));
            Assert.That(catalog.Activities["activity_double_afk_launch"].RewardModifier, Is.EqualTo(2f));
        }

        [Test]
        public void LoadAll_RejectsMissingCrossTableReference()
        {
            var exception = Assert.Throws<ConfigException>(() =>
                new JsonConfigRepository(new BrokenMonsterDropSource()).LoadAll());
            Assert.That(exception.Message, Does.Contain("references missing id 'drop_missing'"));
        }

        private sealed class BrokenMonsterDropSource : IConfigSource
        {
            public string LoadText(string configName)
            {
                var text = Resources.Load<TextAsset>($"Config/{configName}").text;
                return configName == "monsters"
                    ? text.Replace("\"dropTableId\": \"drop_stage_1\"", "\"dropTableId\": \"drop_missing\"")
                    : text;
            }
        }
    }
}
