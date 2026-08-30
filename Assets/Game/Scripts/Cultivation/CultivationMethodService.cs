using System;
using System.Collections.Generic;
using ImmortalLoot.Character;
using ImmortalLoot.Config;
using ImmortalLoot.Realm;

namespace ImmortalLoot.Cultivation
{
    [Serializable]
    public sealed class CultivationMethodState
    {
        public List<string> LearnedMethodIds = new List<string>();
        public string PrimaryMethodId;
        public string[] AuxiliaryMethodIds = new string[2];
    }

    public sealed class CultivationMethodService
    {
        private readonly GameConfigCatalog _catalog;
        private readonly RealmProgressState _realm;
        private readonly CultivationMethodState _state;

        public CultivationMethodService(GameConfigCatalog catalog, RealmProgressState realm, CultivationMethodState state)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _realm = realm ?? throw new ArgumentNullException(nameof(realm));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            if (_state.AuxiliaryMethodIds == null || _state.AuxiliaryMethodIds.Length != 2) _state.AuxiliaryMethodIds = new string[2];
        }

        public bool Learn(string methodId)
        {
            var method = Get(methodId);
            if (!IsUnlocked(method)) throw new InvalidOperationException($"Cultivation method '{methodId}' is locked by realm.");
            if (_state.LearnedMethodIds.Contains(methodId)) return false;
            _state.LearnedMethodIds.Add(methodId);
            return true;
        }

        public void EquipPrimary(string methodId)
        {
            var method = RequireLearned(methodId);
            if (!method.IsPrimary) throw new InvalidOperationException("Only primary cultivation methods can use the primary slot.");
            _state.PrimaryMethodId = methodId;
        }

        public void EquipAuxiliary(int slot, string methodId)
        {
            if (slot < 0 || slot >= _state.AuxiliaryMethodIds.Length) throw new ArgumentOutOfRangeException(nameof(slot));
            var method = RequireLearned(methodId);
            if (method.IsPrimary) throw new InvalidOperationException("Primary cultivation methods cannot use auxiliary slots.");
            for (var i = 0; i < _state.AuxiliaryMethodIds.Length; i++)
                if (i != slot && _state.AuxiliaryMethodIds[i] == methodId)
                    throw new InvalidOperationException("The same auxiliary method cannot be equipped twice.");
            _state.AuxiliaryMethodIds[slot] = methodId;
        }

        public IEnumerable<CultivationMethodConfig> GetActiveMethods()
        {
            if (!string.IsNullOrWhiteSpace(_state.PrimaryMethodId)) yield return Get(_state.PrimaryMethodId);
            foreach (var id in _state.AuxiliaryMethodIds)
                if (!string.IsNullOrWhiteSpace(id)) yield return Get(id);
        }

        public float GetSkillMultiplierBonus(ElementType element) => Sum(method => method.Element == element ? method.SkillMultiplierBonus : 0f);
        public float GetAfkMultiplier() => 1f + Sum(method => method.AfkEfficiencyBonus);
        public float GetBossDamageBonus() => Sum(method => method.BossDamageBonus);
        public float GetLowHealthDamageBonus() => Sum(method => method.LowHealthDamageBonus);

        private float Sum(Func<CultivationMethodConfig, float> selector)
        {
            var value = 0f;
            foreach (var method in GetActiveMethods()) value += selector(method);
            return value;
        }

        private bool IsUnlocked(CultivationMethodConfig method) =>
            _catalog.Realms[_realm.RealmId].Order >= _catalog.Realms[method.UnlockRealmId].Order;

        private CultivationMethodConfig RequireLearned(string id)
        {
            var method = Get(id);
            if (!_state.LearnedMethodIds.Contains(id)) throw new InvalidOperationException($"Cultivation method '{id}' has not been learned.");
            return method;
        }

        private CultivationMethodConfig Get(string id)
        {
            if (!_catalog.CultivationMethods.TryGetValue(id, out var method)) throw new ConfigException($"Cultivation method '{id}' was not found.");
            return method;
        }
    }

    public sealed class CultivationMethodStatProvider : IStatModifierProvider
    {
        private readonly CultivationMethodService _service;
        public CultivationMethodStatProvider(CultivationMethodService service) => _service = service;

        public IEnumerable<StatModifier> GetModifiers()
        {
            foreach (var method in _service.GetActiveMethods())
            {
                var source = $"cultivation:{method.Id}";
                if (method.AttackBonus != 0) yield return new StatModifier(StatId.Attack, StatModifierType.AdditivePercent, method.AttackBonus, source);
                if (method.HealthBonus != 0) yield return new StatModifier(StatId.HP, StatModifierType.AdditivePercent, method.HealthBonus, source);
                if (method.LifeStealBonus != 0) yield return new StatModifier(StatId.LifeSteal, StatModifierType.Flat, method.LifeStealBonus, source);
                if (method.CritBonus != 0) yield return new StatModifier(StatId.CritRate, StatModifierType.Flat, method.CritBonus, source);
                if (method.ElementDamageBonus != 0) yield return new StatModifier(ElementStat(method.Element), StatModifierType.Flat, method.ElementDamageBonus, source);
            }
        }

        private static StatId ElementStat(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return StatId.FireDamage;
                case ElementType.Lightning: return StatId.LightningDamage;
                case ElementType.Yin: return StatId.YinDamage;
                case ElementType.Metal: return StatId.MetalDamage;
                case ElementType.Wood: return StatId.WoodDamage;
                case ElementType.Water: return StatId.WaterDamage;
                case ElementType.Earth: return StatId.EarthDamage;
                case ElementType.Wind: return StatId.WindDamage;
                case ElementType.Yang: return StatId.YangDamage;
                default: throw new ArgumentOutOfRangeException(nameof(element), element, null);
            }
        }
    }
}
