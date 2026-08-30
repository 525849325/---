using System;
using ImmortalLoot.Config;
using ImmortalLoot.Equipment;
using ImmortalLoot.Inventory;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class InventoryServiceTests
    {
        [Test]
        public void Inventory_StacksItemsEnforcesCapacityAndRejectsDuplicateInstances()
        {
            var service = new InventoryService(new InventoryState { EquipmentCapacity = 1 }, Catalog());
            var item = Item("one", EquipmentQuality.Rare, 1);
            service.AddEquipment(item);
            Assert.That(() => service.AddEquipment(item), Throws.InvalidOperationException);
            Assert.That(() => service.AddEquipment(Item("two", EquipmentQuality.Rare, 1)), Throws.InvalidOperationException);
            service.AddStack("item_spirit_dust", 2, ItemCategory.Material);
            service.AddStack("item_spirit_dust", 3, ItemCategory.Material);
            Assert.That(service.State.Materials[0].Count, Is.EqualTo(5));
        }

        [Test]
        public void Query_FiltersAndSortsWithoutMutatingInventoryOrder()
        {
            var state = new InventoryState();
            var service = new InventoryService(state, Catalog());
            var low = Item("low", EquipmentQuality.Fine, 20, "helmet_mistveil");
            var high = Item("high", EquipmentQuality.Epic, 5, "helmet_mistveil");
            service.AddEquipment(low);
            service.AddEquipment(high);
            var result = service.QueryEquipment(EquipmentSortMode.QualityDescending, EquipmentSlot.Helmet, EquipmentQuality.Rare);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].InstanceId, Is.EqualTo("high"));
            Assert.That(state.Equipment[0].InstanceId, Is.EqualTo("low"));
        }

        [Test]
        public void Legendary_IsAutomaticallyLockedAndCannotBeDecomposed()
        {
            var inventory = new InventoryService(new InventoryState(), Catalog());
            var legendary = Item("legend", EquipmentQuality.Legendary, 10);
            inventory.AddEquipment(legendary);
            Assert.That(legendary.IsLocked, Is.True);
            var decomposition = new EquipmentDecompositionService(inventory, Formula());
            Assert.That(() => decomposition.Decompose("legend"), Throws.InvalidOperationException);
        }

        [Test]
        public void BatchDecomposition_ProtectsLegendaryEvenIfManuallyUnlocked()
        {
            var inventory = new InventoryService(new InventoryState(), Catalog());
            var rare = Item("rare", EquipmentQuality.Rare, 10);
            var epic = Item("epic", EquipmentQuality.Epic, 10);
            var legendary = Item("legend", EquipmentQuality.Legendary, 10);
            inventory.AddEquipment(rare);
            inventory.AddEquipment(epic);
            inventory.AddEquipment(legendary);
            legendary.IsLocked = false;
            var reward = new EquipmentDecompositionService(inventory, Formula()).DecomposeAll(EquipmentQuality.Mythic);
            Assert.That(inventory.State.Equipment.Count, Is.EqualTo(1));
            Assert.That(inventory.State.Equipment[0], Is.SameAs(legendary));
            Assert.That(reward.SoftCurrency, Is.EqualTo(520));
            Assert.That(reward.EquipmentEssence, Is.EqualTo(3));
        }

        [Test]
        public void OverflowPolicy_NeverSelectsEquippedLockedOrLegendaryEquipment()
        {
            var equipped = Item("equipped", EquipmentQuality.Common, 1);
            var locked = Item("locked", EquipmentQuality.Fine, 2);
            locked.IsLocked = true;
            var legendary = Item("legendary", EquipmentQuality.Legendary, 3);
            var safe = Item("safe", EquipmentQuality.Rare, 4);
            var protectedIds = new System.Collections.Generic.HashSet<string> { equipped.InstanceId };

            var selected = InventoryOverflowPolicy.SelectDiscardCandidate(
                new[] { equipped, locked, legendary, safe }, protectedIds);

            Assert.That(selected, Is.SameAs(safe));
            Assert.That(InventoryOverflowPolicy.SelectDiscardCandidate(
                new[] { equipped, locked, legendary }, protectedIds), Is.Null);
        }

        private static EquipmentInstance Item(string id, EquipmentQuality quality, int level, string baseId = "weapon_cloudsteel_blade") => new EquipmentInstance
        {
            InstanceId = id, BaseId = baseId, Quality = quality, Level = level, CreateTimeUtc = DateTime.UtcNow
        };

        private static GameConfigCatalog Catalog() => new JsonConfigRepository(new ResourcesConfigSource()).LoadAll();
        private static DecompositionFormulaConfig Formula() => DecompositionFormulaLoader.Load(new ResourcesConfigSource());
    }
}
