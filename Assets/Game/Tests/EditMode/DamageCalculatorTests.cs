using ImmortalLoot.Battle;
using ImmortalLoot.Character;
using ImmortalLoot.Config;
using ImmortalLoot.Core;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class DamageCalculatorTests
    {
        [Test]
        public void Calculate_AppliesDefenseElementAndConfiguredMinimum()
        {
            var calculator = new DamageCalculator(new DamageFormulaConfig(), new FixedRandom(0.9f));
            var attacker = new CharacterStats { Attack = 100, FireDamage = 0.5f, CritDamage = 2f };
            var defender = new CharacterStats { Defense = 100, FireResistance = 0.2f };
            var result = calculator.Calculate(new DamageRequest(attacker, defender, 1f, ElementType.Fire));
            Assert.That(result.Amount, Is.EqualTo(65f).Within(0.001f));
            Assert.That(result.IsCritical, Is.False);

            attacker.Attack = 0;
            result = calculator.Calculate(new DamageRequest(attacker, defender, 0f, ElementType.None));
            Assert.That(result.Amount, Is.EqualTo(1f));
        }

        [Test]
        public void Calculate_CriticalHitUsesCritDamageMultiplier()
        {
            var calculator = new DamageCalculator(new DamageFormulaConfig(), new FixedRandom(0f));
            var result = calculator.Calculate(new DamageRequest(
                new CharacterStats { Attack = 20, CritRate = 1f, CritDamage = 2.5f },
                new CharacterStats(), 1f, ElementType.None));
            Assert.That(result.Amount, Is.EqualTo(50f));
            Assert.That(result.IsCritical, Is.True);
        }

        [Test]
        public void RuntimeFormula_LoadsFromVersionedJson()
        {
            var config = DamageFormulaConfigLoader.Load(new ResourcesConfigSource());
            Assert.That(config.DefenseConstant, Is.EqualTo(100f));
            Assert.That(config.MinimumDamage, Is.EqualTo(1f));
            Assert.That(config.MaximumDamageReduction, Is.EqualTo(0.9f));
        }

        internal sealed class FixedRandom : IRandomSource
        {
            private readonly float _value;
            public FixedRandom(float value) => _value = value;
            public int Range(int minInclusive, int maxExclusive) => minInclusive;
            public float Value() => _value;
        }
    }
}
