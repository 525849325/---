using System;
using System.Collections.Generic;
using ImmortalLoot.Config;
using ImmortalLoot.Core;

namespace ImmortalLoot.Equipment
{
    public sealed class EquipmentGenerator
    {
        private readonly IRandomSource _random;
        private readonly GameConfigCatalog _catalog;
        private readonly AffixGenerator _affixes;

        public EquipmentGenerator(IRandomSource random, GameConfigCatalog catalog)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _affixes = new AffixGenerator(_random);
        }

        public EquipmentInstance Generate(EquipmentDefinition definition, int level, EquipmentQuality quality, string source)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.AffixPool == null || definition.AffixPool.Count == 0) throw new ArgumentException("Affix pool cannot be empty.");

            var result = new EquipmentInstance
            {
                InstanceId = Guid.NewGuid().ToString("N"),
                BaseId = definition.Id,
                DisplayName = definition.DisplayName,
                Level = Math.Max(1, level),
                Quality = quality,
                Source = source,
                SetId = definition.SetId,
                IsLocked = quality >= EquipmentQuality.Legendary,
                CreateTimeUtc = DateTime.UtcNow
            };
            foreach (var baseStat in definition.BaseStats)
            {
                result.BaseStats.Add(new EquipmentStatRoll
                {
                    Stat = baseStat.Stat,
                    ModifierType = baseStat.ModifierType,
                    Value = baseStat.BaseValue + baseStat.ValuePerLevel * (result.Level - 1)
                });
            }

            var rule = _catalog.GetQualityRule(quality);
            var count = rule.Min == rule.Max ? rule.Min : _random.Range(rule.Min, rule.Max + 1);
            result.Affixes.AddRange(_affixes.Generate(definition.AffixPool, count));
            if (quality == EquipmentQuality.Mythic && definition.SpecialEffectPool.Count > 0)
                result.SpecialEffectId = definition.SpecialEffectPool[_random.Range(0, definition.SpecialEffectPool.Count)];
            return result;
        }

    }
}
