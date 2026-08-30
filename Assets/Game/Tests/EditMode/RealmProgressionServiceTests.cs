using System;
using ImmortalLoot.Config;
using ImmortalLoot.Core;
using ImmortalLoot.Realm;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class RealmProgressionServiceTests
    {
        [Test]
        public void MinorBreakthrough_ConsumesConfiguredResourcesAndAdvancesOneStage()
        {
            var state = new RealmProgressState { PlayerLevel = 1, Experience = 100, BreakthroughMaterial = 500 };
            var result = Service(state, 0f).BeginBreakthrough();
            Assert.That(result.Status, Is.EqualTo(RealmBreakthroughStatus.AdvancedStage));
            Assert.That(state.RealmStage, Is.EqualTo(2));
            Assert.That(state.Experience, Is.EqualTo(90));
            Assert.That(state.BreakthroughMaterial, Is.EqualTo(450));
        }

        [Test]
        public void MinorFailure_LosesOnlyConfiguredPartAndStartsCooldown()
        {
            var state = new RealmProgressState
            {
                RealmId = "realm_qi_coalescence", RealmStage = 1, PlayerLevel = 10,
                Experience = 1000, BreakthroughMaterial = 1000
            };
            var service = Service(state, 0.99f);
            var failed = service.BeginBreakthrough();
            Assert.That(failed.Status, Is.EqualTo(RealmBreakthroughStatus.Failed));
            Assert.That(failed.MaterialSpent, Is.EqualTo(50));
            Assert.That(state.BreakthroughMaterial, Is.EqualTo(950));
            Assert.That(state.Experience, Is.EqualTo(1000));
            Assert.That(service.BeginBreakthrough().Status, Is.EqualTo(RealmBreakthroughStatus.CooldownActive));
        }

        [Test]
        public void MajorBreakthrough_RequiresOnePendingTribulationAndVictoryUnlocksRealm()
        {
            var state = ReadyForMajor();
            var service = Service(state, 0f);
            var begin = service.BeginBreakthrough();
            Assert.That(begin.Status, Is.EqualTo(RealmBreakthroughStatus.TribulationRequired));
            Assert.That(begin.TrialToken, Is.Not.Empty);
            Assert.That(state.BreakthroughMaterial, Is.EqualTo(1000));
            Assert.That(service.BeginBreakthrough().Status, Is.EqualTo(RealmBreakthroughStatus.TrialAlreadyPending));
            Assert.That(service.ResolveTribulation("wrong", true).Status, Is.EqualTo(RealmBreakthroughStatus.InvalidTrialToken));

            var success = service.ResolveTribulation(begin.TrialToken, true);
            Assert.That(success.Status, Is.EqualTo(RealmBreakthroughStatus.RealmAdvanced));
            Assert.That(state.RealmId, Is.EqualTo("realm_qi_coalescence"));
            Assert.That(state.RealmStage, Is.EqualTo(1));
            Assert.That(state.Experience, Is.EqualTo(800));
            Assert.That(success.UnlockedSystems, Does.Contain("SpiritualRoot"));
            Assert.That(service.ResolveTribulation(begin.TrialToken, true).Status, Is.EqualTo(RealmBreakthroughStatus.InvalidTrialToken));
        }

        [Test]
        public void FailedTribulation_RefundsMostMaterialsAndDoesNotDestroyProgress()
        {
            var state = ReadyForMajor();
            var service = Service(state, 0f);
            var begin = service.BeginBreakthrough();
            var failed = service.ResolveTribulation(begin.TrialToken, false);
            Assert.That(failed.Status, Is.EqualTo(RealmBreakthroughStatus.Failed));
            Assert.That(failed.MaterialSpent, Is.EqualTo(500));
            Assert.That(state.BreakthroughMaterial, Is.EqualTo(2500));
            Assert.That(state.RealmId, Is.EqualTo("realm_body_tempering"));
            Assert.That(state.RealmStage, Is.EqualTo(10));
            Assert.That(state.Experience, Is.EqualTo(2000));
        }

        [Test]
        public void RealmStatProvider_AccumulatesCompletedAndCurrentStageBonuses()
        {
            var catalog = Catalog();
            var state = new RealmProgressState { RealmId = "realm_qi_coalescence", RealmStage = 1 };
            var provider = new RealmStatProvider(catalog, state);
            var service = new ImmortalLoot.Character.CharacterStatService();
            service.AddProvider(provider);
            var stats = service.Calculate(new ImmortalLoot.Character.CharacterStats { HP = 100, Attack = 100, Defense = 100 });
            Assert.That(stats.Attack, Is.EqualTo(105.8f).Within(0.001f));
            Assert.That(stats.HP, Is.EqualTo(105.8f).Within(0.001f));
        }

        [Test]
        public void MaximumRealmCannotAdvanceBeyondConfiguredEnd()
        {
            var state = new RealmProgressState
            {
                RealmId = "realm_immortal_ascent", RealmStage = 10, PlayerLevel = 999,
                Experience = long.MaxValue, BreakthroughMaterial = long.MaxValue
            };
            Assert.That(Service(state, 0f).BeginBreakthrough().Status, Is.EqualTo(RealmBreakthroughStatus.MaximumRealm));
        }

        private static RealmProgressState ReadyForMajor() => new RealmProgressState
        {
            RealmId = "realm_body_tempering", RealmStage = 10, PlayerLevel = 10,
            Experience = 2000, BreakthroughMaterial = 3000
        };

        private static RealmProgressionService Service(RealmProgressState state, float random) => new RealmProgressionService(
            Catalog(), RealmFormulaLoader.Load(new ResourcesConfigSource()), state,
            new FixedRandom(random), new FixedClock());

        private static GameConfigCatalog Catalog() => new JsonConfigRepository(new ResourcesConfigSource()).LoadAll();

        private sealed class FixedRandom : IRandomSource
        {
            private readonly float _value;
            public FixedRandom(float value) => _value = value;
            public int Range(int minInclusive, int maxExclusive) => minInclusive;
            public float Value() => _value;
        }

        private sealed class FixedClock : IServerClock
        {
            public DateTime UtcNow => new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc);
        }
    }
}
