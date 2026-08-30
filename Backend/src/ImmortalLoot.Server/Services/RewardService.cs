using System.Text.Json;
using ImmortalLoot.Server.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ImmortalLoot.Server.Services;

public sealed record RewardPayload(long SoftCurrency = 0, long PremiumCurrency = 0, IReadOnlyDictionary<string, int>? Items = null);
public sealed record RewardResult(string IdempotencyKey, RewardPayload Payload, bool Replayed);

public sealed class RewardService(GameDbContext db, CurrencyService currencies)
{
    public async Task<RewardResult> GrantTrackedAsync(Guid playerId, string idempotencyKey, string rewardType, RewardPayload payload, CancellationToken cancellationToken)
    {
        var existing = await db.RewardGrants.AsNoTracking().SingleOrDefaultAsync(value => value.PlayerId == playerId && value.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null) return new(idempotencyKey, JsonSerializer.Deserialize<RewardPayload>(existing.PayloadJson) ?? payload, true);
        if (payload.SoftCurrency > 0) await currencies.ChangeAsync(playerId, GameCurrency.SoftCurrency, payload.SoftCurrency, rewardType, idempotencyKey, cancellationToken);
        if (payload.PremiumCurrency > 0) await currencies.ChangeAsync(playerId, GameCurrency.PremiumCurrency, payload.PremiumCurrency, rewardType, idempotencyKey, cancellationToken);
        if (payload.Items is not null)
        {
            foreach (var (itemId, count) in payload.Items)
            {
                if (count <= 0) throw new InvalidOperationException("Reward item count must be positive.");
                var item = await db.PlayerInventories.SingleOrDefaultAsync(value => value.PlayerId == playerId && value.ItemId == itemId && value.Category == "Reward", cancellationToken);
                if (item is null)
                {
                    item = new PlayerInventory { PlayerId = playerId, ItemId = itemId, Category = "Reward" };
                    db.PlayerInventories.Add(item);
                }
                item.Count = checked(item.Count + count);
                db.ItemLogs.Add(new ItemLog { PlayerId = playerId, ItemId = itemId, Delta = count, Reason = rewardType, ReferenceId = idempotencyKey });
            }
        }
        var json = JsonSerializer.Serialize(payload);
        db.RewardGrants.Add(new RewardGrant { PlayerId = playerId, IdempotencyKey = idempotencyKey, RewardType = rewardType, PayloadJson = json });
        db.RewardLogs.Add(new RewardLog { PlayerId = playerId, IdempotencyKey = idempotencyKey, RewardType = rewardType, PayloadJson = json });
        return new(idempotencyKey, payload, false);
    }
}
