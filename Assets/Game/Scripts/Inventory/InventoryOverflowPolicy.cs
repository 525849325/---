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

        public static EquipmentInstance SelectExplicitSacrificeCandidate(
            IReadOnlyList<EquipmentInstance> equipment,
            ISet<string> equippedInstanceIds)
        {
            if (equipment == null) throw new ArgumentNullException(nameof(equipment));
            equippedInstanceIds ??= new HashSet<string>(StringComparer.Ordinal);
            EquipmentInstance candidate = null;
            foreach (var item in equipment)
            {
                if (item == null || equippedInstanceIds.Contains(item.InstanceId)) continue;
                if (candidate == null || IsLowerValue(item, candidate)) candidate = item;
            }
            return candidate;
        }

        public static bool IsHigherValue(EquipmentInstance left, EquipmentInstance right)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            return CompareValue(left, right) > 0;
        }

        private static bool IsLowerValue(EquipmentInstance left, EquipmentInstance right)
        {
            var value = CompareValue(left, right);
            if (value != 0) return value < 0;
            var created = left.CreateTimeUtc.CompareTo(right.CreateTimeUtc);
            if (created != 0) return created < 0;
            return string.CompareOrdinal(left.InstanceId, right.InstanceId) < 0;
        }

        private static int CompareValue(EquipmentInstance left, EquipmentInstance right)
        {
            var quality = left.Quality.CompareTo(right.Quality);
            if (quality != 0) return quality;
            var level = left.Level.CompareTo(right.Level);
            if (level != 0) return level;
            return 0;
        }
    }
}
