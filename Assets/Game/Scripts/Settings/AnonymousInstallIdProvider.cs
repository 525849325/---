using System;
using UnityEngine;

namespace ImmortalLoot.Settings
{
    public interface IInstallIdStore
    {
        string GetString(string key);
        void SetString(string key, string value);
        void DeleteKey(string key);
        void Save();
    }

    public sealed class PlayerPrefsInstallIdStore : IInstallIdStore
    {
        public string GetString(string key) => PlayerPrefs.GetString(key, string.Empty);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public void Save() => PlayerPrefs.Save();
    }

    public sealed class AnonymousInstallIdProvider
    {
        private const string InstallIdKey = "identity.anonymousInstallId";
        private readonly IInstallIdStore _store;
        private readonly Func<string> _generate;
#if UNITY_INCLUDE_TESTS
        private static IInstallIdStore _runtimeStoreOverride;
#endif

        public AnonymousInstallIdProvider(IInstallIdStore store, Func<string> generate = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _generate = generate ?? (() => Guid.NewGuid().ToString("N"));
        }

        public static AnonymousInstallIdProvider CreateRuntime()
        {
#if UNITY_INCLUDE_TESTS
            return new AnonymousInstallIdProvider(_runtimeStoreOverride ?? new PlayerPrefsInstallIdStore());
#else
            return new AnonymousInstallIdProvider(new PlayerPrefsInstallIdStore());
#endif
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

        public void Clear()
        {
            _store.DeleteKey(InstallIdKey);
            _store.Save();
        }

        private static bool IsValid(string value)
        {
            if (value == null || value.Length != 32) return false;
            foreach (var character in value)
                if (!Uri.IsHexDigit(character)) return false;
            return true;
        }

#if UNITY_INCLUDE_TESTS
        public static IDisposable OverrideRuntimeStoreForTests(IInstallIdStore store)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            var previous = _runtimeStoreOverride;
            _runtimeStoreOverride = store;
            return new RuntimeStoreOverrideScope(previous);
        }

        private sealed class RuntimeStoreOverrideScope : IDisposable
        {
            private readonly IInstallIdStore _previous;
            private bool _disposed;
            public RuntimeStoreOverrideScope(IInstallIdStore previous) => _previous = previous;
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _runtimeStoreOverride = _previous;
            }
        }
#endif
    }
}
