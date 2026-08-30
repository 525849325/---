using System;
using System.Collections.Generic;
using ImmortalLoot.Equipment;

namespace ImmortalLoot.Inventory
{
    public static class InventoryOverflowPolicy
    {
        public static EquipmentInstance SelectDiscardCandidate(
            IReadOnlyList<EquipmentInstance> equipment,
            ISet<string> protectedInstanceIds)
        {
            if (equipment == null) throw new ArgumentNullException(nameof(equipment));
            protectedInstanceIds ??= new HashSet<string>(StringComparer.Ordinal);
            EquipmentInstance candidate = null;
            foreach (var item in equipment)
            {
                if (item == null || item.IsLocked || item.Quality >= EquipmentQuality.Legendary ||
                    protectedInstanceIds.Contains(item.InstanceId)) continue;
                if (candidate == null || IsLowerValue(item, candidate)) candidate = item;
            }
            return candidate;
        }

        private static bool IsLowerValue(EquipmentInstance left, EquipmentInstance right)
        {
            var quality = left.Quality.CompareTo(right.Quality);
            if (quality != 0) return quality < 0;
            var level = left.Level.CompareTo(right.Level);
            if (level != 0) return level < 0;
            var created = left.CreateTimeUtc.CompareTo(right.CreateTimeUtc);
            if (created != 0) return created < 0;
            return string.CompareOrdinal(left.InstanceId, right.InstanceId) < 0;
        }
    }
}
