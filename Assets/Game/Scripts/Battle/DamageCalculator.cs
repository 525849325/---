using System;
using ImmortalLoot.Character;
using ImmortalLoot.Config;
using ImmortalLoot.Core;

namespace ImmortalLoot.Battle
{
    [Serializable]
    public sealed class DamageFormulaConfig
    {
        public float DefenseConstant = 100f;
        public float MinimumDamage = 1f;
        public float MaximumDamageReduction = 0.9f;
        public float MaximumElementResistance = 0.8f;
    }

    public readonly struct DamageRequest
    {
        public CharacterStats Attacker { get; }
        public CharacterStats Defender { get; }
        public float SkillMultiplier { get; }
        public ElementType Element { get; }
        public bool CanCrit { get; }

        public DamageRequest(CharacterStats attacker, CharacterStats defender, float skillMultiplier, ElementType element, bool canCrit = true)
        {
            Attacker = attacker;
            Defender = defender;
            SkillMultiplier = skillMultiplier;
            Element = element;
            CanCrit = canCrit;
        }
    }

    public readonly struct DamageResult
    {
        public float Amount { get; }
        public bool IsCritical { get; }
        public DamageResult(float amount, bool isCritical) { Amount = amount; IsCritical = isCritical; }
    }

    public sealed class DamageCalculator
    {
        private readonly DamageFormulaConfig _config;
        private readonly IRandomSource _random;

        public DamageCalculator(DamageFormulaConfig config, IRandomSource random)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            if (_config.DefenseConstant <= 0f || _config.MinimumDamage <= 0f) throw new ArgumentException("Damage formula constants must be positive.");
        }

        public DamageResult Calculate(DamageRequest request)
        {
            if (request.Attacker == null || request.Defender == null) throw new ArgumentException("Damage request requires attacker and defender stats.");
            var baseDamage = Math.Max(0f, request.Attacker.Attack) * Math.Max(0f, request.SkillMultiplier);
            var defenseFactor = _config.DefenseConstant / (_config.DefenseConstant + Math.Max(0f, request.Defender.Defense));
            var elementModifier = Math.Max(0f, 1f + ElementDamage(request.Attacker, request.Element) - Math.Min(_config.MaximumElementResistance, ElementResistance(request.Defender, request.Element)));
            var critical = request.CanCrit && _random.Value() < Math.Max(0f, Math.Min(1f, request.Attacker.CritRate));
            var criticalModifier = critical ? Math.Max(1f, request.Attacker.CritDamage) : 1f;
            var damageBonus = Math.Max(0f, 1f + request.Attacker.DamageBonus);
            var reduction = 1f - Math.Max(0f, Math.Min(_config.MaximumDamageReduction, request.Defender.DamageReduction));
            var amount = baseDamage * defenseFactor * elementModifier * criticalModifier * damageBonus * reduction;
            return new DamageResult(Math.Max(_config.MinimumDamage, amount), critical);
        }

        private static float ElementDamage(CharacterStats stats, ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return stats.FireDamage;
                case ElementType.Water: return stats.WaterDamage;
                case ElementType.Wood: return stats.WoodDamage;
                case ElementType.Metal: return stats.MetalDamage;
                case ElementType.Earth: return stats.EarthDamage;
                case ElementType.Lightning: return stats.LightningDamage;
                case ElementType.Wind: return stats.WindDamage;
                case ElementType.Yin: return stats.YinDamage;
                case ElementType.Yang: return stats.YangDamage;
                default: return 0f;
            }
        }

        private static float ElementResistance(CharacterStats stats, ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return stats.FireResistance;
                case ElementType.Water: return stats.WaterResistance;
                case ElementType.Wood: return stats.WoodResistance;
                case ElementType.Metal: return stats.MetalResistance;
                case ElementType.Earth: return stats.EarthResistance;
                case ElementType.Lightning: return stats.LightningResistance;
                case ElementType.Wind: return stats.WindResistance;
                case ElementType.Yin: return stats.YinResistance;
                case ElementType.Yang: return stats.YangResistance;
                default: return 0f;
            }
        }
    }
}
