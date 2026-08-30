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

            settings.ToggleSound();
            settings.ToggleVibration();
            settings.AcceptPrivacy();

            var reloaded = new GameSettingsService(store);
            Assert.That(reloaded.SoundEnabled, Is.False);
            Assert.That(reloaded.VibrationEnabled, Is.False);
            Assert.That(reloaded.PrivacyAccepted, Is.True);
            Assert.That(store.SaveCount, Is.EqualTo(3));
        }

        private sealed class MemoryStore : IGameSettingsStore
        {
            private readonly Dictionary<string, int> _values = new Dictionary<string, int>();
            public int SaveCount { get; private set; }
            public int GetInt(string key, int defaultValue) => _values.TryGetValue(key, out var value) ? value : defaultValue;
            public void SetInt(string key, int value) => _values[key] = value;
            public void Save() => SaveCount++;
        }
    }
}
