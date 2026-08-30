using System;
using System.IO;
using ImmortalLoot.Player;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class PlayerSaveRepositoryTests
    {
        [Test]
        public void SaveLoad_IsVersionedAndRoundTripsWithoutAccessToken()
        {
            var directory = Path.Combine(Path.GetTempPath(), "immortal-loot-test-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "save.json");
            try
            {
                var savedAt = new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc);
                var repository = new JsonPlayerSaveRepository(path, () => savedAt);
                repository.Save(new PlayerSaveSnapshot
                {
                    PlayerId = "player-1", Nickname = "云游客", Level = 8, SoftCurrency = 120,
                    StageElapsedSeconds = 181, LastActiveUnixSeconds = 1234,
                    InventoryJson = "{\"count\":3}", EquippedInstanceIdsJson = "{\"ids\":[\"equip-1\"]}"
                });
                var loaded = repository.Load();
                Assert.That(loaded.PlayerId, Is.EqualTo("player-1"));
                Assert.That(loaded.Level, Is.EqualTo(8));
                Assert.That(loaded.SoftCurrency, Is.EqualTo(120));
                Assert.That(loaded.StageElapsedSeconds, Is.EqualTo(181));
                Assert.That(loaded.LastActiveUnixSeconds, Is.EqualTo(1234));
                Assert.That(loaded.SavedAtUnixSeconds, Is.EqualTo(new DateTimeOffset(savedAt).ToUnixTimeSeconds()));
                Assert.That(loaded.EquippedInstanceIdsJson, Does.Contain("equip-1"));
                Assert.That(File.ReadAllText(path), Does.Not.Contain("AccessToken"));
                Assert.That(File.Exists(path + ".tmp"), Is.False);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Test]
        public void Load_RejectsTamperedPayload()
        {
            var directory = Path.Combine(Path.GetTempPath(), "immortal-loot-test-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "save.json");
            try
            {
                var repository = new JsonPlayerSaveRepository(path);
                repository.Save(new PlayerSaveSnapshot { PlayerId = "player-1", Nickname = "original" });
                File.WriteAllText(path, File.ReadAllText(path).Replace("original", "tampered"));
                Assert.That(() => repository.Load(), Throws.TypeOf<InvalidDataException>());
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Test]
        public void Quarantine_NeverOverwritesAndNeverThrowsWhenDestinationConflicts()
        {
            var directory = Path.Combine(Path.GetTempPath(), "immortal-loot-test-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "save.json");
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(path, "corrupt");
                var now = new DateTime(2026, 8, 30, 1, 2, 3, DateTimeKind.Utc);
                var destination = path + ".corrupt-20260830T010203-fixed";
                File.WriteAllText(destination, "existing");

                Assert.That(JsonPlayerSaveRepository.TryQuarantine(path, now, () => "fixed"), Is.Null);
                Assert.That(File.ReadAllText(destination), Is.EqualTo("existing"));
                Assert.That(File.Exists(path), Is.True);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
