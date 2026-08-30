using System;
using System.Collections.Generic;
using ImmortalLoot.Core;

namespace ImmortalLoot.Equipment
{
    public sealed class AffixGenerationException : Exception
    {
        public AffixGenerationException(string message) : base(message) { }
    }

    public sealed class AffixGenerator
    {
        private readonly IRandomSource _random;
        public AffixGenerator(IRandomSource random) => _random = random ?? throw new ArgumentNullException(nameof(random));

        public List<AffixRoll> Generate(IReadOnlyList<AffixDefinition> pool, int count)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var candidates = new List<AffixDefinition>(pool);
            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            var usedConflicts = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<AffixRoll>(count);
            while (result.Count < count)
            {
                var legal = candidates.FindAll(value =>
                    !usedIds.Contains(value.Id) &&
                    (string.IsNullOrWhiteSpace(value.ConflictGroup) || !usedConflicts.Contains(value.ConflictGroup)));
                if (legal.Count == 0)
                    throw new AffixGenerationException($"Affix pool can only produce {result.Count} legal rolls, but {count} were requested.");
                var picked = PickWeighted(legal);
                usedIds.Add(picked.Id);
                if (!string.IsNullOrWhiteSpace(picked.ConflictGroup)) usedConflicts.Add(picked.ConflictGroup);
                result.Add(new AffixRoll
                {
                    AffixId = picked.Id,
                    DisplayName = picked.DisplayName,
                    Value = picked.MinValue + (picked.MaxValue - picked.MinValue) * _random.Value(),
                    Stat = picked.Stat,
                    ModifierType = picked.ModifierType
                });
            }
            return result;
        }

        public static int CountMaximumLegalAffixes(IReadOnlyList<AffixDefinition> pool)
        {
            var conflictGroups = new HashSet<string>(StringComparer.Ordinal);
            var count = 0;
            foreach (var affix in pool)
            {
                if (string.IsNullOrWhiteSpace(affix.ConflictGroup)) count++;
                else if (conflictGroups.Add(affix.ConflictGroup)) count++;
            }
            return count;
        }

        private AffixDefinition PickWeighted(List<AffixDefinition> candidates)
        {
            var total = 0;
            foreach (var item in candidates) total += Math.Max(0, item.Weight);
            if (total <= 0) return candidates[_random.Range(0, candidates.Count)];
            var roll = _random.Range(0, total);
            foreach (var item in candidates)
            {
                roll -= Math.Max(0, item.Weight);
                if (roll < 0) return item;
            }
            return candidates[candidates.Count - 1];
        }
    }
}
