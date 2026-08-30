using System;
using System.Collections.Generic;
using ImmortalLoot.Character;
using ImmortalLoot.Config;
using ImmortalLoot.Core;
using ImmortalLoot.Realm;

namespace ImmortalLoot.SpiritualRoot
{
    [Serializable]
    public sealed class SpiritualRootProgress
    {
        public string RootId;
        public int Level;
    }

    [Serializable]
    public sealed class SpiritualRootGrantRecord
    {
        public string TribulationToken;
        public string RootId;
        public int NewLevel;
    }

    [Serializable]
    public sealed class SpiritualRootState
    {
        public List<SpiritualRootProgress> Roots = new List<SpiritualRootProgress>();
        public List<SpiritualRootGrantRecord> GrantRecords = new List<SpiritualRootGrantRecord>();
    }

    public enum SpiritualRootGrantStatus { Granted, AlreadyGranted, AllRootsAtMaximum }

    public readonly struct SpiritualRootGrantResult
    {
        public SpiritualRootGrantStatus Status { get; }
        public string RootId { get; }
        public int NewLevel { get; }
        public SpiritualRootGrantResult(SpiritualRootGrantStatus status, string rootId, int newLevel)
        { Status = status; RootId = rootId ?? string.Empty; NewLevel = newLevel; }
    }

    public interface ISpiritualRootResetService
    {
        void ResetAll(SpiritualRootState state);
    }

    public interface ISpiritualRootRerollService
    {
        SpiritualRootGrantResult Reroll(string grantToken);
    }

    public sealed class SpiritualRootService
    {
        private readonly GameConfigCatalog _catalog;
        private readonly SpiritualRootState _state;
        private readonly IRandomSource _random;

        public SpiritualRootService(GameConfigCatalog catalog, SpiritualRootState state, IRandomSource random)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            foreach (var config in _catalog.SpiritualRoots.Values)
                if (_state.Roots.Find(value => value.RootId == config.Id) == null)
                    _state.Roots.Add(new SpiritualRootProgress { RootId = config.Id });
        }

        public SpiritualRootGrantResult GrantFromTribulation(string tribulationToken)
        {
            if (string.IsNullOrWhiteSpace(tribulationToken)) throw new ArgumentException("Tribulation token is required.", nameof(tribulationToken));
            var previous = _state.GrantRecords.Find(value => value.TribulationToken == tribulationToken);
            if (previous != null) return new SpiritualRootGrantResult(SpiritualRootGrantStatus.AlreadyGranted, previous.RootId, previous.NewLevel);
            var candidates = new List<SpiritualRootProgress>();
            foreach (var progress in _state.Roots)
                if (_catalog.SpiritualRoots.TryGetValue(progress.RootId, out var config) && progress.Level < config.MaxLevel)
                    candidates.Add(progress);
            if (candidates.Count == 0) return new SpiritualRootGrantResult(SpiritualRootGrantStatus.AllRootsAtMaximum, string.Empty, 0);
            var selected = candidates[_random.Range(0, candidates.Count)];
            selected.Level++;
            _state.GrantRecords.Add(new SpiritualRootGrantRecord
            {
                TribulationToken = tribulationToken, RootId = selected.RootId, NewLevel = selected.Level
            });
            return new SpiritualRootGrantResult(SpiritualRootGrantStatus.Granted, selected.RootId, selected.Level);
        }

        public int GetLevel(ElementType element)
        {
            foreach (var config in _catalog.SpiritualRoots.Values)
                if (config.Element == element) return _state.Roots.Find(value => value.RootId == config.Id)?.Level ?? 0;
            return 0;
        }

        public float GetSkillBonus(ElementType element)
        {
            foreach (var config in _catalog.SpiritualRoots.Values)
                if (config.Element == element) return GetLevel(element) * config.SkillBonusPerLevel;
            return 0f;
        }
    }

    public sealed class SpiritualRootStatProvider : IStatModifierProvider
    {
        private readonly GameConfigCatalog _catalog;
        private readonly SpiritualRootState _state;
        public SpiritualRootStatProvider(GameConfigCatalog catalog, SpiritualRootState state) { _catalog = catalog; _state = state; }

        public IEnumerable<StatModifier> GetModifiers()
        {
            foreach (var progress in _state.Roots)
            {
                if (progress.Level <= 0 || !_catalog.SpiritualRoots.TryGetValue(progress.RootId, out var config)) continue;
                yield return new StatModifier(DamageStat(config.Element), StatModifierType.Flat, progress.Level * config.DamageBonusPerLevel, $"spiritual_root:{progress.RootId}:damage");
                yield return new StatModifier(ResistanceStat(config.Element), StatModifierType.Flat, progress.Level * config.ResistanceBonusPerLevel, $"spiritual_root:{progress.RootId}:resistance");
            }
        }

        private static StatId DamageStat(ElementType element)
        {
            switch (element)
            {
                case ElementType.Metal: return StatId.MetalDamage;
                case ElementType.Wood: return StatId.WoodDamage;
                case ElementType.Water: return StatId.WaterDamage;
                case ElementType.Fire: return StatId.FireDamage;
                case ElementType.Earth: return StatId.EarthDamage;
                case ElementType.Wind: return StatId.WindDamage;
                case ElementType.Lightning: return StatId.LightningDamage;
                case ElementType.Yin: return StatId.YinDamage;
                case ElementType.Yang: return StatId.YangDamage;
                default: throw new ArgumentOutOfRangeException(nameof(element), element, null);
            }
        }

        private static StatId ResistanceStat(ElementType element)
        {
            switch (element)
            {
                case ElementType.Metal: return StatId.MetalResistance;
                case ElementType.Wood: return StatId.WoodResistance;
                case ElementType.Water: return StatId.WaterResistance;
                case ElementType.Fire: return StatId.FireResistance;
                case ElementType.Earth: return StatId.EarthResistance;
                case ElementType.Wind: return StatId.WindResistance;
                case ElementType.Lightning: return StatId.LightningResistance;
                case ElementType.Yin: return StatId.YinResistance;
                case ElementType.Yang: return StatId.YangResistance;
                default: throw new ArgumentOutOfRangeException(nameof(element), element, null);
            }
        }
    }

    public readonly struct TribulationResolution
    {
        public RealmBreakthroughResult Realm { get; }
        public SpiritualRootGrantResult? SpiritualRoot { get; }
        public TribulationResolution(RealmBreakthroughResult realm, SpiritualRootGrantResult? spiritualRoot)
        { Realm = realm; SpiritualRoot = spiritualRoot; }
    }

    public sealed class TribulationRewardCoordinator
    {
        private readonly RealmProgressionService _realm;
        private readonly SpiritualRootService _roots;
        public TribulationRewardCoordinator(RealmProgressionService realm, SpiritualRootService roots) { _realm = realm; _roots = roots; }

        public TribulationResolution Resolve(string token, bool victory)
        {
            var result = _realm.ResolveTribulation(token, victory);
            if (result.Status != RealmBreakthroughStatus.RealmAdvanced) return new TribulationResolution(result, null);
            return new TribulationResolution(result, _roots.GrantFromTribulation(token));
        }
    }
}
