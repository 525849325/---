using System;
using System.Collections.Generic;
using ImmortalLoot.Config;
using ImmortalLoot.Inventory;
using ImmortalLoot.Shop;
using ImmortalLoot.Equipment;
using ImmortalLoot.Core;
using UnityEngine;

namespace ImmortalLoot.Payment
{
    [Serializable]
    public sealed class CommercialProductConfig
    {
        public string id; public string name; public string type; public long amountMinorUnits; public string currencyCode;
        public long immediatePremium; public long dailyPremium; public int durationDays; public int afkCapBonusHours;
        public int quickAfkBonus; public int lifetimeLimit; public string unlockRealmId; public string rewardItemId; public int rewardItemCount;
    }
    [Serializable] internal sealed class CommercialProductFile { public int schemaVersion; public CommercialProductConfig[] products; }
    [Serializable] public sealed class CommercialPurchase { public string ProductId; public DateTime PurchasedAtUtc; }
    [Serializable] public sealed class CommercialState { public List<CommercialPurchase> Purchases = new List<CommercialPurchase>(); public bool FirstChargeClaimed; }

    public sealed class CommercialEntitlementService
    {
        private readonly Dictionary<string, CommercialProductConfig> _products;
        private readonly CommercialState _state;
        private readonly CurrencyService _currencies;
        private readonly InventoryService _inventory;
        private readonly GameConfigCatalog _catalog;
        private readonly EquipmentGenerator _equipment;

        public CommercialEntitlementService(IConfigSource source, CommercialState state, CurrencyService currencies, InventoryService inventory)
        {
            var file = JsonUtility.FromJson<CommercialProductFile>(source.LoadText("commercial_products"));
            if (file == null || file.schemaVersion != 1 || file.products == null) throw new ConfigException("Commercial product config is invalid.");
            _products = new Dictionary<string, CommercialProductConfig>(StringComparer.Ordinal);
            foreach (var product in file.products)
            {
                if (string.IsNullOrWhiteSpace(product.id) || product.amountMinorUnits <= 0 || product.immediatePremium < 0 || product.durationDays < -1 || !_products.TryAdd(product.id, product))
                    throw new ConfigException("Commercial product row is invalid or duplicated.");
            }
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _currencies = currencies ?? throw new ArgumentNullException(nameof(currencies));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _catalog = new JsonConfigRepository(source).LoadAll();
            _equipment = new EquipmentGenerator(new SystemRandomSource(), _catalog);
        }

        public IReadOnlyCollection<CommercialProductConfig> Products => _products.Values;

        public void ApplyServerVerifiedPurchase(string productId, string serverOrderNo, DateTime nowUtc, Func<string, bool> realmUnlocked)
        {
            if (!_products.TryGetValue(productId, out var product)) throw new ConfigException("Commercial product was not found.");
            if (!string.IsNullOrEmpty(product.unlockRealmId) && !(realmUnlocked?.Invoke(product.unlockRealmId) ?? false)) throw new InvalidOperationException("Realm pack is locked.");
            var purchased = _state.Purchases.FindAll(value => value.ProductId == productId).Count;
            if (product.lifetimeLimit > 0 && purchased >= product.lifetimeLimit) throw new InvalidOperationException("Lifetime purchase limit exceeded.");
            if (string.IsNullOrWhiteSpace(serverOrderNo)) throw new ArgumentException("A server-verified order is required.");
            _currencies.Change(CurrencyType.PremiumCurrency, product.immediatePremium, "VerifiedPayment", "payment:" + serverOrderNo, nowUtc);
            if (product.rewardItemCount > 0) _inventory.AddStack(product.rewardItemId, product.rewardItemCount, ItemCategory.Consumable);
            _state.Purchases.Add(new CommercialPurchase { ProductId = productId, PurchasedAtUtc = nowUtc });
            if (!_state.FirstChargeClaimed)
            {
                _state.FirstChargeClaimed = true;
                _inventory.AddStack("first_charge_material", 10, ItemCategory.Material);
                _inventory.AddStack("quick_afk_ticket", 1, ItemCategory.Consumable);
                _inventory.AddEquipment(_equipment.Generate(_catalog.GetEquipment("artifact_firstlight"), 10, EquipmentQuality.Legendary, "FirstCharge"));
            }
        }

        public int AfkCapBonusHours(DateTime nowUtc) => SumActive(nowUtc, value => value.afkCapBonusHours);
        public int QuickAfkDailyBonus(DateTime nowUtc) => SumActive(nowUtc, value => value.quickAfkBonus);
        public long DailyPremium(DateTime nowUtc) => SumActiveLong(nowUtc, value => value.dailyPremium);

        private int SumActive(DateTime nowUtc, Func<CommercialProductConfig, int> selector)
        { var sum = 0; foreach (var purchase in _state.Purchases) { var product = _products[purchase.ProductId]; if (Active(product, purchase, nowUtc)) sum += selector(product); } return sum; }
        private long SumActiveLong(DateTime nowUtc, Func<CommercialProductConfig, long> selector)
        { long sum = 0; foreach (var purchase in _state.Purchases) { var product = _products[purchase.ProductId]; if (Active(product, purchase, nowUtc)) sum += selector(product); } return sum; }
        private static bool Active(CommercialProductConfig product, CommercialPurchase purchase, DateTime nowUtc) => product.durationDays < 0 || product.durationDays > 0 && nowUtc < purchase.PurchasedAtUtc.AddDays(product.durationDays);
    }
}
