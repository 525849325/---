using System;
using UnityEngine;

namespace ImmortalLoot.Settings
{
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
        private const string PrivacyKey = "settings.privacyAccepted";
        private const string AutoEquipKey = "settings.autoEquip";
        private readonly IGameSettingsStore _store;

        public bool SoundEnabled { get; private set; }
        public bool VibrationEnabled { get; private set; }
        public bool PrivacyAccepted { get; private set; }
        public bool AutoEquipEnabled { get; private set; }

        public GameSettingsService(IGameSettingsStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            SoundEnabled = _store.GetInt(SoundKey, 1) != 0;
            VibrationEnabled = _store.GetInt(VibrationKey, 1) != 0;
            PrivacyAccepted = _store.GetInt(PrivacyKey, 0) != 0;
            AutoEquipEnabled = _store.GetInt(AutoEquipKey, 1) != 0;
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
            PrivacyAccepted = true;
            Persist(PrivacyKey, true);
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
    }
}
