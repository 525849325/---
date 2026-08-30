using System.Text.Json;
using ImmortalLoot.Server.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ImmortalLoot.Server.Services;

public sealed record BattleStartResult(Guid SessionId, string StageId, string Status, DateTime StartedAtUtc);
public sealed record BattleFinishResult(Guid SessionId, string Status, long RewardSoftCurrency, long RewardExp, string EquipmentInstanceId, bool Replayed);

public sealed class BattleAuthorityService(GameDbContext db, IServerClock clock, CurrencyService currencies, TaskService tasks, ServerEquipmentDropService equipmentDrops)
{
    public async Task<BattleStartResult> StartAsync(Guid playerId, string stageId, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stageId) || string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Stage and idempotency key are required.");
        if (!await db.Players.AnyAsync(value => value.Id == playerId, cancellationToken))
            throw new KeyNotFoundException("Player was not found.");
        var stageNumber = ParseStage(stageId);
        if (stageNumber < 1 || stageNumber > 10) throw new ArgumentException("Stage must be in chapter 1 from 1-1 through 1-10.");
        if (stageNumber > 1 && !await db.PlayerStages.AnyAsync(value => value.PlayerId == playerId && value.StageId == $"stage_1_{stageNumber - 1}" && value.Cleared, cancellationToken))
            throw new InvalidOperationException("Stage is locked.");
        var existing = await db.BattleSessions.SingleOrDefaultAsync(
            value => value.PlayerId == playerId && value.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null) return MapStart(existing);
        var session = new BattleSession
        {
            PlayerId = playerId, StageId = stageId, IdempotencyKey = idempotencyKey,
            Status = "Started", StartedAtUtc = clock.UtcNow
        };
        db.BattleSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return MapStart(session);
    }

    public Task<BattleFinishResult> FinishAsync(Guid playerId, Guid sessionId, string finishIdempotencyKey, CancellationToken cancellationToken) =>
        FinishAsync(playerId, sessionId, finishIdempotencyKey, true, cancellationToken);

    public async Task<BattleFinishResult> FinishAsync(Guid playerId, Guid sessionId, string finishIdempotencyKey, bool rewardWindowEligible, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(finishIdempotencyKey)) throw new ArgumentException("Finish idempotency key is required.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var session = await db.BattleSessions.SingleOrDefaultAsync(
            value => value.Id == sessionId && value.PlayerId == playerId, cancellationToken)
            ?? throw new KeyNotFoundException("Battle session was not found.");
        var priorGrant = await db.RewardGrants.SingleOrDefaultAsync(
            value => value.PlayerId == playerId && value.IdempotencyKey == finishIdempotencyKey, cancellationToken);
        if (priorGrant is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new BattleFinishResult(session.Id, session.Status, session.RewardSoftCurrency, session.RewardExp, session.RewardEquipmentInstanceId, true);
        }
        if (session.Status == "Finished")
        {
            await transaction.CommitAsync(cancellationToken);
            return new BattleFinishResult(session.Id, session.Status, session.RewardSoftCurrency, session.RewardExp, session.RewardEquipmentInstanceId, true);
        }

        var stageConfig = equipmentDrops.Stage(session.StageId);
        var player = await db.Players.SingleAsync(value => value.Id == playerId, cancellationToken);
        var effectivePower = Math.Max(player.Power, player.Level * 100L);
        if (effectivePower < stageConfig.RecommendedPower)
            throw new InvalidOperationException($"Player power {effectivePower} is below stage requirement {stageConfig.RecommendedPower}.");
        var grantsBattleRewards = rewardWindowEligible || stageConfig.IsBossStage;
        var reward = grantsBattleRewards ? stageConfig.RewardSoftCurrency : 0;
        var expReward = grantsBattleRewards ? stageConfig.RewardExp : 0;
        if (reward > 0)
            await currencies.ChangeAsync(playerId, GameCurrency.SoftCurrency, reward, "Battle", session.Id.ToString("N"), cancellationToken);
        if (expReward > 0)
        {
            player.Exp = checked(player.Exp + expReward);
            while (player.Exp >= player.Level * 100L)
            {
                player.Exp -= player.Level * 100L;
                player.Level++;
            }
        }
        session.Status = "Finished";
        session.FinishedAtUtc = clock.UtcNow;
        session.RewardSoftCurrency = reward;
        session.RewardExp = expReward;
        ServerEquipmentDrop? equipment = grantsBattleRewards ? equipmentDrops.GenerateTracked(playerId, session.StageId) : null;
        session.RewardEquipmentInstanceId = equipment?.InstanceId ?? string.Empty;
        var stage = await db.PlayerStages.SingleOrDefaultAsync(value => value.PlayerId == playerId && value.StageId == session.StageId, cancellationToken);
        var firstClear = stage is null || !stage.Cleared;
        if (stage is null) { stage = new PlayerStage { PlayerId = playerId, StageId = session.StageId }; db.PlayerStages.Add(stage); }
        if (!stage.Cleared) { stage.Cleared = true; stage.FirstClearTimeUtc = clock.UtcNow; }
        if (firstClear && stageConfig.FirstClearPremiumCurrency > 0)
            await currencies.ChangeAsync(playerId, GameCurrency.PremiumCurrency, stageConfig.FirstClearPremiumCurrency, "StageFirstClear", session.StageId, cancellationToken);
        var payload = JsonSerializer.Serialize(new { softCurrency = reward, exp = expReward, firstClearPremiumCurrency = firstClear ? stageConfig.FirstClearPremiumCurrency : 0, equipment });
        db.RewardGrants.Add(new RewardGrant { PlayerId = playerId, IdempotencyKey = finishIdempotencyKey, RewardType = "Battle", PayloadJson = payload });
        db.RewardLogs.Add(new RewardLog { PlayerId = playerId, IdempotencyKey = finishIdempotencyKey, RewardType = "Battle", PayloadJson = payload });
        db.BattleLogs.Add(new BattleLog { PlayerId = playerId, BattleSessionId = session.Id, StageId = session.StageId, Result = "Victory", DetailJson = payload });
        await tasks.RecordAsync(playerId, "StageClear", 1, cancellationToken);
        if (session.StageId.EndsWith("_10", StringComparison.Ordinal)) await tasks.RecordAsync(playerId, "BossVictory", 1, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new BattleFinishResult(session.Id, session.Status, reward, expReward, session.RewardEquipmentInstanceId, false);
    }

    private static BattleStartResult MapStart(BattleSession value) => new(value.Id, value.StageId, value.Status, value.StartedAtUtc);
    private static int ParseStage(string stageId)
    {
        var parts = stageId?.Split('_') ?? Array.Empty<string>();
        return parts.Length == 3 && parts[0] == "stage" && parts[1] == "1" && int.TryParse(parts[2], out var value) ? value : -1;
    }
}
