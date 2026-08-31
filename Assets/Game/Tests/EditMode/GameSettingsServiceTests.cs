using System.Collections.Generic;
using ImmortalLoot.Debugging;
using ImmortalLoot.Settings;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class GameSettingsServiceTests
    {
        [Test]
        public void Settings_DefaultSafeValuesAndPersistIndependentToggles()
        {
            var store = new MemoryStore();
            var settings = new GameSettingsService(store);
            Assert.That(settings.SoundEnabled, Is.True);
            Assert.That(settings.VibrationEnabled, Is.True);
            Assert.That(settings.PrivacyAccepted, Is.False);
            Assert.That(settings.PrivacyConsent, Is.EqualTo(PrivacyConsentState.Unknown));
            Assert.That(settings.AutoEquipEnabled, Is.True);

            settings.ToggleSound();
            settings.ToggleVibration();
            settings.AcceptPrivacy();
            settings.ToggleAutoEquip();

            var reloaded = new GameSettingsService(store);
            Assert.That(reloaded.SoundEnabled, Is.False);
            Assert.That(reloaded.VibrationEnabled, Is.False);
            Assert.That(reloaded.PrivacyAccepted, Is.True);
            Assert.That(reloaded.PrivacyConsent, Is.EqualTo(PrivacyConsentState.Accepted));
            Assert.That(reloaded.AutoEquipEnabled, Is.False);
            Assert.That(store.SaveCount, Is.EqualTo(4));
        }

        [Test]
        public void PrivacyConsent_UsesExplicitTriStateAndFailsClosedForCorruptValues()
        {
            var store = new MemoryStore();
            var settings = new GameSettingsService(store);

            Assert.That(settings.PrivacyConsent, Is.EqualTo(PrivacyConsentState.Unknown));
            Assert.That(settings.PrivacyConsentDecided, Is.False);
            settings.DeclinePrivacy();
            Assert.That(new GameSettingsService(store).PrivacyConsent, Is.EqualTo(PrivacyConsentState.Declined));
            Assert.That(new GameSettingsService(store).PrivacyAccepted, Is.False);

            store.Set("settings.privacyConsent.v1", 99);
            Assert.That(new GameSettingsService(store).PrivacyConsent, Is.EqualTo(PrivacyConsentState.Unknown));
            store.Set("settings.privacyConsent.v1", 0);
            store.Set("settings.privacyAccepted", 1);
            Assert.That(new GameSettingsService(store).PrivacyConsent, Is.EqualTo(PrivacyConsentState.Unknown),
                "The previous boolean never came from an explicit production choice and must fail closed.");

            settings.ResetPrivacyConsent();
            Assert.That(new GameSettingsService(store).PrivacyConsent, Is.EqualTo(PrivacyConsentState.Unknown));
        }

        [Test]
        public void PlaytestTelemetry_RequiresDebugBuildAndExactAcceptedConsent()
        {
            Assert.That(PlaytestTelemetryRecorder.ShouldInstall(true, false, 1f, PrivacyConsentState.Unknown), Is.False);
            Assert.That(PlaytestTelemetryRecorder.ShouldInstall(true, false, 1f, PrivacyConsentState.Declined), Is.False);
            Assert.That(PlaytestTelemetryRecorder.ShouldInstall(false, false, 1f, PrivacyConsentState.Accepted), Is.False);
            Assert.That(PlaytestTelemetryRecorder.ShouldInstall(true, false, 1f, PrivacyConsentState.Accepted), Is.True);
            Assert.That(PlaytestTelemetryRecorder.ShouldInstall(true, true, 1f, PrivacyConsentState.Accepted), Is.False);
        }

        [Test]
        public void AnonymousInstallId_IsGeneratedOnceAndNeverUsesDeviceFingerprint()
        {
            var store = new MemoryInstallIdStore();
            var generated = 0;
            var provider = new AnonymousInstallIdProvider(store, () =>
            {
                generated++;
                return "0123456789abcdef0123456789abcdef";
            });

            Assert.That(provider.GetOrCreate(), Is.EqualTo("0123456789abcdef0123456789abcdef"));
            Assert.That(provider.GetOrCreate(), Is.EqualTo("0123456789abcdef0123456789abcdef"));
            Assert.That(generated, Is.EqualTo(1));
            Assert.That(store.SaveCount, Is.EqualTo(1));

            provider.Clear();
            Assert.That(store.GetString("identity.anonymousInstallId"), Is.Empty);
            Assert.That(store.DeleteCount, Is.EqualTo(1));
            Assert.That(store.SaveCount, Is.EqualTo(2));
        }

        private sealed class MemoryStore : IGameSettingsStore
        {
            private readonly Dictionary<string, int> _values = new Dictionary<string, int>();
            public int SaveCount { get; private set; }
            public int GetInt(string key, int defaultValue) => _values.TryGetValue(key, out var value) ? value : defaultValue;
            public void SetInt(string key, int value) => _values[key] = value;
            public void Save() => SaveCount++;
            public void Set(string key, int value) => _values[key] = value;
        }

        private sealed class MemoryInstallIdStore : IInstallIdStore
        {
            private string _value = string.Empty;
            public int SaveCount { get; private set; }
            public int DeleteCount { get; private set; }
            public string GetString(string key) => _value;
            public void SetString(string key, string value) => _value = value;
            public void DeleteKey(string key) { _value = string.Empty; DeleteCount++; }
            public void Save() => SaveCount++;
        }
    }
}
