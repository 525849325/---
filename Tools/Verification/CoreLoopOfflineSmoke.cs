using System;
using System.Collections.Generic;
using System.Globalization;
using ImmortalLoot.Config;
using ImmortalLoot.Stage;

internal static class CoreLoopOfflineSmoke
{
    private const double BossLowerBoundSeconds = 180d;
    private const double BossUpperBoundSeconds = 240d;

    private static int Main(string[] args)
    {
        try
        {
            var fixture = ParseFixture(args);
            VerifyTimeAloneAndDefeatCannotAdvance(fixture);
            VerifyRapidVictoriesRespectBossBoundary(fixture);
            VerifyBossVictoryLeavesStageTenAndLoops(fixture);
            VerifyBeyondValidationHorizonContinues(fixture);
            var tenMinutes = Simulate(fixture, 10);
            var sixtyMinutes = Simulate(fixture, 60);
            VerifyRewardWindowBand(tenMinutes, 20, 25);
            VerifyRewardWindowBand(sixtyMinutes, 130, 150);
            VerifyCycleCadence(tenMinutes, sixtyMinutes);

            Console.WriteLine("PASS: CORE-STAGE-002 offline core-loop smoke");
            Print(tenMinutes);
            Print(sixtyMinutes);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: CORE-STAGE-002 offline core-loop smoke");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void VerifyTimeAloneAndDefeatCannotAdvance(Fixture fixture)
    {
        var pacing = new DemoPacingSession(fixture.Pacing);
        var loop = fixture.CreateLoop();

        pacing.Advance(120d);
        Require(loop.CurrentStageNumber == 1,
            "advancing pacing time without a battle victory changed the stage");

        var beforeDefeat = loop.CurrentStageId;
        loop.RecordDefeat();
        Require(loop.CurrentStageId == beforeDefeat,
            "defeat changed the current stage instead of retrying it");

        var transition = loop.RecordVictory(pacing.CurrentStageNumber);
        Require(transition.Advanced && loop.CurrentStageNumber == 2,
            "an unlocked victory did not advance exactly one stage");

        var afterVictory = loop.CurrentStageId;
        pacing.Advance(30d);
        Require(loop.CurrentStageId == afterVictory,
            "time changed the stage after a victory without another battle result");

        loop.RecordDefeat();
        Require(loop.CurrentStageId == afterVictory,
            "defeat after an advance did not retry the same stage");
        Require(loop.DefeatsOnCurrentStage == 1,
            "defeat retry accounting was not retained on the current stage");
    }

    private static void VerifyRapidVictoriesRespectBossBoundary(Fixture fixture)
    {
        var bossSecond = fixture.Pacing.firstBossMinute * 60d;
        Require(bossSecond >= BossLowerBoundSeconds,
            $"configured Boss gate is earlier than 180 seconds: {bossSecond:0.###}");
        Require(bossSecond <= BossUpperBoundSeconds,
            $"configured Boss gate is later than the 4-minute validation target: {bossSecond:0.###}");

        var pacing = new DemoPacingSession(fixture.Pacing);
        var loop = fixture.CreateLoop();
        pacing.Advance(BossLowerBoundSeconds - 0.001d);

        for (var victory = 0; victory < 1000; victory++)
            loop.RecordVictory(pacing.CurrentStageNumber);

        Require(loop.CurrentStageNumber < 10,
            "rapid victories entered the Boss before 180 seconds");

        pacing.Advance(bossSecond - pacing.ElapsedSeconds);
        var guard = 20;
        while (loop.CurrentStageNumber < 10 && guard-- > 0)
            loop.RecordVictory(pacing.CurrentStageNumber);

        Require(loop.CurrentStageNumber == 10,
            "the Boss did not become reachable when the configured Boss gate opened");
    }

    private static void VerifyBossVictoryLeavesStageTenAndLoops(Fixture fixture)
    {
        var loop = fixture.CreateLoop("stage_1_10");
        var bossStage = loop.CurrentStageId;

        loop.RecordDefeat();
        Require(loop.CurrentStageId == bossStage,
            "Boss defeat did not preserve stage 10 for retry");

        var firstBossWin = loop.RecordVictory(10);
        Require(firstBossWin.CompletedChapter,
            "stage-10 victory was not marked as a chapter completion");
        Require(firstBossWin.CompletedStageId == "stage_1_10" && loop.CurrentStageNumber != 10,
            "Boss victory did not leave stage 10");
        Require(firstBossWin.CompletedCycleIndex == 1 && firstBossWin.NextCycleIndex == 2 && firstBossWin.StartedNewCycle,
            "Boss victory did not create the second persistent cycle");

        var pacing = new DemoPacingSession(fixture.Pacing);
        pacing.Restore(fixture.Pacing.firstBossMinute * 60d);
        pacing.BeginCycle(2);
        for (var victory = 0; victory < 1000; victory++)
            loop.RecordVictory(pacing.CurrentStageNumber);
        Require(loop.CurrentStageNumber == 1,
            "the reset cycle gate allowed immediate post-Boss stage spam");

        pacing.Advance(fixture.Pacing.repeatBossMinute * 60d);

        var guard = 20;
        while (loop.CurrentStageNumber < 10 && guard-- > 0)
            loop.RecordVictory(pacing.CurrentStageNumber);

        Require(loop.CurrentStageNumber == 10,
            "the loop could not reach the Boss again after a stage-10 victory");
        var secondBossWin = loop.RecordVictory(10);
        Require(secondBossWin.CompletedChapter && loop.CurrentStageNumber != 10,
            "the second Boss cycle became stuck on stage 10");
    }

    private static void VerifyBeyondValidationHorizonContinues(Fixture fixture)
    {
        var pacing = new DemoPacingSession(fixture.Pacing);
        var horizon = fixture.Pacing.durationMinutes * 60d;
        pacing.Restore(horizon, 0d, 2);
        pacing.Advance(fixture.Pacing.repeatBossMinute * 60d);

        Require(pacing.IsComplete, "the validation completion marker was lost after the horizon");
        Require(pacing.ElapsedSeconds > horizon,
            "production pacing stopped advancing at the two-hour validation horizon");
        Require(pacing.CurrentStageNumber == 10,
            "a cycle crossing the validation horizon could never reach its Boss");
        Require(pacing.GeneratedRewardWindows > 0,
            "reward cadence stopped after the validation horizon");
    }

    private static SimulationResult Simulate(Fixture fixture, int minutes)
    {
        var pacing = new DemoPacingSession(fixture.Pacing);
        var loop = fixture.CreateLoop();
        var bossEntries = 0;
        var bossVictories = 0;
        var defeats = 0;
        var transitionsAfterBoss = 0;
        var firstBossEntrySecond = -1;
        var secondBossEntrySecond = -1;
        var thirdBossEntrySecond = -1;
        var lastTransitionSecond = 0;

        for (var second = 1; second <= minutes * 60; second++)
        {
            var stageBeforeTime = loop.CurrentStageNumber;
            pacing.Advance(1d);
            Require(loop.CurrentStageNumber == stageBeforeTime,
                $"time advanced stage at second {second}");

            if (second % 17 == 0)
            {
                var beforeDefeat = loop.CurrentStageId;
                loop.RecordDefeat();
                defeats++;
                Require(loop.CurrentStageId == beforeDefeat,
                    $"defeat advanced stage at second {second}");
                continue;
            }

            var beforeVictory = loop.CurrentStageNumber;
            var transition = loop.RecordVictory(pacing.CurrentStageNumber);
            var afterVictory = loop.CurrentStageNumber;
            if (transition.Advanced)
            {
                lastTransitionSecond = second;
                if (bossEntries > 0) transitionsAfterBoss++;
            }
            if (beforeVictory != 10 && afterVictory == 10)
            {
                bossEntries++;
                if (firstBossEntrySecond < 0) firstBossEntrySecond = second;
                else if (secondBossEntrySecond < 0) secondBossEntrySecond = second;
                else if (thirdBossEntrySecond < 0) thirdBossEntrySecond = second;
            }
            if (beforeVictory == 10 && transition.CompletedChapter)
            {
                bossVictories++;
                pacing.BeginCycle(transition.NextCycleIndex);
            }

            while (pacing.TryConsumeBattleReward()) { }

            if (loop.CurrentStageNumber < pacing.CurrentStageNumber)
                Require(second - lastTransitionSecond <= 3,
                    $"stage loop stopped transitioning near second {second}");
        }

        Require(firstBossEntrySecond >= BossLowerBoundSeconds,
            $"{minutes}-minute run entered Boss too early at {firstBossEntrySecond} seconds");
        Require(bossEntries > 1 && bossVictories > 0,
            $"{minutes}-minute run did not complete repeated Boss cycles");
        Require(transitionsAfterBoss > 0,
            $"{minutes}-minute run made no progress after its first Boss entry");
        Require(loop.CurrentStageNumber >= 1 && loop.CurrentStageNumber <= 10,
            $"{minutes}-minute run ended on invalid stage {loop.CurrentStageNumber}");
        Require(pacing.PendingRewards == 0,
            $"{minutes}-minute run left reward windows permanently pending");

        var expectedRewards = ExpectedRewardWindows(fixture.Pacing, minutes * 60);
        Require(pacing.GeneratedRewardWindows == expectedRewards,
            $"{minutes}-minute generated reward count was {pacing.GeneratedRewardWindows}, expected {expectedRewards}");
        Require(pacing.ConsumedRewardWindows == pacing.GeneratedRewardWindows,
            $"{minutes}-minute run did not consume every generated reward window");

        return new SimulationResult(
            minutes,
            firstBossEntrySecond,
            secondBossEntrySecond,
            thirdBossEntrySecond,
            bossEntries,
            bossVictories,
            defeats,
            transitionsAfterBoss,
            pacing.GeneratedRewardWindows);
    }

    private static int ExpectedRewardWindows(DemoPacingConfig pacing, int elapsedSeconds)
    {
        var firstRewardSecond = pacing.firstEquipmentMinute * 60;
        if (elapsedSeconds < firstRewardSecond) return 0;
        return 1 + (elapsedSeconds - firstRewardSecond) / pacing.equipmentDropSeconds;
    }

    private static void VerifyRewardWindowBand(SimulationResult result, int minimum, int maximum)
    {
        Require(result.RewardWindows >= minimum && result.RewardWindows <= maximum,
            $"{result.Minutes}-minute reward windows {result.RewardWindows} outside {minimum}-{maximum} balance band");
    }

    private static void VerifyCycleCadence(SimulationResult tenMinutes, SimulationResult sixtyMinutes)
    {
        Require(tenMinutes.SecondBossEntrySecond >= 480 && tenMinutes.SecondBossEntrySecond <= 600,
            $"10-minute second Boss entry {tenMinutes.SecondBossEntrySecond}s missed the 8-10 minute target");
        Require(tenMinutes.ThirdBossEntrySecond < 0,
            $"10-minute run reached an unexpected third Boss at {tenMinutes.ThirdBossEntrySecond}s");
        Require(tenMinutes.BossEntries == 2 && tenMinutes.BossVictories <= 2,
            $"10-minute run produced Boss spam: entries={tenMinutes.BossEntries}, victories={tenMinutes.BossVictories}");
        Require(sixtyMinutes.BossEntries >= 2 && sixtyMinutes.BossEntries <= 12,
            $"60-minute Boss entries {sixtyMinutes.BossEntries} exceeded the configured cycle cadence");
    }

    private static Fixture ParseFixture(string[] args)
    {
        Require(args.Length >= 10,
            "expected pacing values followed by at least one configured stage descriptor");
        var pacing = new DemoPacingConfig
        {
            durationMinutes = ParseInt(args[0], "durationMinutes"),
            growthPulseMinutes = ParseInt(args[1], "growthPulseMinutes"),
            equipmentDropSeconds = ParseInt(args[2], "equipmentDropSeconds"),
            firstEquipmentMinute = ParseInt(args[3], "firstEquipmentMinute"),
            firstBossMinute = ParseInt(args[4], "firstBossMinute"),
            repeatBossMinute = ParseInt(args[5], "repeatBossMinute"),
            enemyStatGrowthPercentPerCycle = ParseInt(args[6], "enemyStatGrowthPercentPerCycle"),
            rewardGrowthPercentPerCycle = ParseInt(args[7], "rewardGrowthPercentPerCycle"),
            maxScaledCycle = ParseInt(args[8], "maxScaledCycle")
        };
        Require(pacing.durationMinutes >= 60,
            "pacing duration is shorter than the 60-minute acceptance run");
        Require(pacing.equipmentDropSeconds > 0,
            "equipmentDropSeconds must be positive");

        var stages = new Dictionary<string, StageConfig>(StringComparer.Ordinal);
        for (var index = 9; index < args.Length; index++)
        {
            var fields = args[index].Split('|');
            Require(fields.Length == 6, "invalid stage descriptor: " + args[index]);
            var stage = new StageConfig
            {
                Id = fields[0],
                Chapter = ParseInt(fields[1], "chapter"),
                StageNumber = ParseInt(fields[2], "stageNumber"),
                IsBossStage = bool.Parse(fields[3]),
                DropTableId = fields[4],
                FirstClearDropTableId = fields[5]
            };
            Require(!stages.ContainsKey(stage.Id), "duplicate stage id: " + stage.Id);
            stages.Add(stage.Id, stage);
        }

        Require(stages.Count == 10, $"expected ten configured stages, found {stages.Count}");
        for (var stageNumber = 1; stageNumber <= 10; stageNumber++)
        {
            var id = "stage_1_" + stageNumber;
            Require(stages.TryGetValue(id, out var stage), "missing configured stage " + id);
            Require(stage.Chapter == 1 && stage.StageNumber == stageNumber,
                "configured stage identity mismatch for " + id);
            Require(stage.IsBossStage == (stageNumber == 10),
                "only stage_1_10 may be the chapter Boss");
        }

        return new Fixture(pacing, new GameConfigCatalog(stages));
    }

    private static int ParseInt(string value, string field)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidOperationException($"invalid integer for {field}: {value}");
        return parsed;
    }

    private static void Print(SimulationResult result)
    {
        Console.WriteLine(
            $"{result.Minutes}m: firstBoss={result.FirstBossEntrySecond}s, secondBoss={result.SecondBossEntrySecond}s, thirdBoss={result.ThirdBossEntrySecond}s, bossEntries={result.BossEntries}, " +
            $"bossVictories={result.BossVictories}, defeats={result.Defeats}, " +
            $"postBossTransitions={result.TransitionsAfterBoss}, rewards={result.RewardWindows}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        public DemoPacingConfig Pacing { get; }
        private GameConfigCatalog Catalog { get; }

        public Fixture(DemoPacingConfig pacing, GameConfigCatalog catalog)
        {
            Pacing = pacing;
            Catalog = catalog;
        }

        public VictoryDrivenStageLoop CreateLoop(string stageId = "stage_1_1") =>
            new VictoryDrivenStageLoop(Catalog, new StageProgressState(), stageId);
    }

    private readonly struct SimulationResult
    {
        public int Minutes { get; }
        public int FirstBossEntrySecond { get; }
        public int SecondBossEntrySecond { get; }
        public int ThirdBossEntrySecond { get; }
        public int BossEntries { get; }
        public int BossVictories { get; }
        public int Defeats { get; }
        public int TransitionsAfterBoss { get; }
        public int RewardWindows { get; }

        public SimulationResult(
            int minutes,
            int firstBossEntrySecond,
            int secondBossEntrySecond,
            int thirdBossEntrySecond,
            int bossEntries,
            int bossVictories,
            int defeats,
            int transitionsAfterBoss,
            int rewardWindows)
        {
            Minutes = minutes;
            FirstBossEntrySecond = firstBossEntrySecond;
            SecondBossEntrySecond = secondBossEntrySecond;
            ThirdBossEntrySecond = thirdBossEntrySecond;
            BossEntries = bossEntries;
            BossVictories = bossVictories;
            Defeats = defeats;
            TransitionsAfterBoss = transitionsAfterBoss;
            RewardWindows = rewardWindows;
        }
    }
}

namespace ImmortalLoot.Config
{
    public sealed class GameConfigCatalog
    {
        public IReadOnlyDictionary<string, StageConfig> Stages { get; }

        public GameConfigCatalog(Dictionary<string, StageConfig> stages)
        {
            Stages = stages ?? throw new ArgumentNullException(nameof(stages));
        }
    }
}

namespace UnityEngine
{
    internal static class JsonUtility
    {
        public static T FromJson<T>(string json)
        {
            throw new NotSupportedException("The offline gate injects already-parsed JSON values.");
        }
    }
}
