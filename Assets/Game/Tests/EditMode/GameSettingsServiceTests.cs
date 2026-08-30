using System.Collections.Generic;
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
            Assert.That(settings.AutoEquipEnabled, Is.True);

            settings.ToggleSound();
            settings.ToggleVibration();
            settings.AcceptPrivacy();
            settings.ToggleAutoEquip();

            var reloaded = new GameSettingsService(store);
            Assert.That(reloaded.SoundEnabled, Is.False);
            Assert.That(reloaded.VibrationEnabled, Is.False);
            Assert.That(reloaded.PrivacyAccepted, Is.True);
            Assert.That(reloaded.AutoEquipEnabled, Is.False);
            Assert.That(store.SaveCount, Is.EqualTo(4));
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
        }

        private sealed class MemoryStore : IGameSettingsStore
        {
            private readonly Dictionary<string, int> _values = new Dictionary<string, int>();
            public int SaveCount { get; private set; }
            public int GetInt(string key, int defaultValue) => _values.TryGetValue(key, out var value) ? value : defaultValue;
            public void SetInt(string key, int value) => _values[key] = value;
            public void Save() => SaveCount++;
        }

        private sealed class MemoryInstallIdStore : IInstallIdStore
        {
            private string _value = string.Empty;
            public int SaveCount { get; private set; }
            public string GetString(string key) => _value;
            public void SetString(string key, string value) => _value = value;
            public void Save() => SaveCount++;
        }
    }
}
