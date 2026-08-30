using System;
using System.Collections.Generic;
using ImmortalLoot.Character;
using ImmortalLoot.Config;
using ImmortalLoot.Core;
using UnityEngine;

namespace ImmortalLoot.Realm
{
    [Serializable]
    public sealed class RealmFormulaConfig
    {
        public float MinorCostScale;
        public float MinorExpScale;
        public float MinorFailureLossRatio;
        public float TribulationFailureLossRatio;
        public int FailureCooldownSeconds;
    }

    public static class RealmFormulaLoader
    {
        public static RealmFormulaConfig Load(IConfigSource source)
        {
            var file = JsonUtility.FromJson<RealmFormulaFile>(source.LoadText("realm_formula"));
            if (file == null || file.schemaVersion != 1 || file.minorCostScale <= 0 || file.minorExpScale <= 0 || file.minorFailureLossRatio < 0 || file.minorFailureLossRatio > 1 || file.tribulationFailureLossRatio < 0 || file.tribulationFailureLossRatio > 1 || file.failureCooldownSeconds < 0)
                throw new ConfigException("Realm formula config is invalid.");
            return new RealmFormulaConfig
            {
                MinorCostScale = file.minorCostScale,
                MinorExpScale = file.minorExpScale,
                MinorFailureLossRatio = file.minorFailureLossRatio,
                TribulationFailureLossRatio = file.tribulationFailureLossRatio,
                FailureCooldownSeconds = file.failureCooldownSeconds
            };
        }

        [Serializable]
        private sealed class RealmFormulaFile
        {
            public int schemaVersion;
            public float minorCostScale;
            public float minorExpScale;
            public float minorFailureLossRatio;
            public float tribulationFailureLossRatio;
            public int failureCooldownSeconds;
        }
    }

    [Serializable]
    public sealed class PendingTribulation
    {
        public string Token;
        public string TargetRealmId;
        public long ReservedMaterial;
        public long RequiredExp;
    }

    [Serializable]
    public sealed class RealmProgressState
    {
        public string RealmId = "realm_body_tempering";
        public int RealmStage = 1;
        public int PlayerLevel = 1;
        public long Experience;
        public long CultivationExperience;
        public long BreakthroughMaterial;
        public long CooldownUntilUnixSeconds;
        public PendingTribulation PendingTribulation;
    }

    public enum RealmBreakthroughStatus
    {
        AdvancedStage, TribulationRequired, RealmAdvanced, Failed, RequirementsNotMet,
        CooldownActive, TrialAlreadyPending, InvalidTrialToken, MaximumRealm
    }

    public readonly struct RealmBreakthroughResult
    {
        public RealmBreakthroughStatus Status { get; }
        public string RealmId { get; }
        public int RealmStage { get; }
        public long MaterialSpent { get; }
        public long RequiredExperience { get; }
        public long CooldownUntilUnixSeconds { get; }
        public string TrialToken { get; }
        public string[] UnlockedSystems { get; }
        public int DailyDungeonBonus { get; }

        public RealmBreakthroughResult(RealmBreakthroughStatus status, RealmProgressState state, long materialSpent = 0, long requiredExperience = 0, string trialToken = "", string[] unlockedSystems = null, int dailyDungeonBonus = 0)
        {
            Status = status; RealmId = state.RealmId; RealmStage = state.RealmStage;
            MaterialSpent = materialSpent; RequiredExperience = requiredExperience;
            CooldownUntilUnixSeconds = state.CooldownUntilUnixSeconds; TrialToken = trialToken ?? string.Empty;
            UnlockedSystems = unlockedSystems ?? Array.Empty<string>(); DailyDungeonBonus = dailyDungeonBonus;
        }
    }

    public sealed class RealmProgressionService
    {
        private readonly GameConfigCatalog _catalog;
        private readonly RealmFormulaConfig _formula;
        private readonly RealmProgressState _state;
        private readonly IRandomSource _random;
        private readonly IServerClock _clock;
        private readonly List<RealmConfig> _ordered;

        public RealmProgressionService(GameConfigCatalog catalog, RealmFormulaConfig formula, RealmProgressState state, IRandomSource random, IServerClock clock)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _formula = formula ?? throw new ArgumentNullException(nameof(formula));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _ordered = new List<RealmConfig>(_catalog.Realms.Values);
            _ordered.Sort((left, right) => left.Order.CompareTo(right.Order));
            if (!_catalog.Realms.ContainsKey(_state.RealmId)) throw new ConfigException($"Realm state references missing realm '{_state.RealmId}'.");
        }

        public RealmBreakthroughResult BeginBreakthrough()
        {
            if (_state.PendingTribulation != null) return Result(RealmBreakthroughStatus.TrialAlreadyPending, trialToken: _state.PendingTribulation.Token);
            var now = NowUnix();
            if (_state.CooldownUntilUnixSeconds > now) return Result(RealmBreakthroughStatus.CooldownActive);
            var current = _catalog.Realms[_state.RealmId];
            if (_state.RealmStage < 1 || _state.RealmStage > current.StageCount) throw new InvalidOperationException("Realm stage is outside configured range.");
            if (_state.RealmStage == current.StageCount) return BeginMajorBreakthrough(current);

            var requiredExp = Math.Max(1L, (long)Math.Ceiling((double)current.RequiredExp * _formula.MinorExpScale * _state.RealmStage / current.StageCount));
            var requiredMaterial = Math.Max(1L, (long)Math.Ceiling((double)current.BreakthroughCost * _formula.MinorCostScale * _state.RealmStage / current.StageCount));
            if (_state.PlayerLevel < current.RequiredLevel || _state.CultivationExperience < requiredExp || _state.BreakthroughMaterial < requiredMaterial)
                return Result(RealmBreakthroughStatus.RequirementsNotMet, requiredExperience: requiredExp);
            _state.BreakthroughMaterial -= requiredMaterial;
            if (_random.Value() <= current.BreakthroughSuccessRate)
            {
                _state.CultivationExperience -= requiredExp;
                _state.RealmStage++;
                return Result(RealmBreakthroughStatus.AdvancedStage, requiredMaterial, requiredExp);
            }
            var spent = ApplyFailure(requiredMaterial, _formula.MinorFailureLossRatio);
            return Result(RealmBreakthroughStatus.Failed, spent, requiredExp);
        }

        public RealmBreakthroughResult ResolveTribulation(string token, bool victory)
        {
            var pending = _state.PendingTribulation;
            if (pending == null || string.IsNullOrWhiteSpace(token) || pending.Token != token)
                return Result(RealmBreakthroughStatus.InvalidTrialToken);
            if (string.IsNullOrWhiteSpace(pending.TargetRealmId) ||
                !_catalog.Realms.ContainsKey(pending.TargetRealmId) ||
                pending.ReservedMaterial <= 0 || pending.RequiredExp <= 0 ||
                _state.CultivationExperience < pending.RequiredExp)
                return Result(RealmBreakthroughStatus.InvalidTrialToken);
            _state.PendingTribulation = null;
            if (!victory)
            {
                var spent = ApplyFailure(pending.ReservedMaterial, _formula.TribulationFailureLossRatio);
                return Result(RealmBreakthroughStatus.Failed, spent, pending.RequiredExp);
            }
            var target = _catalog.Realms[pending.TargetRealmId];
            _state.CultivationExperience -= pending.RequiredExp;
            _state.RealmId = target.Id;
            _state.RealmStage = 1;
            _state.CooldownUntilUnixSeconds = 0;
            return Result(RealmBreakthroughStatus.RealmAdvanced, pending.ReservedMaterial, pending.RequiredExp,
                unlockedSystems: target.UnlockSystems, dailyDungeonBonus: target.DailyDungeonBonus);
        }

        private RealmBreakthroughResult BeginMajorBreakthrough(RealmConfig current)
        {
            var index = _ordered.FindIndex(value => value.Id == current.Id);
            if (index < 0 || index + 1 >= _ordered.Count) return Result(RealmBreakthroughStatus.MaximumRealm);
            var target = _ordered[index + 1];
            if (_state.PlayerLevel < target.RequiredLevel || _state.CultivationExperience < target.RequiredExp || _state.BreakthroughMaterial < target.BreakthroughCost)
                return Result(RealmBreakthroughStatus.RequirementsNotMet, requiredExperience: target.RequiredExp);
            _state.BreakthroughMaterial -= target.BreakthroughCost;
            var token = Guid.NewGuid().ToString("N");
            _state.PendingTribulation = new PendingTribulation
            {
                Token = token, TargetRealmId = target.Id, ReservedMaterial = target.BreakthroughCost, RequiredExp = target.RequiredExp
            };
            return Result(RealmBreakthroughStatus.TribulationRequired, target.BreakthroughCost, target.RequiredExp, token);
        }

        private long ApplyFailure(long reservedCost, float lossRatio)
        {
            var spent = (long)Math.Ceiling(reservedCost * lossRatio);
            _state.BreakthroughMaterial += reservedCost - spent;
            _state.CooldownUntilUnixSeconds = NowUnix() + _formula.FailureCooldownSeconds;
            return spent;
        }

        private RealmBreakthroughResult Result(RealmBreakthroughStatus status, long materialSpent = 0, long requiredExperience = 0, string trialToken = "", string[] unlockedSystems = null, int dailyDungeonBonus = 0) =>
            new RealmBreakthroughResult(status, _state, materialSpent, requiredExperience, trialToken, unlockedSystems, dailyDungeonBonus);

        private long NowUnix() => new DateTimeOffset(_clock.UtcNow).ToUnixTimeSeconds();
    }

    public sealed class RealmStatProvider : IStatModifierProvider
    {
        private readonly GameConfigCatalog _catalog;
        private readonly RealmProgressState _state;
        public RealmStatProvider(GameConfigCatalog catalog, RealmProgressState state) { _catalog = catalog; _state = state; }

        public IEnumerable<StatModifier> GetModifiers()
        {
            var current = _catalog.Realms[_state.RealmId];
            var total = 0f;
            foreach (var realm in _catalog.Realms.Values)
            {
                if (realm.Order < current.Order) total += realm.BaseStatBonus;
                else if (realm.Id == current.Id) total += realm.BaseStatBonus * Math.Max(1, _state.RealmStage) / realm.StageCount;
            }
            yield return new StatModifier(StatId.HP, StatModifierType.AdditivePercent, total, "realm");
            yield return new StatModifier(StatId.Attack, StatModifierType.AdditivePercent, total, "realm");
            yield return new StatModifier(StatId.Defense, StatModifierType.AdditivePercent, total, "realm");
        }
    }
}
