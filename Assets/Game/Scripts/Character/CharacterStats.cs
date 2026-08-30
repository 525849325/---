using System;

namespace ImmortalLoot.Character
{
    [Serializable]
    public sealed class CharacterStats
    {
        public float HP;
        public float Attack;
        public float Defense;
        public float CritRate;
        public float CritDamage;
        public float AttackSpeed;
        public float Hit;
        public float Dodge;
        public float LifeSteal;
        public float DamageBonus;
        public float DamageReduction;
        public float FireDamage;
        public float WaterDamage;
        public float WoodDamage;
        public float MetalDamage;
        public float EarthDamage;
        public float LightningDamage;
        public float WindDamage;
        public float YinDamage;
        public float YangDamage;
        public float FireResistance;
        public float WaterResistance;
        public float WoodResistance;
        public float MetalResistance;
        public float EarthResistance;
        public float LightningResistance;
        public float WindResistance;
        public float YinResistance;
        public float YangResistance;

        public CharacterStats Clone() => (CharacterStats)MemberwiseClone();

        public float Get(StatId id)
        {
            switch (id)
            {
                case StatId.HP: return HP;
                case StatId.Attack: return Attack;
                case StatId.Defense: return Defense;
                case StatId.CritRate: return CritRate;
                case StatId.CritDamage: return CritDamage;
                case StatId.AttackSpeed: return AttackSpeed;
                case StatId.Hit: return Hit;
                case StatId.Dodge: return Dodge;
                case StatId.LifeSteal: return LifeSteal;
                case StatId.DamageBonus: return DamageBonus;
                case StatId.DamageReduction: return DamageReduction;
                case StatId.FireDamage: return FireDamage;
                case StatId.WaterDamage: return WaterDamage;
                case StatId.WoodDamage: return WoodDamage;
                case StatId.MetalDamage: return MetalDamage;
                case StatId.EarthDamage: return EarthDamage;
                case StatId.LightningDamage: return LightningDamage;
                case StatId.WindDamage: return WindDamage;
                case StatId.YinDamage: return YinDamage;
                case StatId.YangDamage: return YangDamage;
                case StatId.FireResistance: return FireResistance;
                case StatId.WaterResistance: return WaterResistance;
                case StatId.WoodResistance: return WoodResistance;
                case StatId.MetalResistance: return MetalResistance;
                case StatId.EarthResistance: return EarthResistance;
                case StatId.LightningResistance: return LightningResistance;
                case StatId.WindResistance: return WindResistance;
                case StatId.YinResistance: return YinResistance;
                case StatId.YangResistance: return YangResistance;
                default: throw new ArgumentOutOfRangeException(nameof(id), id, null);
            }
        }

        public void Set(StatId id, float value)
        {
            switch (id)
            {
                case StatId.HP: HP = value; break;
                case StatId.Attack: Attack = value; break;
                case StatId.Defense: Defense = value; break;
                case StatId.CritRate: CritRate = value; break;
                case StatId.CritDamage: CritDamage = value; break;
                case StatId.AttackSpeed: AttackSpeed = value; break;
                case StatId.Hit: Hit = value; break;
                case StatId.Dodge: Dodge = value; break;
                case StatId.LifeSteal: LifeSteal = value; break;
                case StatId.DamageBonus: DamageBonus = value; break;
                case StatId.DamageReduction: DamageReduction = value; break;
                case StatId.FireDamage: FireDamage = value; break;
                case StatId.WaterDamage: WaterDamage = value; break;
                case StatId.WoodDamage: WoodDamage = value; break;
                case StatId.MetalDamage: MetalDamage = value; break;
                case StatId.EarthDamage: EarthDamage = value; break;
                case StatId.LightningDamage: LightningDamage = value; break;
                case StatId.WindDamage: WindDamage = value; break;
                case StatId.YinDamage: YinDamage = value; break;
                case StatId.YangDamage: YangDamage = value; break;
                case StatId.FireResistance: FireResistance = value; break;
                case StatId.WaterResistance: WaterResistance = value; break;
                case StatId.WoodResistance: WoodResistance = value; break;
                case StatId.MetalResistance: MetalResistance = value; break;
                case StatId.EarthResistance: EarthResistance = value; break;
                case StatId.LightningResistance: LightningResistance = value; break;
                case StatId.WindResistance: WindResistance = value; break;
                case StatId.YinResistance: YinResistance = value; break;
                case StatId.YangResistance: YangResistance = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(id), id, null);
            }
        }
    }

    public enum StatId
    {
        HP, Attack, Defense, CritRate, CritDamage, AttackSpeed, Hit, Dodge, LifeSteal,
        DamageBonus, DamageReduction, FireDamage, WaterDamage, WoodDamage, MetalDamage,
        EarthDamage, LightningDamage, WindDamage, YinDamage, YangDamage, FireResistance,
        WaterResistance, WoodResistance, MetalResistance, EarthResistance,
        LightningResistance, WindResistance, YinResistance, YangResistance
    }
}
