using System;
using System.Collections.Generic;
using ImmortalLoot.Config;
using ImmortalLoot.Core;
using ImmortalLoot.Equipment;

namespace ImmortalLoot.Debugging
{
    [Serializable]
    public sealed class DebugGameState
    {
        public long SoftCurrency;
        public long PremiumCurrency;
        public long Exp;
        public int Level = 1;
        public int RealmOrder = 1;
        public int RealmStage = 1;
        public DateTime LastOfflineTimeUtc = DateTime.UtcNow;
        public List<EquipmentInstance> Equipment = new List<EquipmentInstance>();
        public List<string> UnlockedStages = new List<string>();
        public List<string> LearnedMethods = new List<string>();
        public List<DebugRootLevel> Roots = new List<DebugRootLevel>();
        public int MockPaymentCount;
    }

    [Serializable] public sealed class DebugRootLevel { public string RootId; public int Level; }

    public sealed class GameDebugService
    {
        private readonly DebugGameState _state;
        private readonly GameConfigCatalog _catalog;
        private readonly EquipmentGenerator _equipment;
        public DebugGameState State => _state;

        public GameDebugService(DebugGameState state, GameConfigCatalog catalog, IRandomSource random)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _equipment = new EquipmentGenerator(random ?? throw new ArgumentNullException(nameof(random)), catalog);
        }

        public void AddSoftCurrency(long amount) => _state.SoftCurrency = checked(_state.SoftCurrency + Positive(amount));
        public void AddPremiumCurrency(long amount) => _state.PremiumCurrency = checked(_state.PremiumCurrency + Positive(amount));
        public void AddExp(long amount) => _state.Exp = checked(_state.Exp + Positive(amount));
        public void LevelUp(int levels = 1) => _state.Level = checked(_state.Level + (int)Positive(levels));
        public void Breakthrough() { if (_state.RealmStage < 10) _state.RealmStage++; else { _state.RealmOrder = Math.Min(10, _state.RealmOrder + 1); _state.RealmStage = 1; } }
        public void UnlockStage(string stageId) { if (!_catalog.Stages.ContainsKey(stageId)) throw new ConfigException("Stage was not found."); if (!_state.UnlockedStages.Contains(stageId)) _state.UnlockedStages.Add(stageId); }
        public void LearnMethod(string methodId) { if (!_catalog.CultivationMethods.ContainsKey(methodId)) throw new ConfigException("Method was not found."); if (!_state.LearnedMethods.Contains(methodId)) _state.LearnedMethods.Add(methodId); }
        public void SetRoot(string rootId, int level) { var config = _catalog.SpiritualRoots[rootId]; var root = _state.Roots.Find(value => value.RootId == rootId); if (root == null) { root = new DebugRootLevel { RootId = rootId }; _state.Roots.Add(root); } root.Level = Math.Clamp(level, 0, config.MaxLevel); }
        public void SimulateOffline8Hours(DateTime nowUtc) => _state.LastOfflineTimeUtc = nowUtc.AddHours(-8);
        public void SimulatePayment(long premiumGrant) { AddPremiumCurrency(premiumGrant); _state.MockPaymentCount++; }

        public EquipmentInstance GenerateEquipment(string baseId, int level, EquipmentQuality quality, string forcedAffixId = null)
        {
            var definition = _catalog.GetEquipment(baseId);
            var item = _equipment.Generate(definition, level, quality, "GM");
            if (!string.IsNullOrWhiteSpace(forcedAffixId))
            {
                var affix = definition.AffixPool.Find(value => value.Id == forcedAffixId) ?? throw new ConfigException("Affix is not legal for this equipment.");
                var roll = new AffixRoll { AffixId = affix.Id, DisplayName = affix.DisplayName, Value = affix.MaxValue, Stat = affix.Stat, ModifierType = affix.ModifierType };
                if (item.Affixes.Count == 0) item.Affixes.Add(roll); else item.Affixes[0] = roll;
            }
            _state.Equipment.Add(item);
            return item;
        }

        public void ClearSave()
        {
            _state.SoftCurrency = _state.PremiumCurrency = _state.Exp = 0;
            _state.Level = _state.RealmOrder = _state.RealmStage = 1;
            _state.Equipment.Clear(); _state.UnlockedStages.Clear(); _state.LearnedMethods.Clear(); _state.Roots.Clear();
            _state.MockPaymentCount = 0;
        }

        private static long Positive(long value) => value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
}
