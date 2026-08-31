using System;
using UnityEngine;

namespace ImmortalLoot.Settings
{
    public enum PrivacyConsentState
    {
        Unknown = 0,
        Accepted = 1,
        Declined = 2
    }

    public interface IGameSettingsStore
    {
        int GetInt(string key, int defaultValue);
        void SetInt(string key, int value);
        void Save();
    }

    public sealed class PlayerPrefsSettingsStore : IGameSettingsStore
    {
        public int GetInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void Save() => PlayerPrefs.Save();
    }

    public sealed class GameSettingsService
    {
        private const string SoundKey = "settings.sound";
        private const string VibrationKey = "settings.vibration";
        private const string PrivacyConsentKey = "settings.privacyConsent.v1";
        private const string AutoEquipKey = "settings.autoEquip";
        private readonly IGameSettingsStore _store;
#if UNITY_INCLUDE_TESTS
        private static IGameSettingsStore _runtimeStoreOverride;
#endif

        public bool SoundEnabled { get; private set; }
        public bool VibrationEnabled { get; private set; }
        public PrivacyConsentState PrivacyConsent => ReadPrivacyConsent();
        public bool PrivacyConsentDecided => PrivacyConsent != PrivacyConsentState.Unknown;
        public bool PrivacyAccepted => PrivacyConsent == PrivacyConsentState.Accepted;
        public bool AnalyticsEnabled => PrivacyAccepted;
        public bool AutoEquipEnabled { get; private set; }

        public GameSettingsService(IGameSettingsStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            SoundEnabled = _store.GetInt(SoundKey, 1) != 0;
            VibrationEnabled = _store.GetInt(VibrationKey, 1) != 0;
            AutoEquipEnabled = _store.GetInt(AutoEquipKey, 1) != 0;
        }

        public static GameSettingsService CreateRuntime()
        {
#if UNITY_INCLUDE_TESTS
            return new GameSettingsService(_runtimeStoreOverride ?? new PlayerPrefsSettingsStore());
#else
            return new GameSettingsService(new PlayerPrefsSettingsStore());
#endif
        }

        public bool ToggleSound()
        {
            SoundEnabled = !SoundEnabled;
            Persist(SoundKey, SoundEnabled);
            return SoundEnabled;
        }

        public bool ToggleVibration()
        {
            VibrationEnabled = !VibrationEnabled;
            Persist(VibrationKey, VibrationEnabled);
            return VibrationEnabled;
        }

        public void AcceptPrivacy()
        {
            PersistConsent(PrivacyConsentState.Accepted);
        }

        public void DeclinePrivacy()
        {
            PersistConsent(PrivacyConsentState.Declined);
        }

        public void ResetPrivacyConsent()
        {
            PersistConsent(PrivacyConsentState.Unknown);
        }

        public bool ToggleAutoEquip()
        {
            AutoEquipEnabled = !AutoEquipEnabled;
            Persist(AutoEquipKey, AutoEquipEnabled);
            return AutoEquipEnabled;
        }

        public void ApplySound() => AudioListener.pause = !SoundEnabled;

        public void TryVibrate()
        {
            if (VibrationEnabled && Application.isMobilePlatform) Handheld.Vibrate();
        }

        private void Persist(string key, bool value)
        {
            _store.SetInt(key, value ? 1 : 0);
            _store.Save();
        }

        private PrivacyConsentState ReadPrivacyConsent()
        {
            var stored = _store.GetInt(PrivacyConsentKey, (int)PrivacyConsentState.Unknown);
            return stored == (int)PrivacyConsentState.Accepted
                ? PrivacyConsentState.Accepted
                : stored == (int)PrivacyConsentState.Declined
                    ? PrivacyConsentState.Declined
                    : PrivacyConsentState.Unknown;
        }

        private void PersistConsent(PrivacyConsentState value)
        {
            _store.SetInt(PrivacyConsentKey, (int)value);
            _store.Save();
        }

#if UNITY_INCLUDE_TESTS
        public static IDisposable OverrideRuntimeStoreForTests(IGameSettingsStore store)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            var previous = _runtimeStoreOverride;
            _runtimeStoreOverride = store;
            return new RuntimeStoreOverrideScope(previous);
        }

        private sealed class RuntimeStoreOverrideScope : IDisposable
        {
            private readonly IGameSettingsStore _previous;
            private bool _disposed;
            public RuntimeStoreOverrideScope(IGameSettingsStore previous) => _previous = previous;
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
