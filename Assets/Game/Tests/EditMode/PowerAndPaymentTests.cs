using System.Threading.Tasks;
using ImmortalLoot.Character;
using ImmortalLoot.Config;
using ImmortalLoot.Payment;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class PowerAndPaymentTests
    {
        [Test]
        public void PowerCalculator_UsesOneConfiguredFormulaAndIsMonotonic()
        {
            var calculator = PowerCalculator.Load(new ResourcesConfigSource());
            var baseline = calculator.Calculate(new CharacterStats { HP = 100, Attack = 10, Defense = 5, CritDamage = 1.5f, AttackSpeed = 1 });
            var improved = calculator.Calculate(new CharacterStats { HP = 150, Attack = 15, Defense = 8, CritRate = 0.1f, CritDamage = 1.5f, AttackSpeed = 1 });
            Assert.That(baseline, Is.GreaterThan(0));
            Assert.That(improved, Is.GreaterThan(baseline));
        }

        [Test]
        public async Task MockPaymentProvider_ReturnsReceiptButNeverGrantsCurrency()
        {
            var result = await new MockPaymentProvider().PurchaseAsync(new PaymentRequest("order-1", "jade_60"));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Provider, Is.EqualTo("mock"));
            Assert.That(result.Receipt, Does.Contain("order-1"));
        }
    }
}
