using System;
using ImmortalLoot.AFK;
using ImmortalLoot.Config;
using ImmortalLoot.Core;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class AfkRewardServiceTests
    {
        [Test]
        public void Calculator_ClampsAtEightHoursAndCalculatesEveryRewardType()
        {
            var reward = new AfkRewardCalculator(Config()).Calculate((long)TimeSpan.FromHours(10).TotalSeconds, 1f);
            Assert.That(reward.EffectiveSeconds, Is.EqualTo(28800));
            Assert.That(reward.Experience, Is.EqualTo(5760));
            Assert.That(reward.SoftCurrency, Is.EqualTo(3840));
            Assert.That(reward.MaterialCount, Is.EqualTo(96));
            Assert.That(reward.EquipmentRolls, Is.EqualTo(96));
        }

        [Test]
        public void Calculator_AppliesStagePlayerActivityAndExtraHourBonuses()
        {
            var reward = new AfkRewardCalculator(Config()).Calculate(
                (long)TimeSpan.FromHours(9).TotalSeconds, 1.25f, 1.2f, 2f, 4);
            Assert.That(reward.EffectiveSeconds, Is.EqualTo(32400));
            Assert.That(reward.Experience, Is.EqualTo(19440));
            Assert.That(reward.EquipmentRolls, Is.EqualTo(324));
        }

        [Test]
        public void Claim_AdvancesAuthoritativeTimestampAndPreventsRepeatedReward()
        {
            var now = new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc);
            var clock = new FakeClock(now);
            var state = new AfkState { LastOfflineUnixSeconds = new DateTimeOffset(now.AddHours(-2)).ToUnixTimeSeconds() };
            var service = new AfkRewardService(Config(), state, clock);
            Assert.That(service.Claim(1f).EffectiveSeconds, Is.EqualTo(7200));
            Assert.That(service.Claim(1f).EffectiveSeconds, Is.EqualTo(0));
        }

        [Test]
        public void FreeQuickAfk_AllowsOncePerUtcDayAndResetsNextDay()
        {
            var clock = new FakeClock(new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc));
            var service = new AfkRewardService(Config(), new AfkState(), clock);
            Assert.That(service.ClaimFreeQuickAfk(1f).EffectiveSeconds, Is.EqualTo(7200));
            Assert.That(() => service.ClaimFreeQuickAfk(1f), Throws.InvalidOperationException);
            clock.UtcNow = clock.UtcNow.AddDays(1);
            Assert.That(service.ClaimFreeQuickAfk(1f).EffectiveSeconds, Is.EqualTo(7200));
        }

        private static AfkConfig Config() => AfkConfigLoader.Load(new ResourcesConfigSource());

        private sealed class FakeClock : IServerClock
        {
            public DateTime UtcNow { get; set; }
            public FakeClock(DateTime utcNow) => UtcNow = utcNow;
        }
    }
}
