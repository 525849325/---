using System;
using ImmortalLoot.Config;
using UnityEngine;

namespace ImmortalLoot.Stage
{
    [Serializable]
    public sealed class DemoPacingConfig
    {
        public int schemaVersion; public int durationMinutes; public int growthPulseMinutes; public int equipmentDropSeconds;
        public int firstEquipmentMinute; public int firstLevelMinute; public int affixUnlockMinute; public int firstBossMinute;
        public int firstBuildDirectionMinute; public int firstRealmBreakthroughMinute; public int firstSpiritualRootMinute;
        public int cultivationUnlockMinute; public int higherQualityMinute; public int rankingUnlockMinute;
        public int realmPackEntryMinute; public int clearChapterMinute;
    }

    public static class DemoPacingLoader
    {
        public static DemoPacingConfig Load(IConfigSource source)
        {
            var value = JsonUtility.FromJson<DemoPacingConfig>(source.LoadText("demo_pacing"));
            if (value == null || value.schemaVersion != 1 || value.durationMinutes != 120 || value.growthPulseMinutes < 3 || value.growthPulseMinutes > 5 || value.equipmentDropSeconds <= 0)
                throw new ConfigException("Demo pacing config is invalid.");
            var timeline = new[] { value.firstEquipmentMinute, value.firstLevelMinute, value.affixUnlockMinute, value.firstBossMinute,
                value.firstBuildDirectionMinute, value.firstRealmBreakthroughMinute, value.firstSpiritualRootMinute, value.cultivationUnlockMinute,
                value.higherQualityMinute, value.rankingUnlockMinute, value.realmPackEntryMinute, value.clearChapterMinute };
            for (var i = 0; i < timeline.Length; i++)
                if (timeline[i] < 0 || timeline[i] > value.durationMinutes || i > 0 && timeline[i] < timeline[i - 1]) throw new ConfigException("Demo milestone timeline is invalid.");
            return value;
        }
    }
}
