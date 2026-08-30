using System;
using ImmortalLoot.Config;
using ImmortalLoot.Equipment;
using ImmortalLoot.Inventory;
using NUnit.Framework;
using UnityEngine;

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

            var explicitSacrifice = InventoryOverflowPolicy.SelectExplicitSacrificeCandidate(
                new[] { equipped, locked, legendary, safe }, protectedIds);
            Assert.That(explicitSacrifice, Is.SameAs(locked),
                "A confirmed recovery action may select the lowest-value protected item, but never an equipped item.");
            var sameTierNewer = Item("same-tier-newer", locked.Quality, locked.Level);
            sameTierNewer.CreateTimeUtc = locked.CreateTimeUtc.AddHours(1);
            Assert.That(InventoryOverflowPolicy.IsHigherValue(sameTierNewer, locked), Is.False,
                "Creation time must not be presented as a real equipment-value upgrade.");
        }

        [Test]
        public void FullProtectedInventory_StoresHighQualityDropAsSerializablePendingEquipment()
        {
            var state = new InventoryState { EquipmentCapacity = 1 };
            var inventory = new InventoryService(state, Catalog());
            var protectedItem = Item("protected", EquipmentQuality.Legendary, 10);
            inventory.AddEquipment(protectedItem);
            var incoming = Item("pending-mythic", EquipmentQuality.Mythic, 20);

            var candidate = InventoryOverflowPolicy.SelectDiscardCandidate(
                state.Equipment,
                new System.Collections.Generic.HashSet<string> { protectedItem.InstanceId });
            Assert.That(candidate, Is.Null, "The reproduction requires a full inventory with no safe discard candidate.");

            inventory.StorePendingEquipment(incoming);

            Assert.That(state.Equipment, Has.Count.EqualTo(1));
            Assert.That(state.Equipment[0], Is.SameAs(protectedItem));
            Assert.That(state.PendingEquipment, Is.SameAs(incoming), "The high-quality drop must remain owned while the bag is full.");
            Assert.That(state.PendingEquipment.IsLocked, Is.True, "Legendary and Mythic pending drops must receive the same protection as bagged equipment.");

            var restored = InventoryStateCodec.Deserialize(InventoryStateCodec.Serialize(state));
            Assert.That(restored.PendingEquipment, Is.Not.Null);
            Assert.That(restored.PendingEquipment.InstanceId, Is.EqualTo(incoming.InstanceId));
            Assert.That(restored.PendingEquipment.Quality, Is.EqualTo(EquipmentQuality.Mythic));
            Assert.That(restored.PendingEquipment.IsLocked, Is.True);
        }

        [Test]
        public void EmptyPendingEquipment_RoundTripDoesNotCreateGhostDrop()
        {
            var restored = InventoryStateCodec.Deserialize(
                InventoryStateCodec.Serialize(new InventoryState { EquipmentCapacity = 120 }));

            Assert.That(restored.PendingEquipment, Is.Null);
            Assert.That(restored.EquipmentCapacity, Is.EqualTo(120));
        }

        [Test]
        public void PendingEquipment_IsClaimedExactlyOnceAfterCapacityBecomesAvailable()
        {
            var state = new InventoryState { EquipmentCapacity = 1 };
            var inventory = new InventoryService(state, Catalog());
            var existing = Item("existing", EquipmentQuality.Legendary, 10);
            var pending = Item("pending", EquipmentQuality.Mythic, 20);
            inventory.AddEquipment(existing);
            inventory.StorePendingEquipment(pending);

            Assert.That(inventory.TryClaimPendingEquipment(out var blockedClaim), Is.False);
            Assert.That(blockedClaim, Is.Null);
            Assert.That(state.PendingEquipment, Is.SameAs(pending));
            Assert.That(state.Equipment, Has.Count.EqualTo(1));

            Assert.That(inventory.RemoveEquipment(existing.InstanceId, out _), Is.True);
            Assert.That(inventory.TryClaimPendingEquipment(out var claimed), Is.True);
            Assert.That(claimed, Is.SameAs(pending));
            Assert.That(state.PendingEquipment, Is.Null);
            Assert.That(state.Equipment, Has.Count.EqualTo(1));
            Assert.That(state.Equipment[0], Is.SameAs(pending));

            Assert.That(inventory.TryClaimPendingEquipment(out var duplicateClaim), Is.False);
            Assert.That(duplicateClaim, Is.Null);
            Assert.That(state.Equipment, Has.Count.EqualTo(1), "A pending drop must never be inserted twice.");
        }

        [Test]
        public void FullInventory_ExplicitReplacementClaimsPendingExactlyOnceWithoutChangingCount()
        {
            var state = new InventoryState { EquipmentCapacity = 1 };
            var inventory = new InventoryService(state, Catalog());
            var existing = Item("existing-protected", EquipmentQuality.Legendary, 10);
            var pending = Item("pending-upgrade", EquipmentQuality.Mythic, 20);
            inventory.AddEquipment(existing);
            inventory.StorePendingEquipment(pending);

            Assert.That(inventory.TryReplaceEquipmentWithPending(existing.InstanceId, out var claimed, out var replaced), Is.True);
            Assert.That(claimed, Is.SameAs(pending));
            Assert.That(replaced, Is.SameAs(existing));
            Assert.That(state.PendingEquipment, Is.Null);
            Assert.That(state.Equipment, Has.Count.EqualTo(1));
            Assert.That(state.Equipment[0], Is.SameAs(pending));

            Assert.That(inventory.TryReplaceEquipmentWithPending(existing.InstanceId, out _, out _), Is.False,
                "The same pending equipment must not be replaceable twice.");
            Assert.That(state.Equipment, Has.Count.EqualTo(1));
        }

        [Test]
        public void LowerValuePendingEquipment_CanBeExplicitlyDiscardedExactlyOnce()
        {
            var state = new InventoryState { EquipmentCapacity = 1 };
            var inventory = new InventoryService(state, Catalog());
            var existing = Item("existing-mythic", EquipmentQuality.Mythic, 20);
            var pending = Item("pending-common", EquipmentQuality.Common, 1);
            inventory.AddEquipment(existing);
            inventory.StorePendingEquipment(pending);

            Assert.That(InventoryOverflowPolicy.IsHigherValue(pending, existing), Is.False);
            Assert.That(inventory.TryDiscardPendingEquipment(out var discarded), Is.True);
            Assert.That(discarded, Is.SameAs(pending));
            Assert.That(state.PendingEquipment, Is.Null);
            Assert.That(state.Equipment, Has.Count.EqualTo(1));
            Assert.That(state.Equipment[0], Is.SameAs(existing));
            Assert.That(inventory.TryDiscardPendingEquipment(out _), Is.False);
        }

        private static EquipmentInstance Item(string id, EquipmentQuality quality, int level, string baseId = "weapon_cloudsteel_blade") => new EquipmentInstance
        {
            InstanceId = id, BaseId = baseId, Quality = quality, Level = level, CreateTimeUtc = DateTime.UtcNow
        };

        private static GameConfigCatalog Catalog() => new JsonConfigRepository(new ResourcesConfigSource()).LoadAll();
        private static DecompositionFormulaConfig Formula() => DecompositionFormulaLoader.Load(new ResourcesConfigSource());
    }
}
