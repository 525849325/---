using System;
using ImmortalLoot.Config;
using ImmortalLoot.Core;
using ImmortalLoot.Inventory;
using ImmortalLoot.Shop;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class ShopServiceTests
    {
        [Test]
        public void CurrencyService_IsIdempotentAndRejectsInsufficientBalance()
        {
            var service = new CurrencyService(new CurrencyState { SoftCurrency = 100 });
            var first = service.Change(CurrencyType.SoftCurrency, -40, "test", "key", DateTime.UtcNow);
            var replay = service.Change(CurrencyType.SoftCurrency, -40, "test", "key", DateTime.UtcNow);
            Assert.That(replay, Is.SameAs(first));
            Assert.That(service.Balance(CurrencyType.SoftCurrency), Is.EqualTo(60));
            Assert.That(service.State.Transactions.Count, Is.EqualTo(1));
            Assert.That(() => service.Change(CurrencyType.SoftCurrency, -61, "test", "other", DateTime.UtcNow), Throws.InvalidOperationException);
        }

        [Test]
        public void Purchase_DebitsGrantsAndReplaysOnlyOnce()
        {
            var fixture = Create(2500);
            var first = fixture.Shop.Purchase("shop_spirit_dust", 2, "buy-1");
            var replay = fixture.Shop.Purchase("shop_spirit_dust", 2, "buy-1");
            Assert.That(replay, Is.SameAs(first));
            Assert.That(fixture.Currency.Balance(CurrencyType.SoftCurrency), Is.EqualTo(500));
            Assert.That(fixture.Inventory.State.Materials.Count, Is.EqualTo(0));
            Assert.That(fixture.Inventory.State.Consumables[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void Purchase_EnforcesDailyLimitAndUnlockCondition()
        {
            var fixture = Create(10000, false);
            fixture.Shop.Purchase("shop_spirit_dust", 5, "limit-ok");
            Assert.That(() => fixture.Shop.Purchase("shop_spirit_dust", 1, "limit-fail"), Throws.InvalidOperationException);
            Assert.That(() => fixture.Shop.Purchase("shop_daily_afk_ticket", 1, "locked"), Throws.InvalidOperationException);
        }

        private static Fixture Create(long softCurrency, bool unlocked = true)
        {
            var catalog = new JsonConfigRepository(new ResourcesConfigSource()).LoadAll();
            var currency = new CurrencyService(new CurrencyState { SoftCurrency = softCurrency, PremiumCurrency = 100 });
            var inventory = new InventoryService(new InventoryState(), catalog);
            var shop = new ShopService(catalog.ShopItems, new ShopState(), currency, inventory, new FixedClock(), _ => unlocked);
            return new Fixture { Shop = shop, Currency = currency, Inventory = inventory };
        }

        private sealed class Fixture { public ShopService Shop; public CurrencyService Currency; public InventoryService Inventory; }
        private sealed class FixedClock : IServerClock { public DateTime UtcNow => new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc); }
    }
}
