using System.Security.Cryptography;
using System.Text.Json;
using ImmortalLoot.Server.Persistence;
using Microsoft.EntityFrameworkCore;
using ImmortalLoot.Server.Config;

namespace ImmortalLoot.Server.Services;

public sealed record PaymentProduct(string Id, long AmountMinorUnits, string CurrencyCode, long PremiumCurrencyGrant);
public sealed record PaymentOrderResult(string OrderNo, string ProductId, string Status, long AmountMinorUnits, string CurrencyCode);
public sealed record ReceiptVerification(bool Valid, string ProviderTransactionId, string ProductId, long AmountMinorUnits, string CurrencyCode, string Detail);
public sealed record CommercialEntitlementResult(long DailyPremium, int AfkCapBonusHours, int QuickAfkBonus, bool FirstChargeClaimed, IReadOnlyList<string> ActiveProductIds);
public sealed record DailyCommercialClaimResult(string UtcDate, long PremiumCurrency, bool Replayed);

public interface IPaymentReceiptVerifier
{
    Task<ReceiptVerification> VerifyAsync(string provider, string receipt, CancellationToken cancellationToken);
}

public sealed class RejectingPaymentReceiptVerifier : IPaymentReceiptVerifier
{
    public Task<ReceiptVerification> VerifyAsync(string provider, string receipt, CancellationToken cancellationToken) =>
        Task.FromResult(new ReceiptVerification(false, string.Empty, string.Empty, 0, string.Empty, "No production payment verifier is configured."));
}

public sealed class DevelopmentPaymentReceiptVerifier(ServerGameConfigCatalog catalog) : IPaymentReceiptVerifier
{
    public Task<ReceiptVerification> VerifyAsync(string provider, string receipt, CancellationToken cancellationToken)
    {
        if (!string.Equals(provider, "mock", StringComparison.Ordinal) || !receipt.StartsWith("mock-receipt:", StringComparison.Ordinal))
            return Task.FromResult(new ReceiptVerification(false, string.Empty, string.Empty, 0, string.Empty, "Unsupported development receipt."));
        var payload = receipt["mock-receipt:".Length..];
        var separator = payload.IndexOf(':');
        if (separator <= 0 || separator == payload.Length - 1)
            return Task.FromResult(new ReceiptVerification(false, string.Empty, string.Empty, 0, string.Empty, "Malformed mock receipt."));
        var orderNo = payload[..separator];
        var productId = payload[(separator + 1)..];
        var product = catalog.CommercialProducts.SingleOrDefault(value => value.Id == productId);
        return Task.FromResult(product == null
            ? new ReceiptVerification(false, string.Empty, productId, 0, string.Empty, "Unknown product.")
            : new ReceiptVerification(true, orderNo, product.Id, product.AmountMinorUnits, product.CurrencyCode, "Development mock receipt accepted."));
    }
}

public sealed class PaymentService(GameDbContext db, CurrencyService currencies, IPaymentReceiptVerifier verifier, IServerClock clock, ServerGameConfigCatalog catalog)
{
    private readonly IReadOnlyList<PaymentProduct> _products = catalog.CommercialProducts.Select(value =>
        new PaymentProduct(value.Id, value.AmountMinorUnits, value.CurrencyCode, value.ImmediatePremium)).ToArray();

    public IReadOnlyList<PaymentProduct> ListProducts() => _products;

    public async Task<CommercialEntitlementResult> GetEntitlementsAsync(Guid playerId, CancellationToken cancellationToken)
        => await CommercialEntitlementCalculator.CalculateAsync(db, catalog, clock.UtcNow, playerId, cancellationToken);

    public async Task<DailyCommercialClaimResult> ClaimDailyEntitlementsAsync(Guid playerId, CancellationToken cancellationToken)
    {
        var date = clock.UtcNow.ToString("yyyy-MM-dd");
        var key = "commercial-daily:" + date;
        if (await db.RewardGrants.AnyAsync(value => value.PlayerId == playerId && value.IdempotencyKey == key, cancellationToken))
            return new DailyCommercialClaimResult(date, 0, true);
        var entitlement = await GetEntitlementsAsync(playerId, cancellationToken);
        if (entitlement.DailyPremium <= 0) throw new InvalidOperationException("No daily commercial entitlement is active.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await currencies.ChangeAsync(playerId, GameCurrency.PremiumCurrency, entitlement.DailyPremium, "CommercialDaily", key, cancellationToken);
        var payload = JsonSerializer.Serialize(new { entitlement.DailyPremium, entitlement.ActiveProductIds });
        db.RewardGrants.Add(new RewardGrant { PlayerId = playerId, IdempotencyKey = key, RewardType = "CommercialDaily", PayloadJson = payload });
        db.RewardLogs.Add(new RewardLog { PlayerId = playerId, IdempotencyKey = key, RewardType = "CommercialDaily", PayloadJson = payload });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DailyCommercialClaimResult(date, entitlement.DailyPremium, false);
    }

    public async Task<PaymentOrderResult> CreateOrderAsync(Guid playerId, string productId, CancellationToken cancellationToken)
    {
        if (!await db.Players.AnyAsync(value => value.Id == playerId, cancellationToken)) throw new KeyNotFoundException("Player was not found.");
        var product = Product(productId);
        var order = new PaymentOrder
        {
            PlayerId = playerId, OrderNo = CreateOrderNo(clock.UtcNow), ProductId = product.Id, Status = "Created",
            AmountMinorUnits = product.AmountMinorUnits, CurrencyCode = product.CurrencyCode
        };
        db.PaymentOrders.Add(order);
        db.PaymentLogs.Add(new PaymentLog { PlayerId = playerId, OrderNo = order.OrderNo, Action = "Created", DetailJson = JsonSerializer.Serialize(product) });
        await db.SaveChangesAsync(cancellationToken);
        return Map(order);
    }

    public async Task<PaymentOrderResult> VerifyAndGrantAsync(Guid playerId, string orderNo, string provider, string receipt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(receipt)) throw new ArgumentException("Provider and receipt are required.");
        var verification = await verifier.VerifyAsync(provider, receipt, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = await db.PaymentOrders.SingleOrDefaultAsync(value => value.PlayerId == playerId && value.OrderNo == orderNo, cancellationToken)
            ?? throw new KeyNotFoundException("Payment order was not found.");
        if (order.Status == "Granted")
        {
            await transaction.CommitAsync(cancellationToken);
            return Map(order);
        }
        var product = Product(order.ProductId);
        var productConfig = catalog.CommercialProducts.Single(value => value.Id == product.Id);
        var valid = verification.Valid && verification.ProductId == product.Id && verification.AmountMinorUnits == product.AmountMinorUnits &&
                    string.Equals(verification.CurrencyCode, product.CurrencyCode, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(verification.ProviderTransactionId);
        if (string.Equals(provider, "mock", StringComparison.Ordinal) && verification.ProviderTransactionId != order.OrderNo) valid = false;
        if (!valid)
        {
            order.Status = "VerificationFailed";
            db.PaymentLogs.Add(new PaymentLog { PlayerId = playerId, OrderNo = order.OrderNo, Action = "VerificationFailed", DetailJson = JsonSerializer.Serialize(verification) });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new InvalidOperationException("Payment receipt verification failed.");
        }
        var duplicate = await db.PaymentOrders.AnyAsync(value => value.Id != order.Id && value.Provider == provider && value.ProviderTransactionId == verification.ProviderTransactionId, cancellationToken);
        if (duplicate) throw new InvalidOperationException("Payment transaction was already consumed.");
        var player = await db.Players.SingleAsync(value => value.Id == playerId, cancellationToken);
        if (!string.IsNullOrEmpty(productConfig.UnlockRealmId))
        {
            var currentOrder = catalog.Realms.Single(value => value.Id == player.RealmId).Order;
            var requiredOrder = catalog.Realms.Single(value => value.Id == productConfig.UnlockRealmId).Order;
            if (currentOrder < requiredOrder) throw new InvalidOperationException("Commercial product is not unlocked for this realm.");
        }
        var purchase = await db.PlayerPurchases.SingleOrDefaultAsync(value => value.PlayerId == playerId && value.ProductId == product.Id && value.PeriodKey == "payment", cancellationToken);
        if (productConfig.LifetimeLimit > 0 && purchase != null && purchase.PurchaseCount >= productConfig.LifetimeLimit)
            throw new InvalidOperationException("Commercial product lifetime limit exceeded.");
        var firstCharge = !await db.PlayerPurchases.AnyAsync(value => value.PlayerId == playerId && value.PeriodKey == "first-charge", cancellationToken);
        order.Provider = provider;
        order.ProviderTransactionId = verification.ProviderTransactionId;
        order.VerifiedAtUtc = clock.UtcNow;
        order.GrantedAtUtc = clock.UtcNow;
        order.Status = "Granted";
        await currencies.ChangeAsync(playerId, GameCurrency.PremiumCurrency, product.PremiumCurrencyGrant, "Payment", order.OrderNo, cancellationToken);
        if (purchase == null)
        {
            purchase = new PlayerPurchase { PlayerId = playerId, ProductId = product.Id, PeriodKey = "payment" };
            db.PlayerPurchases.Add(purchase);
        }
        purchase.PurchaseCount++;
        purchase.LastPurchaseTimeUtc = clock.UtcNow;
        if (productConfig.RewardItemCount > 0) GrantItem(playerId, productConfig.RewardItemId, productConfig.RewardItemCount, "Consumable", order.OrderNo);
        if (firstCharge)
        {
            db.PlayerPurchases.Add(new PlayerPurchase { PlayerId = playerId, ProductId = "first_charge_reward", PeriodKey = "first-charge", PurchaseCount = 1, LastPurchaseTimeUtc = clock.UtcNow });
            GrantItem(playerId, "first_charge_material", 10, "Material", order.OrderNo);
            GrantItem(playerId, "quick_afk_ticket", 1, "Consumable", order.OrderNo);
            var instanceId = Guid.NewGuid().ToString("N");
            db.PlayerEquipment.Add(new PlayerEquipment { PlayerId = playerId, InstanceId = instanceId, BaseId = "artifact_firstlight", Slot = "Artifact", Level = 10, Quality = "Legendary", IsLocked = true, InstanceJson = JsonSerializer.Serialize(new { instanceId, baseId = "artifact_firstlight", slot = "Artifact", level = 10, quality = "Legendary", source = "FirstCharge" }) });
            db.EquipmentLogs.Add(new EquipmentLog { PlayerId = playerId, InstanceId = instanceId, Action = "FirstChargeGrant", ReferenceId = order.OrderNo });
        }
        db.PaymentLogs.Add(new PaymentLog { PlayerId = playerId, OrderNo = order.OrderNo, Action = "Granted", DetailJson = JsonSerializer.Serialize(new { verification.ProviderTransactionId, product.PremiumCurrencyGrant }) });
        db.RewardLogs.Add(new RewardLog
        {
            PlayerId = playerId,
            IdempotencyKey = "payment:" + order.OrderNo,
            RewardType = "Payment",
            PayloadJson = JsonSerializer.Serialize(new { product.Id, product.PremiumCurrencyGrant, productConfig.RewardItemId, productConfig.RewardItemCount, FirstCharge = firstCharge })
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(order);
    }

    private PaymentProduct Product(string id) => _products.SingleOrDefault(value => value.Id == id) ?? throw new KeyNotFoundException("Payment product was not found.");
    private void GrantItem(Guid playerId, string itemId, int count, string category, string referenceId)
    {
        var stack = db.PlayerInventories.Local.SingleOrDefault(value => value.PlayerId == playerId && value.ItemId == itemId && value.Category == category)
            ?? db.PlayerInventories.SingleOrDefault(value => value.PlayerId == playerId && value.ItemId == itemId && value.Category == category);
        if (stack == null) { stack = new PlayerInventory { PlayerId = playerId, ItemId = itemId, Category = category }; db.PlayerInventories.Add(stack); }
        stack.Count = checked(stack.Count + count);
        db.ItemLogs.Add(new ItemLog { PlayerId = playerId, ItemId = itemId, Delta = count, Reason = "Payment", ReferenceId = referenceId });
    }
    private static PaymentOrderResult Map(PaymentOrder order) => new(order.OrderNo, order.ProductId, order.Status, order.AmountMinorUnits, order.CurrencyCode);
    private static string CreateOrderNo(DateTime nowUtc) => "IL" + nowUtc.ToString("yyyyMMddHHmmss") + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
}

public static class CommercialEntitlementCalculator
{
    public static async Task<CommercialEntitlementResult> CalculateAsync(GameDbContext db, ServerGameConfigCatalog catalog, DateTime nowUtc, Guid playerId, CancellationToken cancellationToken)
    {
        var purchases = await db.PlayerPurchases.AsNoTracking().Where(value => value.PlayerId == playerId && value.PeriodKey == "payment").ToListAsync(cancellationToken);
        var active = new List<string>();
        long dailyPremium = 0;
        var afkHours = 0;
        var quickAfk = 0;
        foreach (var purchase in purchases)
        {
            var product = catalog.CommercialProducts.SingleOrDefault(value => value.Id == purchase.ProductId);
            if (product == null || !(product.DurationDays < 0 || product.DurationDays > 0 && nowUtc < purchase.LastPurchaseTimeUtc.AddDays(product.DurationDays))) continue;
            active.Add(product.Id);
            dailyPremium = checked(dailyPremium + product.DailyPremium);
            afkHours = checked(afkHours + product.AfkCapBonusHours);
            quickAfk = checked(quickAfk + product.QuickAfkBonus);
        }
        var firstCharge = await db.PlayerPurchases.AsNoTracking().AnyAsync(value => value.PlayerId == playerId && value.PeriodKey == "first-charge", cancellationToken);
        return new CommercialEntitlementResult(dailyPremium, afkHours, quickAfk, firstCharge, active);
    }
}
