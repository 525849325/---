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
                var repository = new JsonPlayerSaveRepository(path);
                repository.Save(new PlayerSaveSnapshot { PlayerId = "player-1", Nickname = "云游客", Level = 8, InventoryJson = "{\"count\":3}" });
                var loaded = repository.Load();
                Assert.That(loaded.PlayerId, Is.EqualTo("player-1"));
                Assert.That(loaded.Level, Is.EqualTo(8));
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
    }
}
