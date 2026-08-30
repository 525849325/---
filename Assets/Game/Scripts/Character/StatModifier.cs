using System;

namespace ImmortalLoot.Character
{
    public enum StatModifierType { Flat, AdditivePercent }

    [Serializable]
    public readonly struct StatModifier
    {
        public StatId Stat { get; }
        public StatModifierType Type { get; }
        public float Value { get; }
        public string SourceId { get; }

        public StatModifier(StatId stat, StatModifierType type, float value, string sourceId)
        {
            Stat = stat;
            Type = type;
            Value = value;
            SourceId = sourceId ?? string.Empty;
        }
    }
}
