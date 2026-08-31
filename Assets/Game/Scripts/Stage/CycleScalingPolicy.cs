using System;

namespace ImmortalLoot.Stage
{
    public sealed class CycleScalingPolicy
    {
        private readonly int _enemyGrowthPercent;
        private readonly int _rewardGrowthPercent;
        private readonly int _maxScaledCycle;

        public CycleScalingPolicy(DemoPacingConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.enemyStatGrowthPercentPerCycle < 10 || config.enemyStatGrowthPercentPerCycle > 60 ||
                config.rewardGrowthPercentPerCycle < 1 || config.rewardGrowthPercentPerCycle > 25 ||
                config.maxScaledCycle < 2 || config.maxScaledCycle > 20)
                throw new ArgumentException("Cycle scaling values are outside the supported release bounds.", nameof(config));
            _enemyGrowthPercent = config.enemyStatGrowthPercentPerCycle;
            _rewardGrowthPercent = config.rewardGrowthPercentPerCycle;
            _maxScaledCycle = config.maxScaledCycle;
        }

        public float EnemyMultiplier(int cycleIndex)
        {
            var growthSteps = GrowthSteps(cycleIndex);
            return 1f + growthSteps * _enemyGrowthPercent / 100f;
        }

        public long ScaleReward(long baseValue, int cycleIndex)
        {
            if (baseValue <= 0) return 0;
            var percent = 100m + GrowthSteps(cycleIndex) * _rewardGrowthPercent;
            var scaled = decimal.Floor(baseValue * percent / 100m);
            return scaled >= long.MaxValue ? long.MaxValue : decimal.ToInt64(scaled);
        }

        public float RewardMultiplier(int cycleIndex) =>
            1f + GrowthSteps(cycleIndex) * _rewardGrowthPercent / 100f;

        private int GrowthSteps(int cycleIndex)
        {
            var normalizedCycle = Math.Clamp(cycleIndex, 1, _maxScaledCycle);
            return normalizedCycle - 1;
        }
    }
}
