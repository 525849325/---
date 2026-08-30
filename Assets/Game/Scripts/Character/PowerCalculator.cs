using System;
using ImmortalLoot.Config;
using UnityEngine;

namespace ImmortalLoot.Character
{
    [Serializable]
    public sealed class PowerFormulaConfig
    {
        public int schemaVersion;
        public float hpWeight;
        public float attackWeight;
        public float defenseWeight;
        public float critRateWeight;
        public float critDamageWeight;
        public float attackSpeedWeight;
        public float utilityWeight;
        public float elementWeight;
        public float resistanceWeight;
    }

    public sealed class PowerCalculator
    {
        private readonly PowerFormulaConfig _config;
        public PowerCalculator(PowerFormulaConfig config) => _config = config ?? throw new ArgumentNullException(nameof(config));

        public long Calculate(CharacterStats stats)
        {
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            var elements = stats.FireDamage + stats.WaterDamage + stats.WoodDamage + stats.MetalDamage + stats.EarthDamage + stats.LightningDamage + stats.WindDamage + stats.YinDamage + stats.YangDamage;
            var resistances = stats.FireResistance + stats.WaterResistance + stats.WoodResistance + stats.MetalResistance + stats.EarthResistance + stats.LightningResistance + stats.WindResistance + stats.YinResistance + stats.YangResistance;
            var utility = stats.Hit + stats.Dodge + stats.LifeSteal + stats.DamageBonus + stats.DamageReduction;
            var value = stats.HP * _config.hpWeight + stats.Attack * _config.attackWeight + stats.Defense * _config.defenseWeight +
                        stats.CritRate * _config.critRateWeight + stats.CritDamage * _config.critDamageWeight + stats.AttackSpeed * _config.attackSpeedWeight +
                        utility * _config.utilityWeight + elements * _config.elementWeight + resistances * _config.resistanceWeight;
            return Math.Max(0, (long)Math.Round(value, MidpointRounding.AwayFromZero));
        }

        public static PowerCalculator Load(IConfigSource source)
        {
            var config = JsonUtility.FromJson<PowerFormulaConfig>(source.LoadText("power_formula"));
            if (config == null || config.schemaVersion != 1 || config.hpWeight < 0 || config.attackWeight <= 0 || config.defenseWeight < 0)
                throw new ConfigException("Power formula config is invalid.");
            return new PowerCalculator(config);
        }
    }
}
