using System;
using ImmortalLoot.Config;
using ImmortalLoot.Core;
using ImmortalLoot.Realm;
using ImmortalLoot.SpiritualRoot;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class SpiritualRootServiceTests
    {
        [Test]
        public void Grant_IsRandomWithinAvailableRootsAndIdempotentByTribulationToken()
        {
            var state = new SpiritualRootState();
            var service = new SpiritualRootService(Catalog(), state, new FirstRandom());
            var first = service.GrantFromTribulation("trial_1");
            var repeated = service.GrantFromTribulation("trial_1");
            Assert.That(first.Status, Is.EqualTo(SpiritualRootGrantStatus.Granted));
            Assert.That(first.NewLevel, Is.EqualTo(1));
            Assert.That(repeated.Status, Is.EqualTo(SpiritualRootGrantStatus.AlreadyGranted));
            Assert.That(repeated.RootId, Is.EqualTo(first.RootId));
            Assert.That(state.GrantRecords.Count, Is.EqualTo(1));
        }

        [Test]
        public void Grant_SkipsMaximumRootsAndReportsWhenAllAreFull()
        {
            var catalog = Catalog();
            var state = new SpiritualRootState();
            foreach (var config in catalog.SpiritualRoots.Values)
                state.Roots.Add(new SpiritualRootProgress { RootId = config.Id, Level = config.MaxLevel });
            var result = new SpiritualRootService(catalog, state, new FirstRandom()).GrantFromTribulation("trial_full");
            Assert.That(result.Status, Is.EqualTo(SpiritualRootGrantStatus.AllRootsAtMaximum));
            Assert.That(state.GrantRecords.Count, Is.EqualTo(0));
        }

        [Test]
        public void StatProviderAndSkillBonus_UseConfiguredPerLevelValues()
        {
            var catalog = Catalog();
            var state = new SpiritualRootState();
            state.Roots.Add(new SpiritualRootProgress { RootId = "root_fire", Level = 2 });
            var rootService = new SpiritualRootService(catalog, state, new FirstRandom());
            var stats = new ImmortalLoot.Character.CharacterStatService();
            stats.AddProvider(new SpiritualRootStatProvider(catalog, state));
            var result = stats.Calculate(new ImmortalLoot.Character.CharacterStats { HP = 100, Attack = 10 });
            Assert.That(result.FireDamage, Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(result.FireResistance, Is.EqualTo(0.01f).Within(0.0001f));
            Assert.That(rootService.GetSkillBonus(ElementType.Fire), Is.EqualTo(0.012f).Within(0.0001f));
        }

        [Test]
        public void Coordinator_GrantsExactlyOneRootOnlyAfterSuccessfulRealmAdvance()
        {
            var catalog = Catalog();
            var realmState = new RealmProgressState
            {
                RealmId = "realm_body_tempering", RealmStage = 10, PlayerLevel = 10,
                Experience = 2000, BreakthroughMaterial = 3000
            };
            var realm = new RealmProgressionService(catalog, RealmFormulaLoader.Load(new ResourcesConfigSource()), realmState, new FirstRandom(), new FixedClock());
            var rootState = new SpiritualRootState();
            var roots = new SpiritualRootService(catalog, rootState, new FirstRandom());
            var begin = realm.BeginBreakthrough();
            var coordinator = new TribulationRewardCoordinator(realm, roots);
            var resolution = coordinator.Resolve(begin.TrialToken, true);
            Assert.That(resolution.Realm.Status, Is.EqualTo(RealmBreakthroughStatus.RealmAdvanced));
            Assert.That(resolution.SpiritualRoot.HasValue, Is.True);
            Assert.That(resolution.SpiritualRoot.Value.Status, Is.EqualTo(SpiritualRootGrantStatus.Granted));
            Assert.That(rootState.GrantRecords.Count, Is.EqualTo(1));
            var repeated = coordinator.Resolve(begin.TrialToken, true);
            Assert.That(repeated.Realm.Status, Is.EqualTo(RealmBreakthroughStatus.InvalidTrialToken));
            Assert.That(rootState.GrantRecords.Count, Is.EqualTo(1));
        }

        private static GameConfigCatalog Catalog() => new JsonConfigRepository(new ResourcesConfigSource()).LoadAll();

        private sealed class FirstRandom : IRandomSource
        {
            public int Range(int minInclusive, int maxExclusive) => minInclusive;
            public float Value() => 0f;
        }

        private sealed class FixedClock : IServerClock
        {
            public DateTime UtcNow => new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc);
        }
    }
}
