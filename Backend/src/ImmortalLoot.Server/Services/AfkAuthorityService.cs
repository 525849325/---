using System.Text.Json;
using ImmortalLoot.Server.Config;
using ImmortalLoot.Server.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ImmortalLoot.Server.Services;

public sealed record AfkRewardPreview(long EffectiveSeconds, long Exp, long SoftCurrency, int MaterialCount, int EquipmentRolls);
public sealed record AfkClaimResult(AfkRewardPreview Reward, bool Replayed);

public sealed class AfkAuthorityService(GameDbContext db, RewardService rewards, IServerClock clock, ActivityService activities, ServerGameConfigCatalog catalog, ServerEquipmentDropService equipmentDrops)
{
    public async Task<AfkRewardPreview> PreviewAsync(Guid playerId, CancellationToken cancellationToken)
    {
        var player = await db.Players.AsNoTracking().SingleOrDefaultAsync(value => value.Id == playerId, cancellationToken) ?? throw new KeyNotFoundException("Player was not found.");
        var entitlement = await CommercialEntitlementCalculator.CalculateAsync(db, catalog, clock.UtcNow, playerId, cancellationToken);
        return Calculate(player.LastOfflineTimeUtc, entitlement.AfkCapBonusHours);
    }

    public async Task<AfkClaimResult> ClaimAsync(Guid playerId, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Idempotency key is required.");
        var key = "afk:" + idempotencyKey;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var prior = await db.RewardGrants.AsNoTracking().SingleOrDefaultAsync(value => value.PlayerId == playerId && value.IdempotencyKey == key, cancellationToken);
        if (prior is not null)
        {
            var replay = JsonSerializer.Deserialize<AfkClaimResult>(prior.PayloadJson)!;
            await transaction.CommitAsync(cancellationToken);
            return replay with { Replayed = true };
        }
        var player = await db.Players.SingleOrDefaultAsync(value => value.Id == playerId, cancellationToken) ?? throw new KeyNotFoundException("Player was not found.");
        var entitlement = await CommercialEntitlementCalculator.CalculateAsync(db, catalog, clock.UtcNow, playerId, cancellationToken);
        var preview = Calculate(player.LastOfflineTimeUtc, entitlement.AfkCapBonusHours);
        player.Exp = checked(player.Exp + preview.Exp);
        player.LastOfflineTimeUtc = clock.UtcNow;
        IReadOnlyDictionary<string, int>? items = preview.MaterialCount > 0 ? new Dictionary<string, int> { ["item_spirit_dust"] = preview.MaterialCount } : null;
        await GenerateEquipmentAsync(playerId, preview.EquipmentRolls, cancellationToken);
        await rewards.GrantTrackedAsync(playerId, key, "Afk", new RewardPayload(preview.SoftCurrency, 0, items), cancellationToken);
        var result = new AfkClaimResult(preview, false);
        var grant = db.RewardGrants.Local.Single(value => value.PlayerId == playerId && value.IdempotencyKey == key);
        grant.PayloadJson = JsonSerializer.Serialize(result);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<AfkClaimResult> ClaimQuickAsync(Guid playerId, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Idempotency key is required.");
        var date = clock.UtcNow.ToString("yyyy-MM-dd");
        var prefix = "quick-afk:" + date + ":";
        var key = prefix + idempotencyKey;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var prior = await db.RewardGrants.AsNoTracking().SingleOrDefaultAsync(value => value.PlayerId == playerId && value.IdempotencyKey == key, cancellationToken);
        if (prior is not null)
        {
            var replay = JsonSerializer.Deserialize<AfkClaimResult>(prior.PayloadJson)!;
            await transaction.CommitAsync(cancellationToken);
            return replay with { Replayed = true };
        }
        var entitlement = await CommercialEntitlementCalculator.CalculateAsync(db, catalog, clock.UtcNow, playerId, cancellationToken);
        var allowance = checked(catalog.Afk.FreeQuickAfkPerDay + Math.Max(0, entitlement.QuickAfkBonus));
        var used = await db.RewardGrants.CountAsync(value => value.PlayerId == playerId && value.RewardType == "QuickAfk" && value.IdempotencyKey.StartsWith(prefix), cancellationToken);
        if (used >= allowance) throw new InvalidOperationException("No Quick AFK claims remain today.");
        var player = await db.Players.SingleOrDefaultAsync(value => value.Id == playerId, cancellationToken) ?? throw new KeyNotFoundException("Player was not found.");
        var preview = CalculateDuration(catalog.Afk.QuickAfkHours * 60L * 60L);
        player.Exp = checked(player.Exp + preview.Exp);
        IReadOnlyDictionary<string, int>? items = preview.MaterialCount > 0 ? new Dictionary<string, int> { ["item_spirit_dust"] = preview.MaterialCount } : null;
        await GenerateEquipmentAsync(playerId, preview.EquipmentRolls, cancellationToken);
        await rewards.GrantTrackedAsync(playerId, key, "QuickAfk", new RewardPayload(preview.SoftCurrency, 0, items), cancellationToken);
        var result = new AfkClaimResult(preview, false);
        db.RewardGrants.Local.Single(value => value.PlayerId == playerId && value.IdempotencyKey == key).PayloadJson = JsonSerializer.Serialize(result);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task GenerateEquipmentAsync(Guid playerId, int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return;
        var cleared = await db.PlayerStages.AsNoTracking().Where(value => value.PlayerId == playerId && value.Cleared).Select(value => value.StageId).ToListAsync(cancellationToken);
        var stageId = cleared.OrderByDescending(StageNumber).FirstOrDefault() ?? "stage_1_1";
        for (var index = 0; index < count; index++) equipmentDrops.GenerateTracked(playerId, stageId);
    }

    private static int StageNumber(string stageId) => int.TryParse(stageId.Split('_').LastOrDefault(), out var value) ? value : 1;

    private AfkRewardPreview Calculate(DateTime lastOfflineUtc, int afkCapBonusHours)
    {
        var config = catalog.Afk;
        var maximumHours = checked(config.MaximumOfflineHours + Math.Max(0, afkCapBonusHours));
        var seconds = Math.Clamp((long)(clock.UtcNow - lastOfflineUtc).TotalSeconds, 0, maximumHours * 60L * 60L);
        return CalculateDuration(seconds);
    }

    private AfkRewardPreview CalculateDuration(long seconds)
    {
        var config = catalog.Afk;
        var minutes = seconds / 60;
        var multiplier = activities.RewardMultiplier("AfkRewardMultiplier");
        return new(seconds,
            (long)(minutes * config.ExperiencePerMinute * multiplier),
            (long)(minutes * config.SoftCurrencyPerMinute * multiplier),
            (int)(minutes * config.MaterialPerMinute * multiplier),
            (int)(minutes / config.MinutesPerEquipmentRoll * multiplier));
    }
}
