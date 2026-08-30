using System;
using UnityEngine;

namespace ImmortalLoot.Settings
{
    public interface IInstallIdStore
    {
        string GetString(string key);
        void SetString(string key, string value);
        void Save();
    }

    public sealed class PlayerPrefsInstallIdStore : IInstallIdStore
    {
        public string GetString(string key) => PlayerPrefs.GetString(key, string.Empty);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void Save() => PlayerPrefs.Save();
    }

    public sealed class AnonymousInstallIdProvider
    {
        private const string InstallIdKey = "identity.anonymousInstallId";
        private readonly IInstallIdStore _store;
        private readonly Func<string> _generate;

        public AnonymousInstallIdProvider(IInstallIdStore store, Func<string> generate = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _generate = generate ?? (() => Guid.NewGuid().ToString("N"));
        }

        public string GetOrCreate()
        {
            var existing = _store.GetString(InstallIdKey)?.Trim();
            if (IsValid(existing)) return existing;
            var generated = _generate()?.Trim();
            if (!IsValid(generated)) throw new InvalidOperationException("Anonymous install ID generator returned an invalid value.");
            _store.SetString(InstallIdKey, generated);
            _store.Save();
            return generated;
        }

        private static bool IsValid(string value)
        {
            if (value == null || value.Length != 32) return false;
            foreach (var character in value)
                if (!Uri.IsHexDigit(character)) return false;
            return true;
        }
    }
}
