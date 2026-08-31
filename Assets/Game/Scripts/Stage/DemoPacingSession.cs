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
        public double CycleElapsedSeconds { get; private set; }
        public int CycleIndex { get; private set; } = 1;
        public int ElapsedMinutes => (int)(ElapsedSeconds / 60d);
        public bool IsComplete => ElapsedSeconds >= _config.durationMinutes * 60d;

        public DemoPacingSession(DemoPacingConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _nextRewardSecond = config.firstEquipmentMinute * 60d;
        }

        public void Advance(double unscaledSeconds)
        {
            if (double.IsNaN(unscaledSeconds) || double.IsInfinity(unscaledSeconds) || unscaledSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(unscaledSeconds));
            var previousElapsed = ElapsedSeconds;
            ElapsedSeconds = Math.Min(double.MaxValue, ElapsedSeconds + unscaledSeconds);
            CycleElapsedSeconds += ElapsedSeconds - previousElapsed;
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

        public void Restore(double elapsedSeconds) => Restore(elapsedSeconds, elapsedSeconds, 1);

        public void Restore(double elapsedSeconds, double cycleElapsedSeconds, int cycleIndex)
        {
            ElapsedSeconds = NormalizeElapsed(elapsedSeconds, double.MaxValue);
            CycleIndex = Math.Max(1, cycleIndex);
            CycleElapsedSeconds = NormalizeElapsed(cycleElapsedSeconds, ElapsedSeconds);
            _pendingRewards = 0;
            GeneratedRewardWindows = 0;
            ConsumedRewardWindows = 0;
            var firstRewardSecond = _config.firstEquipmentMinute * 60d;
            if (ElapsedSeconds < firstRewardSecond) _nextRewardSecond = firstRewardSecond;
            else
            {
                var completedIntervals = Math.Floor((ElapsedSeconds - firstRewardSecond) / _config.equipmentDropSeconds) + 1d;
                _nextRewardSecond = firstRewardSecond + completedIntervals * _config.equipmentDropSeconds;
            }
        }

        public void BeginCycle(int cycleIndex)
        {
            if (cycleIndex != CycleIndex + 1)
                throw new InvalidOperationException($"Cannot begin cycle {cycleIndex} after cycle {CycleIndex}.");
            CycleIndex = cycleIndex;
            CycleElapsedSeconds = 0d;
        }

        public int CurrentStageNumber
        {
            get
            {
                var bossMinute = CycleIndex <= 1 ? _config.firstBossMinute : _config.repeatBossMinute;
                var bossSecond = Math.Max(1, bossMinute * 60d);
                return Math.Clamp(1 + (int)(CycleElapsedSeconds / (bossSecond / 9d)), 1, 10);
            }
        }

        public bool Reached(int minute) => ElapsedSeconds >= minute * 60d;
        public int ExpectedGrowthPulses => ElapsedMinutes / _config.growthPulseMinutes;
        public int PendingRewards => _pendingRewards;

        private static double NormalizeElapsed(double value, double maximum)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d) return 0d;
            return Math.Min(value, Math.Max(0d, maximum));
        }
    }
}
