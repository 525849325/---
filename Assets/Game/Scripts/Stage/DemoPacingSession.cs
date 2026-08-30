using System;

namespace ImmortalLoot.Stage
{
    public sealed class DemoPacingSession
    {
        private readonly DemoPacingConfig _config;
        private double _nextRewardSecond;
        private int _pendingRewards;
        public int GeneratedRewardWindows { get; private set; }
        public int ConsumedRewardWindows { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public int ElapsedMinutes => (int)(ElapsedSeconds / 60d);
        public bool IsComplete => ElapsedSeconds >= _config.durationMinutes * 60d;

        public DemoPacingSession(DemoPacingConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _nextRewardSecond = config.firstEquipmentMinute * 60d;
        }

        public void Advance(double unscaledSeconds)
        {
            if (unscaledSeconds < 0) throw new ArgumentOutOfRangeException(nameof(unscaledSeconds));
            ElapsedSeconds = Math.Min(_config.durationMinutes * 60d, ElapsedSeconds + unscaledSeconds);
            while (_nextRewardSecond <= ElapsedSeconds)
            {
                _pendingRewards++;
                GeneratedRewardWindows++;
                _nextRewardSecond += _config.equipmentDropSeconds;
            }
        }

        public bool TryConsumeBattleReward()
        {
            if (_pendingRewards <= 0) return false;
            _pendingRewards--;
            ConsumedRewardWindows++;
            return true;
        }

        public int CurrentStageNumber
        {
            get
            {
                var bossSecond = Math.Max(1, _config.firstBossMinute * 60d);
                return Math.Clamp(1 + (int)(ElapsedSeconds / (bossSecond / 9d)), 1, 10);
            }
        }

        public bool Reached(int minute) => ElapsedSeconds >= minute * 60d;
        public int ExpectedGrowthPulses => ElapsedMinutes / _config.growthPulseMinutes;
        public int PendingRewards => _pendingRewards;
    }
}
