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
        public void VictoryLoop_ThirdDefeatRetreatsToFarmablePredecessorAndVictoryReturnsToBoss()
        {
            var loop = new VictoryDrivenStageLoop(Catalog(), new StageProgressState(), "stage_1_10");

            Assert.That(loop.RecordDefeatAndMaybeRetreat(3), Is.False);
            Assert.That(loop.RecordDefeatAndMaybeRetreat(3), Is.False);
            Assert.That(loop.CurrentStageId, Is.EqualTo("stage_1_10"));
            Assert.That(loop.DefeatsOnCurrentStage, Is.EqualTo(2));

            Assert.That(loop.RecordDefeatAndMaybeRetreat(3), Is.True);
            Assert.That(loop.CurrentStageId, Is.EqualTo("stage_1_9"));
            Assert.That(loop.DefeatsOnCurrentStage, Is.Zero);

            var recovery = loop.RecordVictory(10);
            Assert.That(recovery.NextStageId, Is.EqualTo("stage_1_10"));
            Assert.That(loop.CurrentStageId, Is.EqualTo("stage_1_10"));
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
        public void VictoryLoop_FinalBossStartsCycleTwoAtFirstStageAndPreservesFirstClearHistory()
        {
            var state = new StageProgressState
            {
                CycleIndex = 1,
                CycleElapsedSeconds = 123d,
                ClearedStageIds = new System.Collections.Generic.List<string>
                {
                    "stage_1_1", "stage_1_2", "stage_1_3", "stage_1_4", "stage_1_5",
                    "stage_1_6", "stage_1_7", "stage_1_8", "stage_1_9"
                }
            };
            var loop = new VictoryDrivenStageLoop(Catalog(), state, "stage_1_10");

            var transition = loop.RecordVictory(1);

            Assert.That(transition.ClearResult.IsFirstClear, Is.True);
            Assert.That(transition.CompletedStageId, Is.EqualTo("stage_1_10"));
            Assert.That(transition.NextStageId, Is.EqualTo("stage_1_1"));
            Assert.That(transition.Advanced, Is.True);
            Assert.That(transition.CompletedChapter, Is.True);
            Assert.That(transition.CompletedCycleIndex, Is.EqualTo(1));
            Assert.That(transition.NextCycleIndex, Is.EqualTo(2));
            Assert.That(transition.StartedNewCycle, Is.True);
            Assert.That(loop.CurrentStageId, Is.EqualTo("stage_1_1"));
            Assert.That(loop.CurrentCycleIndex, Is.EqualTo(2));
            Assert.That(state.CycleIndex, Is.EqualTo(2));
            Assert.That(state.CycleElapsedSeconds, Is.Zero);
            Assert.That(state.ClearedStageIds, Has.Count.EqualTo(10));
        }

        [Test]
        public void VictoryLoop_CycleTwoRespectsResetGateAndDoesNotRepeatFirstClearRewards()
        {
            var state = new StageProgressState
            {
                CycleIndex = 2,
                ClearedStageIds = new System.Collections.Generic.List<string>
                {
                    "stage_1_1", "stage_1_2", "stage_1_3", "stage_1_4", "stage_1_5",
                    "stage_1_6", "stage_1_7", "stage_1_8", "stage_1_9", "stage_1_10"
                }
            };
            var loop = new VictoryDrivenStageLoop(Catalog(), state, "stage_1_1");

            var gated = loop.RecordVictory(1);

            Assert.That(gated.Advanced, Is.False);
            Assert.That(gated.ClearResult.IsFirstClear, Is.False);
            Assert.That(gated.ClearResult.FirstClearDropTableId, Is.Empty);
            Assert.That(loop.CurrentStageId, Is.EqualTo("stage_1_1"));
            Assert.That(loop.CurrentCycleIndex, Is.EqualTo(2));
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

        [Test]
        public void CycleScaling_GrowsEnemyAndRewardsWithoutMutatingCatalogAndCapsSafely()
        {
            var catalog = Catalog();
            var pacing = DemoPacingLoader.Load(new ResourcesConfigSource());
            var policy = new CycleScalingPolicy(pacing);
            var factory = new StageBattleFactory(
                catalog,
                new MonsterFactory(catalog),
                new DamageCalculator(DamageFormulaConfigLoader.Load(new ResourcesConfigSource()), new DamageCalculatorTests.FixedRandom(0.99f)),
                policy);
            var baselineHp = catalog.Monsters["monster_wasteland_beast"].MaxHp;
            var player = new BattleActor("player", new CharacterStats { HP = 1000, Attack = 100, CritDamage = 1.5f }, 1f);

            var firstCycle = factory.Create("stage_1_1", player, 1);
            var secondCycle = factory.Create("stage_1_1", player, 2);

            Assert.That(firstCycle.Enemy.MaxHp, Is.EqualTo(baselineHp).Within(0.001f));
            Assert.That(secondCycle.Enemy.MaxHp,
                Is.EqualTo(baselineHp * (1f + pacing.enemyStatGrowthPercentPerCycle / 100f)).Within(0.001f));
            Assert.That(catalog.Monsters["monster_wasteland_beast"].MaxHp, Is.EqualTo(baselineHp));
            Assert.That(policy.ScaleReward(100, 1), Is.EqualTo(100));
            Assert.That(policy.ScaleReward(100, 2), Is.EqualTo(100 + pacing.rewardGrowthPercentPerCycle));
            Assert.That(policy.ScaleReward(-1, 2), Is.Zero);
            Assert.That(policy.ScaleReward(long.MaxValue, int.MaxValue), Is.EqualTo(long.MaxValue));
            Assert.That(policy.EnemyMultiplier(int.MaxValue),
                Is.EqualTo(1f + (pacing.maxScaledCycle - 1) * pacing.enemyStatGrowthPercentPerCycle / 100f).Within(0.001f));
        }

        [Test]
        public void StageBattleFactory_RejectsLaterCycleWhenScalingPolicyWasNotProvided()
        {
            var catalog = Catalog();
            var factory = new StageBattleFactory(
                catalog,
                new MonsterFactory(catalog),
                new DamageCalculator(DamageFormulaConfigLoader.Load(new ResourcesConfigSource()), new DamageCalculatorTests.FixedRandom(0.99f)));
            var player = new BattleActor("player", new CharacterStats { HP = 1000, Attack = 100, CritDamage = 1.5f }, 1f);

            Assert.That(() => factory.Create("stage_1_1", player, 2),
                Throws.InvalidOperationException.With.Message.Contains("scaling policy"));
        }

        private static GameConfigCatalog Catalog() => new JsonConfigRepository(new ResourcesConfigSource()).LoadAll();
    }
}
