using ImmortalLoot.Config;
using ImmortalLoot.Cultivation;
using ImmortalLoot.Realm;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class CultivationMethodServiceTests
    {
        [Test]
        public void Learning_RequiresConfiguredRealmAndIsIdempotent()
        {
            var state = new CultivationMethodState();
            var realm = new RealmProgressState { RealmId = "realm_body_tempering" };
            var service = new CultivationMethodService(Catalog(), realm, state);
            Assert.That(() => service.Learn("method_ember_breath"), Throws.InvalidOperationException);
            realm.RealmId = "realm_qi_coalescence";
            Assert.That(service.Learn("method_ember_breath"), Is.True);
            Assert.That(service.Learn("method_ember_breath"), Is.False);
        }

        [Test]
        public void SlotRules_SeparatePrimaryAndAuxiliaryAndRejectDuplicates()
        {
            var service = Service(out _);
            service.Learn("method_cinder_scripture");
            service.Learn("method_ember_breath");
            service.EquipPrimary("method_cinder_scripture");
            service.EquipAuxiliary(0, "method_ember_breath");
            Assert.That(() => service.EquipAuxiliary(1, "method_ember_breath"), Throws.InvalidOperationException);
            Assert.That(() => service.EquipAuxiliary(1, "method_cinder_scripture"), Throws.InvalidOperationException);
            Assert.That(() => service.EquipPrimary("method_ember_breath"), Throws.InvalidOperationException);
        }

        [Test]
        public void FireBuild_CombinesAttackFireSkillAndBossBonuses()
        {
            var service = Build("method_cinder_scripture", "method_ember_breath");
            var stats = Stats(service);
            Assert.That(stats.Attack, Is.EqualTo(107f).Within(0.001f));
            Assert.That(stats.FireDamage, Is.EqualTo(0.17f).Within(0.001f));
            Assert.That(service.GetSkillMultiplierBonus(ElementType.Fire), Is.EqualTo(0.07f).Within(0.001f));
            Assert.That(service.GetBossDamageBonus(), Is.EqualTo(0.03f).Within(0.001f));
        }

        [Test]
        public void LightningBuild_FavorsCritAndLightningDamage()
        {
            var service = Build("method_thunder_pulse", "method_quick_spark");
            var stats = Stats(service);
            Assert.That(stats.Attack, Is.EqualTo(103f).Within(0.001f));
            Assert.That(stats.CritRate, Is.EqualTo(0.06f).Within(0.001f));
            Assert.That(stats.LightningDamage, Is.EqualTo(0.14f).Within(0.001f));
        }

        [Test]
        public void BloodBuild_FavorsHealthLifeStealLowHealthAndAfk()
        {
            var service = Build("method_crimson_well", "method_blood_return");
            var stats = Stats(service);
            Assert.That(stats.HP, Is.EqualTo(123f).Within(0.001f));
            Assert.That(stats.LifeSteal, Is.EqualTo(0.07f).Within(0.001f));
            Assert.That(service.GetLowHealthDamageBonus(), Is.EqualTo(0.28f).Within(0.001f));
            Assert.That(service.GetAfkMultiplier(), Is.EqualTo(1.08f).Within(0.001f));
        }

        private static CultivationMethodService Build(string primary, string auxiliary)
        {
            var service = Service(out _);
            service.Learn(primary);
            service.Learn(auxiliary);
            service.EquipPrimary(primary);
            service.EquipAuxiliary(0, auxiliary);
            return service;
        }

        private static CultivationMethodService Service(out CultivationMethodState state)
        {
            state = new CultivationMethodState();
            return new CultivationMethodService(Catalog(), new RealmProgressState { RealmId = "realm_spirit_foundation" }, state);
        }

        private static ImmortalLoot.Character.CharacterStats Stats(CultivationMethodService service)
        {
            var stats = new ImmortalLoot.Character.CharacterStatService();
            stats.AddProvider(new CultivationMethodStatProvider(service));
            return stats.Calculate(new ImmortalLoot.Character.CharacterStats { HP = 100, Attack = 100 });
        }

        private static GameConfigCatalog Catalog() => new JsonConfigRepository(new ResourcesConfigSource()).LoadAll();
    }
}
