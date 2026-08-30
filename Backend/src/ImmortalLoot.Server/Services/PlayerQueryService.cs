using ImmortalLoot.Server.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ImmortalLoot.Server.Services;

public sealed record SpiritualRootProfile(string RootId, string Name, string Element, int Level, int MaxLevel);

public sealed record PlayerProfileResult(
    Guid PlayerId, string Nickname, int Level, long Exp, string RealmId, int RealmStage,
    long Power, long SoftCurrency, long PremiumCurrency, string CurrentStageId,
    IReadOnlyList<string> ClearedStageIds, string StatsJson,
    DateTime LastLoginTimeUtc, DateTime LastOfflineTimeUtc, IReadOnlyList<SpiritualRootProfile> SpiritualRoots);

public sealed record InventoryResult(
    IReadOnlyList<PlayerInventory> Items,
    IReadOnlyList<PlayerEquipment> Equipment);

public sealed class PlayerQueryService(GameDbContext db, Config.ServerGameConfigCatalog catalog)
{
    public async Task<PlayerProfileResult> GetProfileAsync(Guid playerId, CancellationToken cancellationToken)
    {
        var player = await db.Players.AsNoTracking().SingleAsync(value => value.Id == playerId, cancellationToken);
        var currency = await db.PlayerCurrencies.AsNoTracking().SingleAsync(value => value.PlayerId == playerId, cancellationToken);
        var stats = await db.PlayerStats.AsNoTracking().SingleAsync(value => value.PlayerId == playerId, cancellationToken);
        var rootLevels = await db.PlayerSpiritualRoots.AsNoTracking().Where(value => value.PlayerId == playerId).ToDictionaryAsync(value => value.RootId, value => value.Level, cancellationToken);
        var roots = catalog.SpiritualRoots.Select(value => new SpiritualRootProfile(value.Id, value.Name, value.Element, rootLevels.GetValueOrDefault(value.Id), value.MaxLevel)).ToArray();
        var persistedClears = await db.PlayerStages.AsNoTracking()
            .Where(value => value.PlayerId == playerId && value.Cleared)
            .Select(value => value.StageId)
            .ToListAsync(cancellationToken);
        var clearedSet = persistedClears.ToHashSet(StringComparer.Ordinal);
        var orderedStages = catalog.Stages.OrderBy(value => value.Chapter).ThenBy(value => value.StageNumber).ToArray();
        var clearedStageIds = orderedStages.Where(value => clearedSet.Contains(value.Id)).Select(value => value.Id).ToArray();
        var currentStageId = orderedStages.FirstOrDefault(value => !clearedSet.Contains(value.Id))?.Id
            ?? orderedStages[0].Id;
        return new PlayerProfileResult(player.Id, player.Nickname, player.Level, player.Exp, player.RealmId,
            player.RealmStage, player.Power, currency.SoftCurrency, currency.PremiumCurrency,
            currentStageId, clearedStageIds, stats.StatsJson, player.LastLoginTimeUtc, player.LastOfflineTimeUtc, roots);
    }

    public async Task<InventoryResult> GetInventoryAsync(Guid playerId, CancellationToken cancellationToken) => new(
        await db.PlayerInventories.AsNoTracking().Where(value => value.PlayerId == playerId).ToListAsync(cancellationToken),
        await db.PlayerEquipment.AsNoTracking().Where(value => value.PlayerId == playerId).ToListAsync(cancellationToken));
}
