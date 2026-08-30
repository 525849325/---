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
        public void VictoryLoop_TimeGateDoesNotAdvanceToLockedNextStage()
        {
            var loop = new VictoryDrivenStageLoop(Catalog(), new StageProgressState(), "stage_1_1");

            var transition = loop.RecordVictory(1);

            Assert.That(transition.CompletedStageId, Is.EqualTo("stage_1_1"));
            Assert.That(transition.NextStageId, Is.EqualTo("stage_1_1"));
            Assert.That(transition.Advanced, Is.False);
            Assert.That(transition.CompletedChapter, Is.False);
            Assert.That(loop.CurrentStageId, Is.EqualTo("stage_1_1"));
        }

        [Test]
        public void VictoryLoop_VictoryAdvancesWhenNextStageIsUnlocked()
        {
            var loop = new VictoryDrivenStageLoop(Catalog(), new StageProgressState(), "stage_1_1");

            var transition = loop.RecordVictory(2);

            Assert.That(transition.ClearResult.IsFirstClear, Is.True);
            Assert.That(transition.CompletedStageId, Is.EqualTo("stage_1_1"));
            Assert.That(transition.NextStageId, Is.EqualTo("stage_1_2"));
            Assert.That(transition.Advanced, Is.True);
            Assert.That(loop.CurrentStageId, Is.EqualTo("stage_1_2"));
            Assert.That(loop.CurrentStageNumber, Is.EqualTo(2));
            Assert.That(loop.CurrentStage.Id, Is.EqualTo("stage_1_2"));
        }

        [Test]
        public void VictoryLoop_DefeatRetriesSameStageAndTracksDefeatsUntilVictory()
        {
            var loop = new VictoryDrivenStageLoop(Catalog(), new StageProgressState(), "stage_1_3");

            loop.RecordDefeat();
            loop.RecordDefeat();

            Assert.That(loop.CurrentStageId, Is.EqualTo("stage_1_3"));
            Assert.That(loop.DefeatsOnCurrentStage, Is.EqualTo(2));
            var transition = loop.RecordVictory(4);
            Assert.That(transition.NextStageId, Is.EqualTo("stage_1_4"));
            Assert.That(loop.DefeatsOnCurrentStage, Is.Zero);
        }

        [Test]
        public void VictoryLoop_MigratedStageSevenReconcilesPredecessorsBeforeSettlement()
        {
            var state = new StageProgressState();
            var loop = new VictoryDrivenStageLoop(Catalog(), state, "stage_1_7");

            Assert.That(state.ClearedStageIds, Is.EqualTo(new[]
            {
                "stage_1_1", "stage_1_2", "stage_1_3", "stage_1_4", "stage_1_5", "stage_1_6"
            }));
            var transition = loop.RecordVictory(8);
            Assert.That(transition.ClearResult.IsFirstClear, Is.True);
            Assert.That(transition.NextStageId, Is.EqualTo("stage_1_8"));
            Assert.That(loop.CurrentStageId, Is.EqualTo("stage_1_8"));
        }

        [Test]
        public void VictoryLoop_FinalBossVictoryLoopsToFirstStageRegardlessOfGate()
        {
            var loop = new VictoryDrivenStageLoop(Catalog(), new StageProgressState(), "stage_1_10");

            var transition = loop.RecordVictory(1);

            Assert.That(transition.ClearResult.IsFirstClear, Is.True);
            Assert.That(transition.CompletedStageId, Is.EqualTo("stage_1_10"));
            Assert.That(transition.NextStageId, Is.EqualTo("stage_1_1"));
            Assert.That(transition.Advanced, Is.True);
            Assert.That(transition.CompletedChapter, Is.True);
            Assert.That(loop.CurrentStageId, Is.EqualTo("stage_1_1"));
        }

        [Test]
        public void VictoryLoop_RepeatedVictoryDoesNotDuplicateFirstClearReward()
        {
            var loop = new VictoryDrivenStageLoop(Catalog(), new StageProgressState(), "stage_1_1");

            var first = loop.RecordVictory(1);
            var repeated = loop.RecordVictory(1);

            Assert.That(first.ClearResult.IsFirstClear, Is.True);
            Assert.That(first.ClearResult.FirstClearDropTableId, Is.EqualTo("drop_first_clear_1"));
            Assert.That(repeated.ClearResult.IsFirstClear, Is.False);
            Assert.That(repeated.ClearResult.FirstClearDropTableId, Is.Empty);
        }

        [Test]
        public void VictoryLoop_InvalidCurrentStageFallsBackToFirstStage()
        {
            var loop = new VictoryDrivenStageLoop(Catalog(), new StageProgressState(), "stage_missing");

            Assert.That(loop.CurrentStageId, Is.EqualTo("stage_1_1"));
            Assert.That(loop.CurrentStageNumber, Is.EqualTo(1));
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
