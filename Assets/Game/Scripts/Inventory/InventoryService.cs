using System;
using System.Collections.Generic;
using ImmortalLoot.Config;
using ImmortalLoot.Equipment;

namespace ImmortalLoot.Inventory
{
    public sealed class InventoryService
    {
        private readonly InventoryState _state;
        private readonly GameConfigCatalog _catalog;
        public InventoryState State => _state;

        public InventoryService(InventoryState state, GameConfigCatalog catalog)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public void AddEquipment(EquipmentInstance item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (_state.Equipment.Count >= _state.EquipmentCapacity) throw new InvalidOperationException("Equipment inventory is full.");
            if (_state.Equipment.Exists(value => value.InstanceId == item.InstanceId)) throw new InvalidOperationException($"Equipment '{item.InstanceId}' already exists.");
            if (item.Quality >= EquipmentQuality.Legendary) item.IsLocked = true;
            _state.Equipment.Add(item);
        }

        public void StorePendingEquipment(EquipmentInstance item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (_state.Equipment.Exists(value => value.InstanceId == item.InstanceId))
                throw new InvalidOperationException($"Equipment '{item.InstanceId}' already exists in the inventory.");
            if (_state.PendingEquipment != null)
            {
                if (_state.PendingEquipment.InstanceId == item.InstanceId) return;
                throw new InvalidOperationException("Pending equipment must be claimed before another drop can be stored.");
            }
            if (item.Quality >= EquipmentQuality.Legendary) item.IsLocked = true;
            _state.PendingEquipment = item;
        }

        public bool TryClaimPendingEquipment(out EquipmentInstance item)
        {
            item = null;
            var pending = _state.PendingEquipment;
            if (pending == null || _state.Equipment.Count >= _state.EquipmentCapacity) return false;
            if (_state.Equipment.Exists(value => value.InstanceId == pending.InstanceId))
                throw new InvalidOperationException($"Pending equipment '{pending.InstanceId}' already exists in the inventory.");

            AddEquipment(pending);
            _state.PendingEquipment = null;
            item = pending;
            return true;
        }

        public bool TryDiscardPendingEquipment(out EquipmentInstance item)
        {
            item = _state.PendingEquipment;
            if (item == null) return false;
            _state.PendingEquipment = null;
            return true;
        }

        public bool TryReplaceEquipmentWithPending(
            string replacementInstanceId,
            out EquipmentInstance claimed,
            out EquipmentInstance replaced)
        {
            claimed = null;
            replaced = null;
            if (string.IsNullOrWhiteSpace(replacementInstanceId) || _state.PendingEquipment == null ||
                _state.Equipment.Count < _state.EquipmentCapacity) return false;

            var replacementIndex = _state.Equipment.FindIndex(value => value.InstanceId == replacementInstanceId);
            if (replacementIndex < 0) return false;
            var pending = _state.PendingEquipment;
            if (_state.Equipment.Exists(value => value.InstanceId == pending.InstanceId))
                throw new InvalidOperationException($"Pending equipment '{pending.InstanceId}' already exists in the inventory.");

            replaced = _state.Equipment[replacementIndex];
            _state.Equipment[replacementIndex] = pending;
            _state.PendingEquipment = null;
            claimed = pending;
            return true;
        }

        public void AddStack(string itemId, int count, ItemCategory category)
        {
            if (string.IsNullOrWhiteSpace(itemId)) throw new ArgumentException("Item id is required.", nameof(itemId));
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            var list = category == ItemCategory.Material ? _state.Materials : _state.Consumables;
            var stack = list.Find(value => value.ItemId == itemId);
            if (stack == null) { stack = new ItemStack { ItemId = itemId }; list.Add(stack); }
            checked { stack.Count += count; }
        }

        public IReadOnlyList<EquipmentInstance> QueryEquipment(EquipmentSortMode sort, EquipmentSlot? slot = null, EquipmentQuality? minimumQuality = null, bool unlockedOnly = false)
        {
            var result = _state.Equipment.FindAll(item =>
                (!slot.HasValue || _catalog.GetEquipment(item.BaseId).Slot == slot.Value) &&
                (!minimumQuality.HasValue || item.Quality >= minimumQuality.Value) &&
                (!unlockedOnly || !item.IsLocked));
            result.Sort((left, right) => Compare(left, right, sort));
            return result;
        }

        public bool RemoveEquipment(string instanceId, out EquipmentInstance item)
        {
            var index = _state.Equipment.FindIndex(value => value.InstanceId == instanceId);
            if (index < 0) { item = null; return false; }
            item = _state.Equipment[index];
            _state.Equipment.RemoveAt(index);
            return true;
        }

        private int Compare(EquipmentInstance left, EquipmentInstance right, EquipmentSortMode mode)
        {
            switch (mode)
            {
                case EquipmentSortMode.LevelDescending: return right.Level.CompareTo(left.Level);
                case EquipmentSortMode.CreateTimeDescending: return right.CreateTimeUtc.CompareTo(left.CreateTimeUtc);
                case EquipmentSortMode.Slot: return _catalog.GetEquipment(left.BaseId).Slot.CompareTo(_catalog.GetEquipment(right.BaseId).Slot);
                default:
                    var quality = right.Quality.CompareTo(left.Quality);
                    return quality != 0 ? quality : right.Level.CompareTo(left.Level);
            }
        }
    }
}
