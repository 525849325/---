using System;
using System.Collections.Generic;
using System.Globalization;
using ImmortalLoot.Battle;
using ImmortalLoot.Character;
using ImmortalLoot.Config;
using ImmortalLoot.Core;
using ImmortalLoot.Equipment;
using ImmortalLoot.Stage;

internal static class RealBattlePacingSmoke
{
    private const float TickSeconds = 1f / 60f;
    private const double RespawnSeconds = 0.65d;
    private const double MaximumEncounterSeconds = 300d;
    private const int DefeatsBeforeRetreat = 3;
    private const string FirstStageId = "stage_1_1";
    private const string PlayerSkillId = "skill_ember_brand";

    private static int Main(string[] args)
    {
        try
        {
            var fixture = ParseFixture(args);
            VerifyDeterministicGrowthResistance(fixture);
            var tenMinutes = Simulate(fixture, 10, 20260830);
            var sixtyMinutes = Simulate(fixture, 60, 20260830);
            VerifyResult(tenMinutes);
            VerifyResult(sixtyMinutes);

            Console.WriteLine("PASS: BALANCE-001 real-battle pacing smoke");
            Console.WriteLine("growth-resistance: deterministic level-1 Boss defeats -> stage-9 farm recovery -> level-4 Boss victory within 120s");
            Console.WriteLine(
                "model: production battle/factory/stage/pacing; tick=1/60s; respawn=0.65s; " +
                "player=controller baseline plus configured stage-exp level growth; equipment/cultivation bonuses excluded");
            Print(tenMinutes);
            Print(sixtyMinutes);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: BALANCE-001 real-battle pacing smoke");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static SimulationResult Simulate(Fixture fixture, int minutes, int randomSeed)
    {
        var pacing = new DemoPacingSession(fixture.Pacing);
        var loop = new VictoryDrivenStageLoop(
            fixture.Catalog,
            new StageProgressState(),
            FirstStageId);
        var battles = new StageBattleFactory(
            fixture.Catalog,
            new MonsterFactory(fixture.Catalog),
            new DamageCalculator(fixture.DamageFormula, new SystemRandomSource(randomSeed)));
        var progression = new BaselineProgression();
        var horizonSeconds = minutes * 60d;
        var elapsedSeconds = 0d;
        var nextSpawnSecond = 0d;
        var encounterStartedSecond = 0d;
        var activeStageId = string.Empty;
        var activeStageWasBoss = false;
        var finishedState = default(BattleState?);
        AutoBattleEngine battle = null;

        var encounterCount = 0;
        var victories = 0;
        var defeats = 0;
        var bossEntries = 0;
        var bossVictories = 0;
        var postBossTransitions = 0;
        var firstBossReachedSecond = -1d;
        var firstBossVictorySecond = -1d;
        var firstBossPlayerLevel = 0;
        var maximumEncounterDuration = 0d;
        var lastSettlementSecond = 0d;
        var lastTransitionSecond = 0d;

        while (elapsedSeconds < horizonSeconds)
        {
            if (battle == null && elapsedSeconds + 0.000001d >= nextSpawnSecond)
            {
                var stage = loop.CurrentStage;
                activeStageId = stage.Id;
                activeStageWasBoss = stage.IsBossStage;
                encounterStartedSecond = elapsedSeconds;
                finishedState = null;
                battle = battles.Create(stage.Id, CreatePlayer(fixture.Catalog, progression.Level));
                battle.SuppressPresentationEvents = true;
                battle.Finished += state =>
                {
                    if (finishedState.HasValue)
                        throw new InvalidOperationException("Finished fired more than once for one encounter.");
                    finishedState = state;
                };
                encounterCount++;
                if (activeStageWasBoss)
                {
                    bossEntries++;
                    if (firstBossReachedSecond < 0d)
                    {
                        firstBossReachedSecond = elapsedSeconds;
                        firstBossPlayerLevel = progression.Level;
                    }
                }
            }

            var step = Math.Min(TickSeconds, horizonSeconds - elapsedSeconds);
            pacing.Advance(step);
            battle?.Tick((float)step);
            elapsedSeconds += step;

            if (battle != null && elapsedSeconds - encounterStartedSecond > MaximumEncounterSeconds)
                throw new InvalidOperationException(
                    $"Encounter '{activeStageId}' remained running for more than {MaximumEncounterSeconds:0} seconds.");

            if (!finishedState.HasValue) continue;

            var encounterDuration = elapsedSeconds - encounterStartedSecond;
            maximumEncounterDuration = Math.Max(maximumEncounterDuration, encounterDuration);
            lastSettlementSecond = elapsedSeconds;
            if (finishedState.Value == BattleState.Victory)
            {
                var completed = fixture.Catalog.Stages[activeStageId];
                var hadBossVictory = bossVictories > 0;
                var transition = loop.RecordVictory(pacing.CurrentStageNumber);
                victories++;
                if (activeStageWasBoss)
                {
                    bossVictories++;
                    if (firstBossVictorySecond < 0d) firstBossVictorySecond = elapsedSeconds;
                }
                if (transition.Advanced)
                {
                    lastTransitionSecond = elapsedSeconds;
                    if (hadBossVictory) postBossTransitions++;
                }

                var consumedRewardWindow = pacing.TryConsumeBattleReward();
                if (completed.IsBossStage || consumedRewardWindow)
                    progression.GrantExperience(completed.RewardExp);
            }
            else if (finishedState.Value == BattleState.Defeat)
            {
                loop.RecordDefeatAndMaybeRetreat(DefeatsBeforeRetreat);
                defeats++;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Encounter '{activeStageId}' finished in unexpected state {finishedState.Value}.");
            }

            battle = null;
            finishedState = null;
            nextSpawnSecond = elapsedSeconds + RespawnSeconds;
        }

        var runningEncounterDuration = battle == null ? 0d : elapsedSeconds - encounterStartedSecond;
        var postBossProgressStalled = bossVictories > 0 &&
                                      (postBossTransitions == 0 ||
                                       elapsedSeconds - lastTransitionSecond > MaximumEncounterSeconds + RespawnSeconds);
        var stuck = runningEncounterDuration > MaximumEncounterSeconds ||
                    encounterCount == 0 ||
                    lastSettlementSecond <= 0d ||
                    postBossProgressStalled;
        return new SimulationResult(
            minutes,
            firstBossReachedSecond,
            firstBossVictorySecond,
            firstBossPlayerLevel,
            bossEntries,
            bossVictories,
            postBossTransitions,
            encounterCount,
            victories,
            defeats,
            pacing.GeneratedRewardWindows,
            pacing.ConsumedRewardWindows,
            pacing.PendingRewards,
            progression.Level,
            maximumEncounterDuration,
            loop.CurrentStageId,
            battle != null,
            stuck);
    }

    private static void VerifyDeterministicGrowthResistance(Fixture fixture)
    {
        var battles = new StageBattleFactory(
            fixture.Catalog,
            new MonsterFactory(fixture.Catalog),
            new DamageCalculator(fixture.DamageFormula, new NoCriticalRandomSource()));
        var loop = new VictoryDrivenStageLoop(fixture.Catalog, new StageProgressState(), "stage_1_10");
        var pacing = new DemoPacingSession(fixture.Pacing);
        pacing.Restore(180d);
        var progression = new BaselineProgression();
        var bossDefeats = 0;
        var retreats = 0;
        var farmVictories = 0;
        var bossVictory = false;
        var bossVictoryStartLevel = 0;
        var bossVictoryRemainingHp = 0f;
        var encountersUsed = 0;
        var recoveryStartedSecond = pacing.ElapsedSeconds;

        var baseline = CreatePlayer(fixture.Catalog, progression.Level);
        Require(Math.Abs(baseline.MaxHp - 180f) < 0.001f && baseline.MaxHp < 9999f,
            "the deterministic baseline player did not use the controller's real level-1 HP");

        for (var encounter = 0; encounter < 20 && !bossVictory; encounter++)
        {
            encountersUsed++;
            var stage = loop.CurrentStage;
            var battle = battles.Create(stage.Id, CreatePlayer(fixture.Catalog, progression.Level));
            battle.SuppressPresentationEvents = true;
            var result = ResolveWithProductionPacing(battle, pacing);
            if (result == BattleState.Defeat)
            {
                Require(stage.IsBossStage, "the recovery farm stage was not beatable by the baseline player");
                bossDefeats++;
                if (loop.RecordDefeatAndMaybeRetreat(DefeatsBeforeRetreat)) retreats++;
                continue;
            }

            Require(result == BattleState.Victory, "the recovery encounter did not settle");
            var transition = loop.RecordVictory(pacing.CurrentStageNumber);
            var victoryStartLevel = progression.Level;
            var consumedRewardWindow = pacing.TryConsumeBattleReward();
            if (stage.IsBossStage || consumedRewardWindow)
                progression.GrantExperience(stage.RewardExp);
            if (stage.IsBossStage)
            {
                bossVictory = true;
                bossVictoryStartLevel = victoryStartLevel;
                bossVictoryRemainingHp = battle.Player.Hp;
                Require(transition.Advanced && transition.CompletedChapter && loop.CurrentStageId == FirstStageId,
                    "the recovered Boss victory did not loop the chapter");
            }
            else
            {
                farmVictories++;
                Require(loop.CurrentStageId == "stage_1_10",
                    "the recovery farm victory did not return to the blocked Boss");
            }
        }

        Require(bossDefeats == 6 && retreats == 2 && farmVictories == 2 && encountersUsed == 9,
            "an under-level migrated Boss state did not exercise the bounded retreat/farm recovery path");
        Require(bossVictory && bossVictoryStartLevel == 4 && progression.Level == 5 && bossVictoryRemainingHp > 0f,
            "the bounded recovery path did not turn real stage experience into a survivable level-4 Boss victory");
        Require(pacing.ElapsedSeconds - recoveryStartedSecond <= 120d,
            "the deterministic migrated-save recovery exceeded the two-minute liveness guardrail");
        var grown = CreatePlayer(fixture.Catalog, bossVictoryStartLevel);
        Require(Math.Abs(grown.MaxHp - 225f) < 0.001f && grown.MaxHp < 9999f,
            "the recovered Boss attempt did not use the controller's real level-4 HP");
    }

    private static BattleState ResolveWithProductionPacing(AutoBattleEngine battle, DemoPacingSession pacing)
    {
        BattleState? finished = null;
        battle.Finished += state => finished = state;
        var elapsed = 0d;
        while (!finished.HasValue && elapsed <= MaximumEncounterSeconds)
        {
            battle.Tick(TickSeconds);
            pacing.Advance(TickSeconds);
            elapsed += TickSeconds;
        }
        Require(finished.HasValue, "a deterministic recovery encounter exceeded the production timeout");
        pacing.Advance(RespawnSeconds);
        return finished.Value;
    }

    private static BattleActor CreatePlayer(GameConfigCatalog catalog, int level)
    {
        var levelOffset = Math.Max(0, level - 1);
        var stats = new CharacterStats
        {
            HP = 180f + levelOffset * 15f,
            Attack = 12f + levelOffset * 2f,
            Defense = 3f,
            CritRate = 0.1f,
            CritDamage = 1.5f,
            AttackSpeed = 1f,
            FireDamage = 0.1f
        };
        return new BattleActor("player", stats, 0.7f, new[] { catalog.Skills[PlayerSkillId] });
    }

    private static void VerifyResult(SimulationResult result)
    {
        Require(!result.Stuck, $"{result.Minutes}-minute simulation became stuck");
        Require(result.FirstBossReachedSecond >= 180d,
            $"{result.Minutes}-minute simulation reached the Boss before the 180-second gate");
        Require(result.FirstBossReachedSecond <= 240d,
            $"{result.Minutes}-minute simulation missed the 4-minute Boss reach target");
        Require(result.FirstBossPlayerLevel == 5,
            $"{result.Minutes}-minute simulation reached its first Boss at unexpected level {result.FirstBossPlayerLevel}");
        Require(result.FirstBossVictorySecond >= result.FirstBossReachedSecond,
            $"{result.Minutes}-minute simulation did not defeat the first Boss");
        Require(result.FirstBossVictorySecond <= 240d,
            $"{result.Minutes}-minute simulation missed the 4-minute first-Boss defeat guardrail");
        Require(result.BossVictories > 1,
            $"{result.Minutes}-minute simulation did not complete a second Boss cycle");
        Require(result.Defeats <= 2,
            $"{result.Minutes}-minute simulation accumulated {result.Defeats} defeats and regressed into retry churn");
        Require(result.PostBossTransitions > 0,
            $"{result.Minutes}-minute simulation made no stage transition after its first Boss victory");
        var expectedRewardWindows = result.Minutes == 10 ? 22 : 142;
        Require(result.GeneratedRewardWindows == expectedRewardWindows,
            $"{result.Minutes}-minute simulation generated {result.GeneratedRewardWindows} reward windows instead of {expectedRewardWindows}");
        Require(result.ConsumedRewardWindows == result.GeneratedRewardWindows && result.PendingRewardWindows == 0,
            $"{result.Minutes}-minute reward-window accounting did not drain exactly");
        Require(result.MaximumEncounterDuration <= MaximumEncounterSeconds,
            $"{result.Minutes}-minute simulation exceeded the encounter timeout");
    }

    private static Fixture ParseFixture(string[] args)
    {
        Require(args != null && args.Length >= 10,
            "Expected pacing/formula values followed by production config descriptors.");
        var pacing = new DemoPacingConfig
        {
            schemaVersion = 1,
            durationMinutes = ParseInt(args[0], "durationMinutes"),
            growthPulseMinutes = ParseInt(args[1], "growthPulseMinutes"),
            equipmentDropSeconds = ParseInt(args[2], "equipmentDropSeconds"),
            firstEquipmentMinute = ParseInt(args[3], "firstEquipmentMinute"),
            firstBossMinute = ParseInt(args[4], "firstBossMinute")
        };
        var damageFormula = new DamageFormulaConfig
        {
            DefenseConstant = ParseFloat(args[5], "defenseConstant"),
            MinimumDamage = ParseFloat(args[6], "minimumDamage"),
            MaximumDamageReduction = ParseFloat(args[7], "maximumDamageReduction"),
            MaximumElementResistance = ParseFloat(args[8], "maximumElementResistance")
        };
        var skills = new Dictionary<string, SkillConfig>(StringComparer.Ordinal);
        var monsters = new Dictionary<string, MonsterConfig>(StringComparer.Ordinal);
        var stages = new Dictionary<string, StageConfig>(StringComparer.Ordinal);

        for (var index = 9; index < args.Length; index++)
        {
            var fields = args[index].Split('|');
            switch (fields[0])
            {
                case "skill":
                    Require(fields.Length == 10, "Invalid skill descriptor: " + args[index]);
                    Add(skills, new SkillConfig
                    {
                        Id = fields[1],
                        Name = fields[1],
                        Element = EnumValue<ElementType>(fields[2], "skill element"),
                        Type = EnumValue<SkillType>(fields[3], "skill type"),
                        Cooldown = ParseFloat(fields[4], "skill cooldown"),
                        Multiplier = ParseFloat(fields[5], "skill multiplier"),
                        TargetType = EnumValue<SkillTargetType>(fields[6], "skill target"),
                        EffectType = EnumValue<SkillEffectType>(fields[7], "skill effect"),
                        EffectValue = ParseFloat(fields[8], "skill effect value"),
                        Duration = ParseFloat(fields[9], "skill duration"),
                        UnlockRequirement = string.Empty
                    }, "skill");
                    break;
                case "monster":
                    Require(fields.Length == 10, "Invalid monster descriptor: " + args[index]);
                    Add(monsters, new MonsterConfig
                    {
                        Id = fields[1],
                        Name = fields[1],
                        Rank = EnumValue<MonsterRank>(fields[2], "monster rank"),
                        MaxHp = ParseFloat(fields[3], "monster maxHp"),
                        Attack = ParseFloat(fields[4], "monster attack"),
                        Defense = ParseFloat(fields[5], "monster defense"),
                        AttackInterval = ParseFloat(fields[6], "monster attack interval"),
                        EnrageSeconds = ParseFloat(fields[7], "monster enrage seconds"),
                        DropTableId = fields[8],
                        SkillIds = SplitIds(fields[9])
                    }, "monster");
                    break;
                case "stage":
                    Require(fields.Length == 11, "Invalid stage descriptor: " + args[index]);
                    Add(stages, new StageConfig
                    {
                        Id = fields[1],
                        Name = fields[1],
                        Chapter = ParseInt(fields[2], "stage chapter"),
                        StageNumber = ParseInt(fields[3], "stage number"),
                        IsBossStage = bool.Parse(fields[4]),
                        RewardExp = ParseLong(fields[5], "stage rewardExp"),
                        RewardSoftCurrency = ParseLong(fields[6], "stage rewardSoftCurrency"),
                        DropTableId = fields[7],
                        FirstClearDropTableId = fields[8],
                        AfkRewardRate = ParseFloat(fields[9], "stage afk reward rate"),
                        MonsterGroup = SplitIds(fields[10])
                    }, "stage");
                    break;
                default:
                    throw new InvalidOperationException("Unknown descriptor type: " + fields[0]);
            }
        }

        Require(pacing.durationMinutes >= 60, "Pacing duration is shorter than the 60-minute run.");
        Require(pacing.equipmentDropSeconds > 0, "Equipment reward cadence must be positive.");
        Require(skills.ContainsKey(PlayerSkillId), $"Player skill '{PlayerSkillId}' is missing.");
        Require(stages.ContainsKey(FirstStageId), $"First stage '{FirstStageId}' is missing.");
        Require(stages.Count == 10, $"Expected the frozen ten-stage chapter, found {stages.Count} stages.");
        foreach (var monster in monsters.Values)
            foreach (var skillId in monster.SkillIds)
                Require(skills.ContainsKey(skillId), $"Monster '{monster.Id}' references missing skill '{skillId}'.");
        foreach (var stage in stages.Values)
            foreach (var monsterId in stage.MonsterGroup)
                Require(monsters.ContainsKey(monsterId), $"Stage '{stage.Id}' references missing monster '{monsterId}'.");

        var catalog = new GameConfigCatalog(
            new Dictionary<string, EquipmentDefinition>(StringComparer.Ordinal),
            new Dictionary<EquipmentQuality, AffixCountRange>(),
            skills: skills,
            monsters: monsters,
            stages: stages);
        return new Fixture(pacing, damageFormula, catalog);
    }

    private static void Add<T>(Dictionary<string, T> values, T value, string label) where T : class
    {
        string id;
        if (value is SkillConfig skill) id = skill.Id;
        else if (value is MonsterConfig monster) id = monster.Id;
        else if (value is StageConfig stage) id = stage.Id;
        else throw new InvalidOperationException("Unsupported config type " + typeof(T).Name);
        Require(!string.IsNullOrWhiteSpace(id), $"{label} id is empty.");
        Require(!values.ContainsKey(id), $"Duplicate {label} id '{id}'.");
        values.Add(id, value);
    }

    private static string[] SplitIds(string value) =>
        string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : value.Split(',');

    private static T EnumValue<T>(string value, string field) where T : struct
    {
        if (!Enum.TryParse(value, true, out T parsed))
            throw new InvalidOperationException($"Invalid {field}: {value}");
        return parsed;
    }

    private static int ParseInt(string value, string field)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidOperationException($"Invalid integer for {field}: {value}");
        return parsed;
    }

    private static long ParseLong(string value, string field)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidOperationException($"Invalid integer for {field}: {value}");
        return parsed;
    }

    private static float ParseFloat(string value, string field)
    {
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidOperationException($"Invalid number for {field}: {value}");
        return parsed;
    }

    private static void Print(SimulationResult result)
    {
        Console.WriteLine(
            $"{result.Minutes}m: firstBossReached={result.FirstBossReachedSecond:0.00}s, " +
            $"firstBossDefeated={result.FirstBossVictorySecond:0.00}s, firstBossLevel={result.FirstBossPlayerLevel}, bossEntries={result.BossEntries}, " +
            $"bossVictories={result.BossVictories}, postBossTransitions={result.PostBossTransitions}, " +
            $"encounters={result.EncounterCount}, " +
            $"victories={result.Victories}, defeats={result.Defeats}, " +
            $"rewards={result.ConsumedRewardWindows}/{result.GeneratedRewardWindows} pending={result.PendingRewardWindows}, " +
            $"level={result.FinalLevel}, maxEncounter={result.MaximumEncounterDuration:0.00}s, " +
            $"current={result.CurrentStageId}, inFlight={result.EncounterInFlight}, stuck={result.Stuck}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class BaselineProgression
    {
        private long _experience;
        public int Level { get; private set; } = 1;

        public void GrantExperience(long amount)
        {
            _experience += Math.Max(0L, amount);
            while (_experience >= Level * 50L)
            {
                _experience -= Level * 50L;
                Level++;
            }
        }
    }

    private sealed class NoCriticalRandomSource : IRandomSource
    {
        public int Range(int minInclusive, int maxExclusive) => minInclusive;
        public float Value() => 1f;
    }

    private sealed class Fixture
    {
        public DemoPacingConfig Pacing { get; }
        public DamageFormulaConfig DamageFormula { get; }
        public GameConfigCatalog Catalog { get; }

        public Fixture(DemoPacingConfig pacing, DamageFormulaConfig damageFormula, GameConfigCatalog catalog)
        {
            Pacing = pacing;
            DamageFormula = damageFormula;
            Catalog = catalog;
        }
    }

    private readonly struct SimulationResult
    {
        public int Minutes { get; }
        public double FirstBossReachedSecond { get; }
        public double FirstBossVictorySecond { get; }
        public int FirstBossPlayerLevel { get; }
        public int BossEntries { get; }
        public int BossVictories { get; }
        public int PostBossTransitions { get; }
        public int EncounterCount { get; }
        public int Victories { get; }
        public int Defeats { get; }
        public int GeneratedRewardWindows { get; }
        public int ConsumedRewardWindows { get; }
        public int PendingRewardWindows { get; }
        public int FinalLevel { get; }
        public double MaximumEncounterDuration { get; }
        public string CurrentStageId { get; }
        public bool EncounterInFlight { get; }
        public bool Stuck { get; }

        public SimulationResult(
            int minutes,
            double firstBossReachedSecond,
            double firstBossVictorySecond,
            int firstBossPlayerLevel,
            int bossEntries,
            int bossVictories,
            int postBossTransitions,
            int encounterCount,
            int victories,
            int defeats,
            int generatedRewardWindows,
            int consumedRewardWindows,
            int pendingRewardWindows,
            int finalLevel,
            double maximumEncounterDuration,
            string currentStageId,
            bool encounterInFlight,
            bool stuck)
        {
            Minutes = minutes;
            FirstBossReachedSecond = firstBossReachedSecond;
            FirstBossVictorySecond = firstBossVictorySecond;
            FirstBossPlayerLevel = firstBossPlayerLevel;
            BossEntries = bossEntries;
            BossVictories = bossVictories;
            PostBossTransitions = postBossTransitions;
            EncounterCount = encounterCount;
            Victories = victories;
            Defeats = defeats;
            GeneratedRewardWindows = generatedRewardWindows;
            ConsumedRewardWindows = consumedRewardWindows;
            PendingRewardWindows = pendingRewardWindows;
            FinalLevel = finalLevel;
            MaximumEncounterDuration = maximumEncounterDuration;
            CurrentStageId = currentStageId;
            EncounterInFlight = encounterInFlight;
            Stuck = stuck;
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
