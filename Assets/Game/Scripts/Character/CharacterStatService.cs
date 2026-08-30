using System;
using System.Collections.Generic;

namespace ImmortalLoot.Character
{
    public interface IStatModifierProvider
    {
        IEnumerable<StatModifier> GetModifiers();
    }

    public sealed class CharacterStatService
    {
        private readonly CharacterStatAggregator _aggregator;
        private readonly List<IStatModifierProvider> _providers = new List<IStatModifierProvider>();

        public CharacterStatService(CharacterStatAggregator aggregator = null)
        {
            _aggregator = aggregator ?? new CharacterStatAggregator();
        }

        public void AddProvider(IStatModifierProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (!_providers.Contains(provider)) _providers.Add(provider);
        }

        public bool RemoveProvider(IStatModifierProvider provider) => _providers.Remove(provider);

        public CharacterStats Calculate(CharacterStats baseStats)
        {
            var modifiers = new List<StatModifier>();
            foreach (var provider in _providers) modifiers.AddRange(provider.GetModifiers());
            return _aggregator.Calculate(baseStats, modifiers);
        }
    }
}
