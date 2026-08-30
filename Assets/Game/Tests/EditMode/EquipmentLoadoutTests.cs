using System.Collections.Generic;
using ImmortalLoot.Character;
using ImmortalLoot.Config;
using ImmortalLoot.Equipment;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class EquipmentLoadoutTests
    {
        [Test]
        public void Loadout_SupportsTwoRingsReplacementAndLockState()
        {
            var catalog = Catalog();
            var loadout = new EquipmentLoadoutService(catalog);
            var ring1 = Item("r1", "ring_sunscar");
            var ring2 = Item("r2", "ring_moonpulse");
            loadout.Equip(ring1);
            loadout.Equip(ring2);
            Assert.That(loadout.Equipped.Count, Is.EqualTo(2));
            Assert.That(loadout.Equipped[EquipmentSlot.Ring1], Is.SameAs(ring1));
            Assert.That(loadout.Equipped[EquipmentSlot.Ring2], Is.SameAs(ring2));
            loadout.SetLocked(ring1, true);
            Assert.That(ring1.IsLocked, Is.True);

            var replacement = Item("r3", "ring_sunscar");
            var result = loadout.Equip(replacement);
            Assert.That(result.Replaced, Is.SameAs(ring1));
            Assert.That(loadout.Equipped[EquipmentSlot.Ring1], Is.SameAs(replacement));
        }

        [Test]
        public void StatProvider_AppliesBaseAffixSpecialEffectAndSetThresholds()
        {
            var catalog = Catalog();
            var loadout = new EquipmentLoadoutService(catalog);
            var weapon = Item("weapon", "weapon_cloudsteel_blade", "set_cloudtrace");
            weapon.BaseStats.Add(new EquipmentStatRoll { Stat = StatId.Attack, ModifierType = StatModifierType.Flat, Value = 8 });
            weapon.Affixes.Add(new AffixRoll { AffixId = "crit", Stat = StatId.CritRate, ModifierType = StatModifierType.Flat, Value = 0.03f });
            weapon.SpecialEffectId = "effect_cinder_echo";
            var helmet = Item("helmet", "helmet_mistveil", "set_cloudtrace");
            helmet.BaseStats.Add(new EquipmentStatRoll { Stat = StatId.HP, ModifierType = StatModifierType.Flat, Value = 35 });
            loadout.Equip(weapon);
            loadout.Equip(helmet);
            var service = new CharacterStatService();
            service.AddProvider(new EquipmentStatProvider(catalog, loadout));
            var result = service.Calculate(new CharacterStats { HP = 100, Attack = 10 });
            Assert.That(result.Attack, Is.EqualTo(18));
            Assert.That(result.HP, Is.EqualTo(145.8f).Within(0.01f));
            Assert.That(result.CritRate, Is.EqualTo(0.03f));
            Assert.That(result.FireDamage, Is.EqualTo(0.12f));
        }

        [Test]
        public void Comparison_ReplacesOnlyCandidateSlot()
        {
            var catalog = Catalog();
            var current = Item("old", "weapon_cloudsteel_blade");
            current.BaseStats.Add(new EquipmentStatRoll { Stat = StatId.Attack, ModifierType = StatModifierType.Flat, Value = 5 });
            var candidate = Item("new", "weapon_cloudsteel_blade");
            candidate.BaseStats.Add(new EquipmentStatRoll { Stat = StatId.Attack, ModifierType = StatModifierType.Flat, Value = 12 });
            var comparison = new EquipmentComparisonService(catalog).Compare(
                new CharacterStats { HP = 100, Attack = 10 },
                new Dictionary<EquipmentSlot, EquipmentInstance> { { EquipmentSlot.Weapon, current } }, candidate);
            Assert.That(comparison.AttackDelta, Is.EqualTo(7));
        }

        [Test]
        public void Generator_MythicItemRollsConfiguredSpecialEffectAndSnapshotsBaseStats()
        {
            var catalog = Catalog();
            var item = new EquipmentGenerator(new ImmortalLoot.Core.SystemRandomSource(42), catalog).Generate(
                catalog.GetEquipment("weapon_cloudsteel_blade"), 10, EquipmentQuality.Mythic, "test");
            Assert.That(item.SpecialEffectId, Is.Not.Empty);
            Assert.That(catalog.SpecialEffects.ContainsKey(item.SpecialEffectId), Is.True);
            Assert.That(item.BaseStats[0].Value, Is.EqualTo(18.8f).Within(0.001f));
            Assert.That(item.SetId, Is.EqualTo("set_cloudtrace"));
        }

        private static EquipmentInstance Item(string id, string baseId, string setId = "") => new EquipmentInstance
        {
            InstanceId = id, BaseId = baseId, SetId = setId, Level = 1, Quality = EquipmentQuality.Rare
        };

        private static GameConfigCatalog Catalog() => new JsonConfigRepository(new ResourcesConfigSource()).LoadAll();
    }
}
