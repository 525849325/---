using System.Text.Json;
using ImmortalLoot.Server.Config;
using ImmortalLoot.Server.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ImmortalLoot.Server.Services;

public sealed record BattleStartResult(Guid SessionId, string StageId, string Status, DateTime StartedAtUtc);
public sealed record BattleFinishResult(Guid SessionId, string Status, long RewardSoftCurrency, long RewardExp, string EquipmentInstanceId, bool Replayed);

public sealed class BattleAuthorityService(GameDbContext db, IServerClock clock, CurrencyService currencies, TaskService tasks, ServerEquipmentDropService equipmentDrops, ServerGameConfigCatalog catalog)
{
    public async Task<BattleStartResult> StartAsync(Guid playerId, string stageId, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stageId) || string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Stage and idempotency key are required.");
        if (!await db.Players.AnyAsync(value => value.Id == playerId, cancellationToken))
            throw new KeyNotFoundException("Player was not found.");
        if (!catalog.Stages.Any(value => string.Equals(value.Id, stageId, StringComparison.Ordinal)))
            throw new ArgumentException("Stage must be a configured canonical chapter-1 stage.");
        var existing = await db.BattleSessions.SingleOrDefaultAsync(
            value => value.PlayerId == playerId && value.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null) return MapIdempotentStart(existing, stageId);
        var clearedStageIds = await db.PlayerStages.AsNoTracking()
            .Where(value => value.PlayerId == playerId && value.Cleared)
            .Select(value => value.StageId)
            .ToListAsync(cancellationToken);
        var stageSnapshot = AuthoritativeStageProgression.Resolve(catalog.Stages, clearedStageIds);
        var activeSession = await db.BattleSessions.SingleOrDefaultAsync(
            value => value.PlayerId == playerId && value.Status == "Started", cancellationToken);
        if (activeSession is not null)
        {
            if (!string.Equals(activeSession.StageId, stageSnapshot.CurrentStageId, StringComparison.Ordinal))
            {
                activeSession.Status = "Invalidated";
                activeSession.FinishedAtUtc = clock.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
            else if (string.Equals(activeSession.StageId, stageId, StringComparison.Ordinal))
            {
                return MapStart(activeSession);
            }
            else
            {
                throw new InvalidOperationException("Player already has an active battle session for another stage.");
            }
        }
        if (!string.Equals(stageId, stageSnapshot.CurrentStageId, StringComparison.Ordinal))
            throw new InvalidOperationException("Stage is not the player's authoritative current stage.");
        var session = new BattleSession
        {
            PlayerId = playerId, StageId = stageId, IdempotencyKey = idempotencyKey,
            Status = "Started", StartedAtUtc = clock.UtcNow
        };
        db.BattleSessions.Add(session);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.Entry(session).State = EntityState.Detached;
            var concurrentReplay = await db.BattleSessions.AsNoTracking().SingleOrDefaultAsync(
                value => value.PlayerId == playerId && value.IdempotencyKey == idempotencyKey, cancellationToken);
            if (concurrentReplay is not null) return MapIdempotentStart(concurrentReplay, stageId);
            var concurrentActive = await db.BattleSessions.AsNoTracking().SingleOrDefaultAsync(
                value => value.PlayerId == playerId && value.Status == "Started", cancellationToken);
            if (concurrentActive is not null &&
                string.Equals(concurrentActive.StageId, stageId, StringComparison.Ordinal) &&
                await IsAuthoritativeCurrentStageAsync(playerId, stageId, cancellationToken))
                return MapStart(concurrentActive);
            if (concurrentActive is not null)
                throw new InvalidOperationException("Player already has an active battle session for another stage.");
            throw;
        }
        if (!await IsAuthoritativeCurrentStageAsync(playerId, stageId, cancellationToken))
        {
            session.Status = "Invalidated";
            session.FinishedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Stage progression changed while the battle was starting.");
        }
        return MapStart(session);
    }

    public Task<BattleFinishResult> FinishAsync(Guid playerId, Guid sessionId, string finishIdempotencyKey, CancellationToken cancellationToken) =>
        FinishAsync(playerId, sessionId, finishIdempotencyKey, false, cancellationToken);

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
        if (session.Status != "Started")
            throw new InvalidOperationException("Battle session is not active.");
        if (!await IsAuthoritativeCurrentStageAsync(playerId, session.StageId, cancellationToken))
        {
            session.Status = "Invalidated";
            session.FinishedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new InvalidOperationException("Battle session no longer matches the player's authoritative current stage.");
        }

        var stageConfig = equipmentDrops.Stage(session.StageId);
        var player = await db.Players.SingleAsync(value => value.Id == playerId, cancellationToken);
        var effectivePower = Math.Max(player.Power, player.Level * 100L);
        if (effectivePower < stageConfig.RecommendedPower)
            throw new InvalidOperationException($"Player power {effectivePower} is below stage requirement {stageConfig.RecommendedPower}.");
        // Reward-window timing is not yet persisted server-side. Never trust the compatibility
        // flag from an untrusted client; only the authoritative Boss classification can grant here.
        _ = rewardWindowEligible;
        var grantsBattleRewards = stageConfig.IsBossStage;
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

    private static BattleStartResult MapIdempotentStart(BattleSession existing, string requestedStageId)
    {
        if (!string.Equals(existing.StageId, requestedStageId, StringComparison.Ordinal))
            throw new InvalidOperationException("Battle start idempotency key was already used for a different stage.");
        if (existing.Status == "Invalidated")
            throw new InvalidOperationException("Battle start was invalidated by an authoritative progression change.");
        return MapStart(existing);
    }

    private async Task<bool> IsAuthoritativeCurrentStageAsync(Guid playerId, string stageId, CancellationToken cancellationToken)
    {
        var clearedStageIds = await db.PlayerStages.AsNoTracking()
            .Where(value => value.PlayerId == playerId && value.Cleared)
            .Select(value => value.StageId)
            .ToListAsync(cancellationToken);
        var stageSnapshot = AuthoritativeStageProgression.Resolve(catalog.Stages, clearedStageIds);
        return string.Equals(stageId, stageSnapshot.CurrentStageId, StringComparison.Ordinal);
    }

    private static BattleStartResult MapStart(BattleSession value) => new(value.Id, value.StageId, value.Status, value.StartedAtUtc);
}
