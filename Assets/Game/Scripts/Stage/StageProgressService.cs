using System;
using System.Collections.Generic;
using ImmortalLoot.Config;

namespace ImmortalLoot.Stage
{
    [Serializable]
    public sealed class StageProgressState
    {
        public List<string> ClearedStageIds = new List<string>();
        public int CycleIndex = 1;
        public double CycleElapsedSeconds;
    }

    public readonly struct StageClearResult
    {
        public bool IsFirstClear { get; }
        public string ClearedStageId { get; }
        public string UnlockedStageId { get; }
        public string RewardDropTableId { get; }
        public string FirstClearDropTableId { get; }

        public StageClearResult(bool firstClear, string clearedStageId, string unlockedStageId, string rewardDropTableId, string firstClearDropTableId)
        {
            IsFirstClear = firstClear; ClearedStageId = clearedStageId; UnlockedStageId = unlockedStageId;
            RewardDropTableId = rewardDropTableId; FirstClearDropTableId = firstClearDropTableId;
        }
    }

    public sealed class StageProgressService
    {
        private readonly GameConfigCatalog _catalog;
        private readonly StageProgressState _state;
        private readonly List<StageConfig> _orderedStages;
        private readonly HashSet<string> _cleared;

        public StageProgressService(GameConfigCatalog catalog, StageProgressState state)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _orderedStages = new List<StageConfig>(_catalog.Stages.Values);
            _orderedStages.Sort((left, right) => left.Chapter != right.Chapter
                ? left.Chapter.CompareTo(right.Chapter)
                : left.StageNumber.CompareTo(right.StageNumber));
            _cleared = new HashSet<string>(_state.ClearedStageIds, StringComparer.Ordinal);
        }

        public bool CanEnter(string stageId)
        {
            var index = IndexOf(stageId);
            return index == 0 || _cleared.Contains(stageId) || _cleared.Contains(_orderedStages[index - 1].Id);
        }

        public StageClearResult RecordVictory(string stageId)
        {
            var index = IndexOf(stageId);
            if (!CanEnter(stageId)) throw new InvalidOperationException($"Stage '{stageId}' is locked.");
            var firstClear = _cleared.Add(stageId);
            if (firstClear) _state.ClearedStageIds.Add(stageId);
            var stage = _orderedStages[index];
            var next = index + 1 < _orderedStages.Count ? _orderedStages[index + 1].Id : string.Empty;
            return new StageClearResult(firstClear, stage.Id, next, stage.DropTableId, firstClear ? stage.FirstClearDropTableId : string.Empty);
        }

        private int IndexOf(string stageId)
        {
            for (var i = 0; i < _orderedStages.Count; i++) if (_orderedStages[i].Id == stageId) return i;
            throw new ConfigException($"Stage '{stageId}' was not found.");
        }
    }
}
