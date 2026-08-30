using ImmortalLoot.Config;
using ImmortalLoot.Stage;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class DemoPacingTests
    {
        [Test]
        public void TwoHourTimeline_MatchesRequiredExperienceWindows()
        {
            var pacing = DemoPacingLoader.Load(new ResourcesConfigSource());
            Assert.That(pacing.firstEquipmentMinute, Is.InRange(0, 5));
            Assert.That(pacing.firstLevelMinute, Is.InRange(0, 5));
            Assert.That(pacing.firstBossMinute, Is.InRange(5, 15));
            Assert.That(pacing.firstBuildDirectionMinute, Is.InRange(5, 15));
            Assert.That(pacing.firstRealmBreakthroughMinute, Is.InRange(15, 30));
            Assert.That(pacing.firstSpiritualRootMinute, Is.InRange(15, 30));
            Assert.That(pacing.cultivationUnlockMinute, Is.InRange(30, 60));
            Assert.That(pacing.rankingUnlockMinute, Is.InRange(60, 120));
            Assert.That(pacing.realmPackEntryMinute, Is.InRange(60, 120));
            Assert.That(pacing.clearChapterMinute, Is.LessThanOrEqualTo(120));
            Assert.That(120 / pacing.growthPulseMinutes, Is.GreaterThanOrEqualTo(24));
            Assert.That(120 * 60 / pacing.equipmentDropSeconds, Is.GreaterThanOrEqualTo(200));
        }

        [Test]
        public void TwoHourVirtualSession_DrivesDropsStagesGrowthAndCompletion()
        {
            var config = DemoPacingLoader.Load(new ResourcesConfigSource());
            var session = new DemoPacingSession(config);
            var rewards = 0;
            for (var second = 0; second < config.durationMinutes * 60; second++)
            {
                session.Advance(1);
                while (session.TryConsumeBattleReward()) rewards++;
                if (second + 1 < config.firstBossMinute * 60) Assert.That(session.CurrentStageNumber, Is.LessThan(10));
            }
            Assert.That(session.IsComplete, Is.True);
            Assert.That(session.CurrentStageNumber, Is.EqualTo(10));
            Assert.That(session.ExpectedGrowthPulses, Is.EqualTo(config.durationMinutes / config.growthPulseMinutes));
            Assert.That(rewards, Is.EqualTo(1 + (config.durationMinutes * 60 - config.firstEquipmentMinute * 60) / config.equipmentDropSeconds));
            Assert.That(session.GeneratedRewardWindows, Is.EqualTo(rewards));
            Assert.That(session.ConsumedRewardWindows, Is.EqualTo(rewards));
            Assert.That(session.PendingRewards, Is.Zero);
            Assert.That(session.Reached(config.firstSpiritualRootMinute), Is.True);
            Assert.That(session.Reached(config.realmPackEntryMinute), Is.True);
            Assert.That(session.Reached(config.clearChapterMinute), Is.True);
        }
    }
}
