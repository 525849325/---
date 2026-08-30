using System;
using ImmortalLoot.Config;
using ImmortalLoot.Core;
using UnityEngine;

namespace ImmortalLoot.AFK
{
    [Serializable]
    public sealed class AfkConfig
    {
        public int MaximumOfflineHours;
        public float ExperiencePerMinute;
        public float SoftCurrencyPerMinute;
        public float MaterialPerMinute;
        public float MinutesPerEquipmentRoll;
        public int QuickAfkHours;
        public int FreeQuickAfkPerDay;
    }

    public static class AfkConfigLoader
    {
        public static AfkConfig Load(IConfigSource source)
        {
            var file = JsonUtility.FromJson<AfkFile>(source.LoadText("afk"));
            if (file == null || file.schemaVersion != 1 || file.maximumOfflineHours <= 0 || file.experiencePerMinute < 0 || file.softCurrencyPerMinute < 0 || file.materialPerMinute < 0 || file.minutesPerEquipmentRoll <= 0 || file.quickAfkHours <= 0 || file.freeQuickAfkPerDay < 0)
                throw new ConfigException("AFK config is invalid.");
            return new AfkConfig
            {
                MaximumOfflineHours = file.maximumOfflineHours,
                ExperiencePerMinute = file.experiencePerMinute,
                SoftCurrencyPerMinute = file.softCurrencyPerMinute,
                MaterialPerMinute = file.materialPerMinute,
                MinutesPerEquipmentRoll = file.minutesPerEquipmentRoll,
                QuickAfkHours = file.quickAfkHours,
                FreeQuickAfkPerDay = file.freeQuickAfkPerDay
            };
        }

        [Serializable]
        private sealed class AfkFile
        {
            public int schemaVersion;
            public int maximumOfflineHours;
            public float experiencePerMinute;
            public float softCurrencyPerMinute;
            public float materialPerMinute;
            public float minutesPerEquipmentRoll;
            public int quickAfkHours;
            public int freeQuickAfkPerDay;
        }
    }

    public readonly struct AfkReward
    {
        public long EffectiveSeconds { get; }
        public long Experience { get; }
        public long SoftCurrency { get; }
        public int MaterialCount { get; }
        public int EquipmentRolls { get; }

        public AfkReward(long seconds, long experience, long softCurrency, int materialCount, int equipmentRolls)
        { EffectiveSeconds = seconds; Experience = experience; SoftCurrency = softCurrency; MaterialCount = materialCount; EquipmentRolls = equipmentRolls; }
    }

    public sealed class AfkRewardCalculator
    {
        private readonly AfkConfig _config;
        public AfkRewardCalculator(AfkConfig config) => _config = config ?? throw new ArgumentNullException(nameof(config));

        public AfkReward Calculate(long offlineSeconds, float stageRate, float playerBonus = 1f, float activityBonus = 1f, int extraMaximumHours = 0)
        {
            var cap = TimeSpan.FromHours(_config.MaximumOfflineHours + Math.Max(0, extraMaximumHours)).TotalSeconds;
            var seconds = (long)Math.Max(0, Math.Min(cap, offlineSeconds));
            var minutes = seconds / 60d;
            var multiplier = Math.Max(0f, stageRate) * Math.Max(0f, playerBonus) * Math.Max(0f, activityBonus);
            return new AfkReward(
                seconds,
                (long)Math.Floor(_config.ExperiencePerMinute * minutes * multiplier),
                (long)Math.Floor(_config.SoftCurrencyPerMinute * minutes * multiplier),
                (int)Math.Floor(_config.MaterialPerMinute * minutes * multiplier),
                (int)Math.Floor(minutes * multiplier / _config.MinutesPerEquipmentRoll));
        }
    }

    [Serializable]
    public sealed class AfkState
    {
        public long LastOfflineUnixSeconds;
        public string QuickAfkUtcDate;
        public int QuickAfkUsedToday;
    }

    public sealed class AfkRewardService
    {
        private readonly AfkConfig _config;
        private readonly AfkRewardCalculator _calculator;
        private readonly AfkState _state;
        private readonly IServerClock _clock;

        public AfkRewardService(AfkConfig config, AfkState state, IServerClock clock)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _calculator = new AfkRewardCalculator(config);
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public AfkReward Preview(float stageRate, float playerBonus = 1f, float activityBonus = 1f, int extraMaximumHours = 0)
        {
            var now = new DateTimeOffset(_clock.UtcNow).ToUnixTimeSeconds();
            return _calculator.Calculate(now - _state.LastOfflineUnixSeconds, stageRate, playerBonus, activityBonus, extraMaximumHours);
        }

        public AfkReward Claim(float stageRate, float playerBonus = 1f, float activityBonus = 1f, int extraMaximumHours = 0)
        {
            var reward = Preview(stageRate, playerBonus, activityBonus, extraMaximumHours);
            _state.LastOfflineUnixSeconds = new DateTimeOffset(_clock.UtcNow).ToUnixTimeSeconds();
            return reward;
        }

        public AfkReward ClaimFreeQuickAfk(float stageRate, float playerBonus = 1f, float activityBonus = 1f)
        {
            RefreshQuickAfkDay();
            if (_state.QuickAfkUsedToday >= _config.FreeQuickAfkPerDay) throw new InvalidOperationException("No free Quick AFK claims remain today.");
            _state.QuickAfkUsedToday++;
            return _calculator.Calculate((long)TimeSpan.FromHours(_config.QuickAfkHours).TotalSeconds, stageRate, playerBonus, activityBonus);
        }

        private void RefreshQuickAfkDay()
        {
            var date = _clock.UtcNow.ToString("yyyy-MM-dd");
            if (_state.QuickAfkUtcDate == date) return;
            _state.QuickAfkUtcDate = date;
            _state.QuickAfkUsedToday = 0;
        }
    }
}
