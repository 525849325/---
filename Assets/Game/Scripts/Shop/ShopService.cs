using System;
using System.Collections.Generic;
using ImmortalLoot.Config;
using ImmortalLoot.Core;
using ImmortalLoot.Inventory;

namespace ImmortalLoot.Shop
{
    [Serializable]
    public sealed class ShopState
    {
        public List<ShopPurchaseCounter> Counters = new List<ShopPurchaseCounter>();
        public List<ShopReceipt> Receipts = new List<ShopReceipt>();
    }

    [Serializable] public sealed class ShopPurchaseCounter { public string ProductId; public string PeriodKey; public int Count; }
    [Serializable] public sealed class ShopReceipt { public string IdempotencyKey; public string ProductId; public int Quantity; public long BalanceAfter; }

    public sealed class ShopService
    {
        private readonly IReadOnlyDictionary<string, ShopItemConfig> _offers;
        private readonly ShopState _state;
        private readonly CurrencyService _currencies;
        private readonly InventoryService _inventory;
        private readonly IServerClock _clock;
        private readonly Func<string, bool> _isUnlocked;

        public ShopService(IReadOnlyDictionary<string, ShopItemConfig> offers, ShopState state, CurrencyService currencies,
            InventoryService inventory, IServerClock clock, Func<string, bool> isUnlocked)
        {
            _offers = offers ?? throw new ArgumentNullException(nameof(offers));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _currencies = currencies ?? throw new ArgumentNullException(nameof(currencies));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _isUnlocked = isUnlocked ?? (_ => true);
        }

        public ShopReceipt Purchase(string productId, int quantity, string idempotencyKey)
        {
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            foreach (var existingReceipt in _state.Receipts)
                if (existingReceipt.IdempotencyKey == idempotencyKey) return existingReceipt;
            if (!_offers.TryGetValue(productId, out var offer)) throw new KeyNotFoundException("Shop product was not found.");
            if (!string.IsNullOrEmpty(offer.UnlockCondition) && !_isUnlocked(offer.UnlockCondition)) throw new InvalidOperationException("Shop product is locked.");
            var period = PeriodKey(offer);
            var counter = FindCounter(productId, period);
            if (offer.LimitCount > 0 && counter.Count + quantity > offer.LimitCount) throw new InvalidOperationException("Purchase limit exceeded.");
            var total = checked(offer.Price * quantity);
            var debit = _currencies.Change(offer.Currency, -total, "ShopPurchase", "shop:" + idempotencyKey, _clock.UtcNow);
            _inventory.AddStack(offer.ItemId, quantity, ItemCategory.Consumable);
            counter.Count += quantity;
            var receipt = new ShopReceipt { IdempotencyKey = idempotencyKey, ProductId = productId, Quantity = quantity, BalanceAfter = debit.BalanceAfter };
            _state.Receipts.Add(receipt);
            return receipt;
        }

        private ShopPurchaseCounter FindCounter(string productId, string period)
        {
            foreach (var counter in _state.Counters)
                if (counter.ProductId == productId && counter.PeriodKey == period) return counter;
            var created = new ShopPurchaseCounter { ProductId = productId, PeriodKey = period };
            _state.Counters.Add(created);
            return created;
        }

        private string PeriodKey(ShopItemConfig offer)
        {
            if (offer.LimitType == LimitType.Daily) return _clock.UtcNow.ToString("yyyy-MM-dd");
            if (offer.LimitType == LimitType.Weekly)
            {
                var day = (7 + (int)_clock.UtcNow.DayOfWeek - (int)DayOfWeek.Monday) % 7;
                return _clock.UtcNow.Date.AddDays(-day).ToString("yyyy-MM-dd");
            }
            return "lifetime";
        }
    }
}
