using System.Security.Cryptography;
using System.Text.Json;
using ImmortalLoot.Server.Persistence;
using Microsoft.EntityFrameworkCore;
using ImmortalLoot.Server.Config;

namespace ImmortalLoot.Server.Services;

public interface IServerRandomSource
{
    bool Roll(double probability);
    int Next(int maxExclusive);
}

public sealed class CryptoServerRandomSource : IServerRandomSource
{
    public bool Roll(double probability) => RandomNumberGenerator.GetInt32(1_000_000) < (int)(Math.Clamp(probability, 0, 1) * 1_000_000);
    public int Next(int maxExclusive) => maxExclusive > 0 ? RandomNumberGenerator.GetInt32(maxExclusive) : throw new ArgumentOutOfRangeException(nameof(maxExclusive));
}

public sealed record RealmBreakthroughResult(string RealmId, int RealmStage, bool Succeeded, string SpiritualRootId, bool Replayed);

public sealed class RealmAuthorityService(GameDbContext db, CurrencyService currencies, TaskService tasks, ServerGameConfigCatalog catalog, IServerRandomSource random)
{
    private IReadOnlyList<ServerRealmConfig> Realms => catalog.Realms;

    public async Task<RealmBreakthroughResult> BreakthroughAsync(Guid playerId, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Idempotency key is required.");
        var key = "realm:" + idempotencyKey;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var prior = await db.RewardGrants.AsNoTracking().SingleOrDefaultAsync(value => value.PlayerId == playerId && value.IdempotencyKey == key, cancellationToken);
        if (prior is not null)
        {
            var replay = JsonSerializer.Deserialize<RealmBreakthroughResult>(prior.PayloadJson)!;
            await transaction.CommitAsync(cancellationToken);
            return replay with { Replayed = true };
        }
        var player = await db.Players.SingleOrDefaultAsync(value => value.Id == playerId, cancellationToken) ?? throw new KeyNotFoundException("Player was not found.");
        var index = Realms.ToList().FindIndex(value => value.Id == player.RealmId);
        if (index < 0) throw new InvalidOperationException("Player realm is invalid.");
        var config = Realms[index];
        if (player.Exp < config.RequiredExp) throw new InvalidOperationException("Insufficient experience.");
        await currencies.ChangeAsync(playerId, GameCurrency.SoftCurrency, -config.BreakthroughCost, "RealmBreakthrough", key, cancellationToken);
        var succeeded = random.Roll(config.BreakthroughSuccessRate);
        var spiritualRootId = string.Empty;
        if (succeeded)
        {
            player.Exp -= config.RequiredExp;
            if (player.RealmStage < 10) player.RealmStage++;
            else if (index + 1 < Realms.Count)
            {
                player.RealmId = Realms[index + 1].Id; player.RealmStage = 1;
                var levels = await db.PlayerSpiritualRoots.Where(value => value.PlayerId == playerId).ToDictionaryAsync(value => value.RootId, cancellationToken);
                var candidates = catalog.SpiritualRoots.Where(value => !levels.TryGetValue(value.Id, out var progress) || progress.Level < value.MaxLevel).ToArray();
                if (candidates.Length > 0) spiritualRootId = candidates[random.Next(candidates.Length)].Id;
                if (spiritualRootId.Length > 0)
                {
                var root = await db.PlayerSpiritualRoots.SingleOrDefaultAsync(value => value.PlayerId == playerId && value.RootId == spiritualRootId, cancellationToken);
                if (root is null) { root = new PlayerSpiritualRoot { PlayerId = playerId, RootId = spiritualRootId }; db.PlayerSpiritualRoots.Add(root); }
                root.Level++;
                }
                await tasks.RecordAsync(playerId, "Tribulation", 1, cancellationToken);
            }
        }
        var result = new RealmBreakthroughResult(player.RealmId, player.RealmStage, succeeded, spiritualRootId, false);
        var json = JsonSerializer.Serialize(result);
        db.RewardGrants.Add(new RewardGrant { PlayerId = playerId, IdempotencyKey = key, RewardType = "RealmBreakthrough", PayloadJson = json });
        db.RewardLogs.Add(new RewardLog { PlayerId = playerId, IdempotencyKey = key, RewardType = "RealmBreakthrough", PayloadJson = json });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
