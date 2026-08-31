using System.Collections.Generic;
using ImmortalLoot.Settings;

namespace ImmortalLoot.Tests.PlayMode
{
    internal sealed class TestGameSettingsStore : IGameSettingsStore
    {
        private readonly Dictionary<string, int> _values = new Dictionary<string, int>();
        public int GetInt(string key, int defaultValue) => _values.TryGetValue(key, out var value) ? value : defaultValue;
        public void SetInt(string key, int value) => _values[key] = value;
        public void Save() { }
    }

    internal sealed class TestInstallIdStore : IInstallIdStore
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
        public string GetString(string key) => _values.TryGetValue(key, out var value) ? value : string.Empty;
        public void SetString(string key, string value) => _values[key] = value;
        public void DeleteKey(string key) => _values.Remove(key);
        public void Save() { }
    }
}
