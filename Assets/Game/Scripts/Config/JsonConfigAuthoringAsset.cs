using System;
using UnityEngine;

namespace ImmortalLoot.Config
{
    [CreateAssetMenu(fileName = "GameConfigAuthoring", menuName = "ImmortalLoot/JSON Config Authoring")]
    public sealed class JsonConfigAuthoringAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string ConfigName;
            public TextAsset RuntimeJson;
            [TextArea(8, 30)] public string JsonOverride;

            public string EffectiveJson => string.IsNullOrWhiteSpace(JsonOverride)
                ? RuntimeJson == null ? string.Empty : RuntimeJson.text
                : JsonOverride;
        }

        public Entry[] Entries = Array.Empty<Entry>();
    }
}
