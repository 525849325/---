using System;
using System.Collections.Generic;
using ImmortalLoot.Config;

namespace ImmortalLoot.Equipment
{
    public readonly struct EquipResult
    {
        public EquipmentSlot Slot { get; }
        public EquipmentInstance Equipped { get; }
        public EquipmentInstance Replaced { get; }
        public EquipResult(EquipmentSlot slot, EquipmentInstance equipped, EquipmentInstance replaced)
        { Slot = slot; Equipped = equipped; Replaced = replaced; }
    }

    public sealed class EquipmentLoadoutService
    {
        private readonly GameConfigCatalog _catalog;
        private readonly Dictionary<EquipmentSlot, EquipmentInstance> _equipped = new Dictionary<EquipmentSlot, EquipmentInstance>();
        public IReadOnlyDictionary<EquipmentSlot, EquipmentInstance> Equipped => _equipped;

        public EquipmentLoadoutService(GameConfigCatalog catalog) => _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

        public EquipResult Equip(EquipmentInstance item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            var definition = _catalog.GetEquipment(item.BaseId);
            EquipmentSlot? oldSlot = null;
            foreach (var pair in _equipped) if (pair.Value.InstanceId == item.InstanceId) oldSlot = pair.Key;
            if (oldSlot.HasValue) _equipped.Remove(oldSlot.Value);
            _equipped.TryGetValue(definition.Slot, out var replaced);
            _equipped[definition.Slot] = item;
            return new EquipResult(definition.Slot, item, replaced);
        }

        public EquipmentInstance Unequip(EquipmentSlot slot)
        {
            if (!_equipped.TryGetValue(slot, out var item)) return null;
            _equipped.Remove(slot);
            return item;
        }

        public void SetLocked(EquipmentInstance item, bool locked)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            item.IsLocked = locked;
        }
    }
}
