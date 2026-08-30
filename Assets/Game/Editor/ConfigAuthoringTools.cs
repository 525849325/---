using System;
using System.Collections.Generic;
using System.IO;
using ImmortalLoot.Config;
using UnityEditor;
using UnityEngine;

namespace ImmortalLoot.Editor
{
    public static class ConfigAuthoringTools
    {
        private static readonly string[] ConfigNames =
        {
            "affixes", "equipment", "quality_rules", "realms", "spiritual_roots", "skills",
            "cultivation_methods", "drop_tables", "monsters", "stages", "shop", "activities", "battle_formula", "inventory_formula", "afk", "realm_formula"
        };

        [MenuItem("ImmortalLoot/Config/Create Authoring Asset")]
        public static void CreateAuthoringAsset()
        {
            const string directory = "Assets/Game/Data";
            const string path = directory + "/GameConfigAuthoring.asset";
            Directory.CreateDirectory(directory);
            var asset = AssetDatabase.LoadAssetAtPath<JsonConfigAuthoringAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<JsonConfigAuthoringAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.Entries = new JsonConfigAuthoringAsset.Entry[ConfigNames.Length];
            for (var i = 0; i < ConfigNames.Length; i++)
            {
                asset.Entries[i] = new JsonConfigAuthoringAsset.Entry
                {
                    ConfigName = ConfigNames[i],
                    RuntimeJson = Resources.Load<TextAsset>($"Config/{ConfigNames[i]}")
                };
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            Debug.Log($"Created config authoring asset at {path}.");
        }

        public static void CreateAndValidateForAutomation()
        {
            CreateAuthoringAsset();
            ValidateRuntimeJson();
        }

        [MenuItem("ImmortalLoot/Config/Validate Runtime JSON")]
        public static void ValidateRuntimeJson()
        {
            var catalog = new JsonConfigRepository(new ResourcesConfigSource()).LoadAll();
            Debug.Log(Describe(catalog, "Runtime JSON validation passed"));
        }

        [MenuItem("ImmortalLoot/Config/Validate and Export Selected Authoring Asset")]
        public static void ValidateAndExportSelected()
        {
            var asset = Selection.activeObject as JsonConfigAuthoringAsset;
            if (asset == null) throw new InvalidOperationException("Select a JsonConfigAuthoringAsset first.");
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in asset.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.ConfigName)) continue;
                values[entry.ConfigName] = entry.EffectiveJson;
            }
            var catalog = new JsonConfigRepository(new DictionaryConfigSource(values)).LoadAll();
            foreach (var pair in values)
            {
                var path = $"Assets/Game/Resources/Config/{pair.Key}.json";
                File.WriteAllText(path, pair.Value);
            }
            AssetDatabase.Refresh();
            Debug.Log(Describe(catalog, "Authoring JSON validation and export passed"));
        }

        private static string Describe(GameConfigCatalog catalog, string prefix) =>
            $"{prefix}: {catalog.Equipment.Count} equipment, {catalog.Realms.Count} realms, " +
            $"{catalog.SpiritualRoots.Count} roots, {catalog.Skills.Count} skills, " +
            $"{catalog.CultivationMethods.Count} methods, {catalog.Monsters.Count} monsters, " +
            $"{catalog.Stages.Count} stages, {catalog.DropTables.Count} drop tables, " +
            $"{catalog.ShopItems.Count} shop items, {catalog.Activities.Count} activities.";

        private sealed class DictionaryConfigSource : IConfigSource
        {
            private readonly IReadOnlyDictionary<string, string> _values;
            public DictionaryConfigSource(IReadOnlyDictionary<string, string> values) => _values = values;
            public string LoadText(string configName)
            {
                if (!_values.TryGetValue(configName, out var value)) throw new ConfigException($"Authoring asset is missing '{configName}'.");
                return value;
            }
        }
    }
}
