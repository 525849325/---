using ImmortalLoot.Config;
using UnityEngine;

namespace ImmortalLoot.Battle
{
    public static class DamageFormulaConfigLoader
    {
        public static DamageFormulaConfig Load(IConfigSource source)
        {
            var row = JsonUtility.FromJson<DamageFormulaRow>(source.LoadText("battle_formula"));
            if (row == null || row.schemaVersion != 1) throw new ConfigException("Battle formula config has an unsupported schema version.");
            if (row.defenseConstant <= 0f || row.minimumDamage <= 0f || row.maximumDamageReduction < 0f || row.maximumDamageReduction >= 1f || row.maximumElementResistance < 0f || row.maximumElementResistance >= 1f)
                throw new ConfigException("Battle formula config contains invalid values.");
            return new DamageFormulaConfig
            {
                DefenseConstant = row.defenseConstant,
                MinimumDamage = row.minimumDamage,
                MaximumDamageReduction = row.maximumDamageReduction,
                MaximumElementResistance = row.maximumElementResistance
            };
        }

        [System.Serializable]
        private sealed class DamageFormulaRow
        {
            public int schemaVersion;
            public float defenseConstant;
            public float minimumDamage;
            public float maximumDamageReduction;
            public float maximumElementResistance;
        }
    }
}
