using System;
using System.Collections.Generic;
using ImmortalLoot.Config;

namespace ImmortalLoot.Stage
{
    public readonly struct VictoryDrivenStageTransition
    {
        public StageClearResult ClearResult { get; }
        public string CompletedStageId { get; }
        public string NextStageId { get; }
        public bool Advanced { get; }
        public bool CompletedChapter { get; }

        public VictoryDrivenStageTransition(
            StageClearResult clearResult,
            string completedStageId,
            string nextStageId,
            bool advanced,
            bool completedChapter)
        {
            ClearResult = clearResult;
            CompletedStageId = completedStageId;
            NextStageId = nextStageId;
            Advanced = advanced;
            CompletedChapter = completedChapter;
        }
    }

    public sealed class VictoryDrivenStageLoop
    {
        private const string FallbackStageId = "stage_1_1";

        private readonly GameConfigCatalog _catalog;
        private readonly StageProgressState _state;
        private readonly List<StageConfig> _orderedStages;
        private readonly StageProgressService _progress;
        private string _currentStageId;

        public string CurrentStageId => _currentStageId;
        public int CurrentStageNumber => CurrentStage.StageNumber;
        public StageConfig CurrentStage => _catalog.Stages[_currentStageId];
        public int DefeatsOnCurrentStage { get; private set; }

        public VictoryDrivenStageLoop(GameConfigCatalog catalog, StageProgressState state, string currentStageId)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _state.ClearedStageIds ??= new List<string>();
            _orderedStages = new List<StageConfig>(_catalog.Stages.Values);
            _orderedStages.Sort(CompareStages);

            if (!_catalog.Stages.TryGetValue(currentStageId ?? string.Empty, out var current))
            {
                if (!_catalog.Stages.TryGetValue(FallbackStageId, out current))
                    throw new ConfigException($"Fallback stage '{FallbackStageId}' was not found.");
            }

            _currentStageId = current.Id;
            ReconcilePredecessors(current.Id);
            _progress = new StageProgressService(_catalog, _state);
        }

        public VictoryDrivenStageTransition RecordVictory(int maximumUnlockedStageNumber)
        {
            var completed = CurrentStage;
            var clearResult = _progress.RecordVictory(completed.Id);
            DefeatsOnCurrentStage = 0;

            if (IsFinalBoss(completed))
            {
                _currentStageId = FirstStageIdInChapter(completed.Chapter);
                return new VictoryDrivenStageTransition(
                    clearResult, completed.Id, _currentStageId, advanced: true, completedChapter: true);
            }

            var nextStageId = completed.Id;
            if (!string.IsNullOrEmpty(clearResult.UnlockedStageId) &&
                _catalog.Stages.TryGetValue(clearResult.UnlockedStageId, out var candidate) &&
                candidate.StageNumber <= maximumUnlockedStageNumber)
            {
                nextStageId = candidate.Id;
            }

            _currentStageId = nextStageId;
            return new VictoryDrivenStageTransition(
                clearResult,
                completed.Id,
                _currentStageId,
                advanced: !string.Equals(completed.Id, _currentStageId, StringComparison.Ordinal),
                completedChapter: false);
        }

        public void RecordDefeat()
        {
            if (DefeatsOnCurrentStage < int.MaxValue) DefeatsOnCurrentStage++;
        }

        private void ReconcilePredecessors(string currentStageId)
        {
            var cleared = new HashSet<string>(_state.ClearedStageIds, StringComparer.Ordinal);
            for (var i = 0; i < _orderedStages.Count; i++)
            {
                var stage = _orderedStages[i];
                if (string.Equals(stage.Id, currentStageId, StringComparison.Ordinal)) return;
                if (cleared.Add(stage.Id)) _state.ClearedStageIds.Add(stage.Id);
            }
            throw new ConfigException($"Stage '{currentStageId}' was not found in the ordered stage catalog.");
        }

        private bool IsFinalBoss(StageConfig stage)
        {
            if (!stage.IsBossStage) return false;
            foreach (var candidate in _orderedStages)
                if (candidate.Chapter == stage.Chapter && candidate.StageNumber > stage.StageNumber)
                    return false;
            return true;
        }

        private string FirstStageIdInChapter(int chapter)
        {
            foreach (var stage in _orderedStages)
                if (stage.Chapter == chapter)
                    return stage.Id;
            throw new ConfigException($"Chapter '{chapter}' has no stages.");
        }

        private static int CompareStages(StageConfig left, StageConfig right)
        {
            var chapter = left.Chapter.CompareTo(right.Chapter);
            if (chapter != 0) return chapter;
            var number = left.StageNumber.CompareTo(right.StageNumber);
            return number != 0 ? number : string.CompareOrdinal(left.Id, right.Id);
        }
    }
}
