using ImmortalLoot.Battle;
using ImmortalLoot.Character;
using ImmortalLoot.Config;
using ImmortalLoot.Stage;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class StageServicesTests
    {
        [Test]
        public void Progression_UnlocksNextStageAndAwardsFirstClearOnlyOnce()
        {
            var catalog = Catalog();
            var state = new StageProgressState();
            var service = new StageProgressService(catalog, state);
            Assert.That(service.CanEnter("stage_1_1"), Is.True);
            Assert.That(service.CanEnter("stage_1_10"), Is.False);
            var first = service.RecordVictory("stage_1_1");
            Assert.That(first.IsFirstClear, Is.True);
            Assert.That(first.UnlockedStageId, Is.EqualTo("stage_1_2"));
            Assert.That(first.FirstClearDropTableId, Is.EqualTo("drop_first_clear_1"));
            Assert.That(service.CanEnter("stage_1_2"), Is.True);
            Assert.That(service.CanEnter("stage_1_10"), Is.False);
            var repeated = service.RecordVictory("stage_1_1");
            Assert.That(repeated.IsFirstClear, Is.False);
            Assert.That(repeated.FirstClearDropTableId, Is.Empty);
        }

        [Test]
        public void Factories_CreateConfiguredBossEncounter()
        {
            var catalog = Catalog();
            var monsterFactory = new MonsterFactory(catalog);
            var player = new BattleActor("player", new CharacterStats { HP = 1000, Attack = 100, CritDamage = 1.5f }, 1f);
            var battle = new StageBattleFactory(catalog, monsterFactory,
                new DamageCalculator(DamageFormulaConfigLoader.Load(new ResourcesConfigSource()), new DamageCalculatorTests.FixedRandom(0.99f)))
                .Create("stage_1_10", player);
            Assert.That(battle.Enemies.Count, Is.EqualTo(1));
            Assert.That(battle.Enemy.Rank, Is.EqualTo(MonsterRank.Boss));
            Assert.That(battle.Enemy.Id, Is.EqualTo("monster_stone_nightmare"));
            battle.SkipToResult();
            Assert.That(battle.State, Is.EqualTo(BattleState.Victory));
        }

        private static GameConfigCatalog Catalog() => new JsonConfigRepository(new ResourcesConfigSource()).LoadAll();
    }
}
