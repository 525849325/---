using System;
using ImmortalLoot.Config;
using ImmortalLoot.Inventory;
using ImmortalLoot.Payment;
using ImmortalLoot.Shop;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class CommercialEntitlementTests
    {
        [Test]
        public void Catalog_ContainsOnlyTheSixMvpCommercialProductFamilies()
        {
            var fixture = Create();
            Assert.That(fixture.Service.Products.Count, Is.EqualTo(6));
        }

        [Test]
        public void VerifiedPurchase_GrantsFirstChargeOnceAndTimedCardBenefits()
        {
            var fixture = Create();
            var now = new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc);
            fixture.Service.ApplyServerVerifiedPurchase("monthly_card_30", "order-month", now, _ => true);
            fixture.Service.ApplyServerVerifiedPurchase("jade_60", "order-jade", now, _ => true);
            Assert.That(fixture.Currency.Balance(CurrencyType.PremiumCurrency), Is.EqualTo(360));
            Assert.That(fixture.Inventory.State.Materials.Find(value => value.ItemId == "first_charge_material").Count, Is.EqualTo(10));
            Assert.That(fixture.Inventory.State.Consumables.Find(value => value.ItemId == "quick_afk_ticket").Count, Is.EqualTo(1));
            Assert.That(fixture.Inventory.State.Equipment.Count, Is.EqualTo(1));
            Assert.That(fixture.Inventory.State.Equipment[0].IsLocked, Is.True);
            Assert.That(fixture.Service.AfkCapBonusHours(now.AddDays(29)), Is.EqualTo(4));
            Assert.That(fixture.Service.QuickAfkDailyBonus(now.AddDays(29)), Is.EqualTo(1));
            Assert.That(fixture.Service.DailyPremium(now.AddDays(30)), Is.EqualTo(0));
        }

        [Test]
        public void RealmAndLifetimeProducts_EnforceUnlockAndLimit()
        {
            var fixture = Create();
            var now = DateTime.UtcNow;
            Assert.That(() => fixture.Service.ApplyServerVerifiedPurchase("realm_pack_core", "locked", now, _ => false), Throws.InvalidOperationException);
            fixture.Service.ApplyServerVerifiedPurchase("permanent_card_98", "permanent", now, _ => true);
            Assert.That(() => fixture.Service.ApplyServerVerifiedPurchase("permanent_card_98", "duplicate", now, _ => true), Throws.InvalidOperationException);
            Assert.That(fixture.Service.DailyPremium(now.AddYears(10)), Is.EqualTo(10));
        }

        private static Fixture Create()
        {
            var source = new ResourcesConfigSource();
            var catalog = new JsonConfigRepository(source).LoadAll();
            var currency = new CurrencyService(new CurrencyState());
            var inventory = new InventoryService(new InventoryState(), catalog);
            return new Fixture { Currency = currency, Inventory = inventory, Service = new CommercialEntitlementService(source, new CommercialState(), currency, inventory) };
        }
        private sealed class Fixture { public CurrencyService Currency; public InventoryService Inventory; public CommercialEntitlementService Service; }
    }
}
