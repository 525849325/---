using ImmortalLoot.Server.Persistence;
using Microsoft.EntityFrameworkCore;
using ImmortalLoot.Server.Config;

namespace ImmortalLoot.Server.Services;

public sealed record ShopOffer(string Id, string ShopId, string ItemId, GameCurrency Currency, long Price, string LimitType, int LimitCount, string UnlockRealmId);
public sealed record ShopPurchaseResult(Guid PurchaseId, string ProductId, string ItemId, int Quantity, long TotalPrice, long BalanceAfter, bool Replayed);

public sealed class ShopService(GameDbContext db, CurrencyService currencies, IServerClock clock, ServerGameConfigCatalog catalog)
{
    private readonly IReadOnlyList<ShopOffer> _offers = catalog.ShopItems.Select(value => new ShopOffer(value.Id, value.ShopId, value.ItemId,
        Enum.Parse<GameCurrency>(value.Currency), value.Price, value.LimitType, value.LimitCount, value.UnlockCondition)).ToArray();

    public IReadOnlyList<ShopOffer> List() => _offers;

    public async Task<ShopPurchaseResult> PurchaseAsync(Guid playerId, string productId, int quantity, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (quantity <= 0 || quantity > 99) throw new ArgumentException("Quantity must be between 1 and 99.");
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 160) throw new ArgumentException("A valid idempotency key is required.");
        var offer = _offers.SingleOrDefault(value => value.Id == productId) ?? throw new KeyNotFoundException("Shop product was not found.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var replay = await db.ShopPurchases.SingleOrDefaultAsync(value => value.PlayerId == playerId && value.IdempotencyKey == idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(replay.Id, replay.ProductId, offer.ItemId, replay.Quantity, replay.TotalPrice, replay.BalanceAfter, true);
        }
        var player = await db.Players.SingleOrDefaultAsync(value => value.Id == playerId, cancellationToken) ?? throw new KeyNotFoundException("Player was not found.");
        if (offer.UnlockRealmId.Length > 0 && player.RealmId != offer.UnlockRealmId) throw new InvalidOperationException("Shop product is not unlocked.");
        var periodKey = offer.LimitType == "Daily" ? clock.UtcNow.ToString("yyyy-MM-dd") : "lifetime";
        var counter = await db.PlayerPurchases.SingleOrDefaultAsync(value => value.PlayerId == playerId && value.ProductId == productId && value.PeriodKey == periodKey, cancellationToken);
        var purchased = counter?.PurchaseCount ?? 0;
        if (offer.LimitCount > 0 && purchased + quantity > offer.LimitCount) throw new InvalidOperationException("Purchase limit exceeded.");
        var total = checked(offer.Price * quantity);
        var purchaseId = Guid.NewGuid();
        var balance = await currencies.ChangeAsync(playerId, offer.Currency, -total, "ShopPurchase", purchaseId.ToString("N"), cancellationToken);
        if (counter is null)
        {
            counter = new PlayerPurchase { PlayerId = playerId, ProductId = productId, PeriodKey = periodKey };
            db.PlayerPurchases.Add(counter);
        }
        counter.PurchaseCount += quantity;
        counter.LastPurchaseTimeUtc = clock.UtcNow;
        var inventory = await db.PlayerInventories.SingleOrDefaultAsync(value => value.PlayerId == playerId && value.ItemId == offer.ItemId && value.Category == "Consumable", cancellationToken);
        if (inventory is null)
        {
            inventory = new PlayerInventory { PlayerId = playerId, ItemId = offer.ItemId, Category = "Consumable" };
            db.PlayerInventories.Add(inventory);
        }
        inventory.Count = checked(inventory.Count + quantity);
        var purchase = new ShopPurchase
        {
            Id = purchaseId, PlayerId = playerId, ProductId = productId, IdempotencyKey = idempotencyKey,
            Quantity = quantity, Currency = offer.Currency.ToString(), TotalPrice = total, BalanceAfter = balance
        };
        db.ShopPurchases.Add(purchase);
        db.ItemLogs.Add(new ItemLog { PlayerId = playerId, ItemId = offer.ItemId, Delta = quantity, Reason = "ShopPurchase", ReferenceId = purchaseId.ToString("N") });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(purchase.Id, productId, offer.ItemId, quantity, total, balance, false);
    }
}
