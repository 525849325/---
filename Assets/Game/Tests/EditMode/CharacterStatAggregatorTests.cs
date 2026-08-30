using System.Collections.Generic;
using ImmortalLoot.Character;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class CharacterStatAggregatorTests
    {
        [Test]
        public void Calculate_AppliesFlatThenAdditivePercentWithoutMutatingBaseStats()
        {
            var baseStats = new CharacterStats { HP = 100, Attack = 20, CritRate = 0.1f };
            var modifiers = new List<StatModifier>
            {
                new StatModifier(StatId.Attack, StatModifierType.Flat, 10, "equipment"),
                new StatModifier(StatId.Attack, StatModifierType.AdditivePercent, 0.2f, "realm"),
                new StatModifier(StatId.Attack, StatModifierType.AdditivePercent, 0.1f, "method")
            };
            var result = new CharacterStatAggregator().Calculate(baseStats, modifiers);
            Assert.That(result.Attack, Is.EqualTo(39f).Within(0.001f));
            Assert.That(baseStats.Attack, Is.EqualTo(20f));
        }

        [Test]
        public void Calculate_ClampsProbabilityStatsToSafeRange()
        {
            var stats = new CharacterStats { HP = 100, Attack = 10, CritRate = 0.8f };
            var result = new CharacterStatAggregator().Calculate(stats, new[]
            {
                new StatModifier(StatId.CritRate, StatModifierType.AdditivePercent, 1f, "test")
            });
            Assert.That(result.CritRate, Is.EqualTo(0.95f));
        }

        [Test]
        public void CharacterStatService_ComposesIndependentModuleProviders()
        {
            var service = new CharacterStatService();
            service.AddProvider(new FixedProvider(new StatModifier(StatId.Attack, StatModifierType.Flat, 5, "equipment")));
            service.AddProvider(new FixedProvider(new StatModifier(StatId.FireDamage, StatModifierType.Flat, 0.1f, "spiritual_root")));
            var result = service.Calculate(new CharacterStats { Attack = 10, HP = 100 });
            Assert.That(result.Attack, Is.EqualTo(15));
            Assert.That(result.FireDamage, Is.EqualTo(0.1f));
        }

        private sealed class FixedProvider : IStatModifierProvider
        {
            private readonly StatModifier _modifier;
            public FixedProvider(StatModifier modifier) => _modifier = modifier;
            public IEnumerable<StatModifier> GetModifiers() { yield return _modifier; }
        }
    }
}
