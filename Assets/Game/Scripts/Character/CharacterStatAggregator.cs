using System;
using System.Collections.Generic;

namespace ImmortalLoot.Character
{
    public sealed class CharacterStatAggregator
    {
        public CharacterStats Calculate(CharacterStats baseStats, IEnumerable<StatModifier> modifiers)
        {
            if (baseStats == null) throw new ArgumentNullException(nameof(baseStats));
            if (modifiers == null) throw new ArgumentNullException(nameof(modifiers));
            var result = baseStats.Clone();
            var flat = new Dictionary<StatId, float>();
            var percent = new Dictionary<StatId, float>();
            foreach (var modifier in modifiers)
            {
                var target = modifier.Type == StatModifierType.Flat ? flat : percent;
                target.TryGetValue(modifier.Stat, out var current);
                target[modifier.Stat] = current + modifier.Value;
            }
            foreach (StatId stat in Enum.GetValues(typeof(StatId)))
            {
                flat.TryGetValue(stat, out var flatValue);
                percent.TryGetValue(stat, out var percentValue);
                var value = (baseStats.Get(stat) + flatValue) * (1f + percentValue);
                result.Set(stat, Clamp(stat, value));
            }
            return result;
        }

        private static float Clamp(StatId stat, float value)
        {
            switch (stat)
            {
                case StatId.HP:
                case StatId.Attack:
                case StatId.Defense:
                case StatId.AttackSpeed:
                    return Math.Max(0f, value);
                case StatId.CritRate:
                case StatId.Dodge:
                case StatId.LifeSteal:
                case StatId.DamageReduction:
                    return Math.Max(0f, Math.Min(0.95f, value));
                default:
                    return value;
            }
        }
    }
}
