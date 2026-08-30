using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ImmortalLoot.Cultivation;
using ImmortalLoot.Player;
using ImmortalLoot.Realm;
using ImmortalLoot.SpiritualRoot;
using ImmortalLoot.Stage;
using NUnit.Framework;
using UnityEngine;

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
        public void ProgressState_RoundTripsEveryCoreGrowthDimension()
        {
            var state = new PlayerProgressState
            {
                CurrentStageId = "stage_1_7",
                GuideStep = 3,
                TaskClaimed = true,
                Realm = new RealmProgressState
                {
                    RealmId = "realm_qi_coalescence", RealmStage = 4, PlayerLevel = 12,
                    Experience = 345, BreakthroughMaterial = 678, CooldownUntilUnixSeconds = 999,
                    PendingTribulation = new PendingTribulation
                    {
                        Token = "trial-token", TargetRealmId = "realm_spirit_foundation",
                        ReservedMaterial = 77, RequiredExp = 88
                    }
                },
                Stage = new StageProgressState { ClearedStageIds = new System.Collections.Generic.List<string> { "stage_1_1", "stage_1_2" } },
                Cultivation = new CultivationMethodState
                {
                    LearnedMethodIds = new System.Collections.Generic.List<string> { "method_cinder_scripture", "method_ember_breath" },
                    PrimaryMethodId = "method_cinder_scripture",
                    AuxiliaryMethodIds = new[] { "method_ember_breath", "method_quick_spark" }
                },
                SpiritualRoots = new SpiritualRootState
                {
                    Roots = new System.Collections.Generic.List<SpiritualRootProgress>
                    {
                        new SpiritualRootProgress { RootId = "root_fire", Level = 2 }
                    },
                    GrantRecords = new System.Collections.Generic.List<SpiritualRootGrantRecord>
                    {
                        new SpiritualRootGrantRecord { TribulationToken = "trial-token", RootId = "root_fire", NewLevel = 2 }
                    }
                }
            };

            var restored = PlayerProgressStateCodec.Deserialize(PlayerProgressStateCodec.Serialize(state));

            Assert.That(restored.CurrentStageId, Is.EqualTo("stage_1_7"));
            Assert.That(restored.GuideStep, Is.EqualTo(3));
            Assert.That(restored.TaskClaimed, Is.True);
            Assert.That(restored.Realm.RealmId, Is.EqualTo("realm_qi_coalescence"));
            Assert.That(restored.Realm.RealmStage, Is.EqualTo(4));
            Assert.That(restored.Realm.PlayerLevel, Is.EqualTo(12));
            Assert.That(restored.Realm.Experience, Is.EqualTo(345));
            Assert.That(restored.Realm.BreakthroughMaterial, Is.EqualTo(678));
            Assert.That(restored.Realm.CooldownUntilUnixSeconds, Is.EqualTo(999));
            Assert.That(restored.Realm.PendingTribulation.Token, Is.EqualTo("trial-token"));
            Assert.That(restored.Realm.PendingTribulation.TargetRealmId, Is.EqualTo("realm_spirit_foundation"));
            Assert.That(restored.Realm.PendingTribulation.ReservedMaterial, Is.EqualTo(77));
            Assert.That(restored.Realm.PendingTribulation.RequiredExp, Is.EqualTo(88));
            Assert.That(restored.Stage.ClearedStageIds, Is.EquivalentTo(new[] { "stage_1_1", "stage_1_2" }));
            Assert.That(restored.Cultivation.LearnedMethodIds, Is.EquivalentTo(new[] { "method_cinder_scripture", "method_ember_breath" }));
            Assert.That(restored.Cultivation.PrimaryMethodId, Is.EqualTo("method_cinder_scripture"));
            Assert.That(restored.Cultivation.AuxiliaryMethodIds[0], Is.EqualTo("method_ember_breath"));
            Assert.That(restored.Cultivation.AuxiliaryMethodIds[1], Is.EqualTo("method_quick_spark"));
            Assert.That(restored.SpiritualRoots.Roots[0].RootId, Is.EqualTo("root_fire"));
            Assert.That(restored.SpiritualRoots.Roots[0].Level, Is.EqualTo(2));
            Assert.That(restored.SpiritualRoots.GrantRecords[0].TribulationToken, Is.EqualTo("trial-token"));
            Assert.That(restored.SpiritualRoots.GrantRecords[0].RootId, Is.EqualTo("root_fire"));
            Assert.That(restored.SpiritualRoots.GrantRecords[0].NewLevel, Is.EqualTo(2));
        }

        [Test]
        public void ProgressState_EmptyOrPartialJsonReturnsUsableDefaults()
        {
            var empty = PlayerProgressStateCodec.Deserialize(string.Empty);
            var partial = PlayerProgressStateCodec.Deserialize(
                "{\"GuideStep\":2," +
                "\"Stage\":{\"ClearedStageIds\":[\"stage_1_1\",\" \",null,\"stage_1_1\",\" stage_1_2 \"]}," +
                "\"Cultivation\":{\"LearnedMethodIds\":[\"method_cinder_scripture\",\" \",null,\"method_cinder_scripture\",\" method_ember_breath \"],\"AuxiliaryMethodIds\":[\"method_ember_breath\"]}," +
                "\"SpiritualRoots\":{\"Roots\":[null,{\"RootId\":\"root_fire\",\"Level\":2}],\"GrantRecords\":[null,{\"TribulationToken\":\"trial-token\",\"RootId\":\"root_fire\",\"NewLevel\":2}]}}");

            foreach (var state in new[] { empty, partial })
            {
                Assert.That(state.CurrentStageId, Is.EqualTo("stage_1_1"));
                Assert.That(state.Realm, Is.Not.Null);
                Assert.That(state.Realm.PendingTribulation, Is.Null);
                Assert.That(state.Stage?.ClearedStageIds, Is.Not.Null);
                Assert.That(state.Cultivation?.LearnedMethodIds, Is.Not.Null);
                Assert.That(state.Cultivation?.AuxiliaryMethodIds, Has.Length.EqualTo(2));
                Assert.That(state.SpiritualRoots?.Roots, Is.Not.Null);
                Assert.That(state.SpiritualRoots?.GrantRecords, Is.Not.Null);
            }
            Assert.That(partial.GuideStep, Is.EqualTo(2));
            Assert.That(partial.Stage.ClearedStageIds, Is.EqualTo(new[] { "stage_1_1", "stage_1_2" }));
            Assert.That(partial.Cultivation.LearnedMethodIds,
                Is.EqualTo(new[] { "method_cinder_scripture", "method_ember_breath" }));
            Assert.That(partial.Cultivation.AuxiliaryMethodIds[0], Is.EqualTo("method_ember_breath"));
            Assert.That(partial.Cultivation.AuxiliaryMethodIds[1], Is.Empty);
            Assert.That(partial.SpiritualRoots.Roots, Has.Count.EqualTo(1));
            Assert.That(partial.SpiritualRoots.Roots[0].RootId, Is.EqualTo("root_fire"));
            Assert.That(partial.SpiritualRoots.GrantRecords, Has.Count.EqualTo(1));
            Assert.That(partial.SpiritualRoots.GrantRecords[0].TribulationToken, Is.EqualTo("trial-token"));
        }

        [Test]
        public void ProgressState_NormalizesDuplicateRootsWithoutLosingGrantIdempotency()
        {
            var state = PlayerProgressStateCodec.Deserialize(
                "{\"SpiritualRoots\":{" +
                "\"Roots\":[{\"RootId\":\" root_fire \",\"Level\":1},{\"RootId\":\"root_fire\",\"Level\":3}]," +
                "\"GrantRecords\":[{\"TribulationToken\":\" trial-token \",\"RootId\":\"\",\"NewLevel\":1}," +
                "{\"TribulationToken\":\"trial-token\",\"RootId\":\"root_fire\",\"NewLevel\":3}," +
                "{\"TribulationToken\":\" orphan-token \",\"RootId\":null,\"NewLevel\":-2}]}}");

            Assert.That(state.SpiritualRoots.Roots, Has.Count.EqualTo(1));
            Assert.That(state.SpiritualRoots.Roots[0].RootId, Is.EqualTo("root_fire"));
            Assert.That(state.SpiritualRoots.Roots[0].Level, Is.EqualTo(3));
            Assert.That(state.SpiritualRoots.GrantRecords, Has.Count.EqualTo(2));
            Assert.That(state.SpiritualRoots.GrantRecords[0].TribulationToken, Is.EqualTo("trial-token"));
            Assert.That(state.SpiritualRoots.GrantRecords[0].RootId, Is.EqualTo("root_fire"));
            Assert.That(state.SpiritualRoots.GrantRecords[0].NewLevel, Is.EqualTo(3));
            Assert.That(state.SpiritualRoots.GrantRecords[1].TribulationToken, Is.EqualTo("orphan-token"));
            Assert.That(state.SpiritualRoots.GrantRecords[1].RootId, Is.Empty);
            Assert.That(state.SpiritualRoots.GrantRecords[1].NewLevel, Is.Zero);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("{}")]
        public void Save_MissingAggregateBootstrapsFromLegacyMirror(string progressJson)
        {
            var directory = Path.Combine(Path.GetTempPath(), "immortal-loot-test-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "save.json");
            try
            {
                var repository = new JsonPlayerSaveRepository(path);
                repository.Save(new PlayerSaveSnapshot
                {
                    Level = 8,
                    Exp = 234,
                    RealmId = "realm_qi_coalescence",
                    RealmStage = 3,
                    ProgressJson = progressJson
                });

                var loaded = repository.Load();
                var progress = PlayerProgressStateCodec.Deserialize(loaded.ProgressJson);

                Assert.That(progress.Realm.PlayerLevel, Is.EqualTo(8));
                Assert.That(progress.Realm.Experience, Is.EqualTo(234));
                Assert.That(progress.Realm.RealmId, Is.EqualTo("realm_qi_coalescence"));
                Assert.That(progress.Realm.RealmStage, Is.EqualTo(3));
                Assert.That(loaded.Level, Is.EqualTo(progress.Realm.PlayerLevel));
                Assert.That(loaded.Exp, Is.EqualTo(progress.Realm.Experience));
                Assert.That(loaded.RealmId, Is.EqualTo(progress.Realm.RealmId));
                Assert.That(loaded.RealmStage, Is.EqualTo(progress.Realm.RealmStage));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Test]
        public void Save_ConflictingV3AggregateOverridesLegacyMirror()
        {
            var directory = Path.Combine(Path.GetTempPath(), "immortal-loot-test-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "save.json");
            try
            {
                var snapshot = new PlayerSaveSnapshot
                {
                    Level = 99,
                    Exp = 9999,
                    RealmId = "legacy-conflict",
                    RealmStage = 9,
                    ProgressJson = PlayerProgressStateCodec.Serialize(new PlayerProgressState
                    {
                        Realm = new RealmProgressState
                        {
                            PlayerLevel = 12,
                            Experience = 345,
                            RealmId = "realm_qi_coalescence",
                            RealmStage = 4
                        }
                    })
                };
                var repository = new JsonPlayerSaveRepository(path);

                repository.Save(snapshot);
                var loaded = repository.Load();
                var progress = PlayerProgressStateCodec.Deserialize(loaded.ProgressJson);

                Assert.That(snapshot.Level, Is.EqualTo(12));
                Assert.That(snapshot.Exp, Is.EqualTo(345));
                Assert.That(snapshot.RealmId, Is.EqualTo("realm_qi_coalescence"));
                Assert.That(snapshot.RealmStage, Is.EqualTo(4));
                Assert.That(loaded.Level, Is.EqualTo(progress.Realm.PlayerLevel));
                Assert.That(loaded.Exp, Is.EqualTo(progress.Realm.Experience));
                Assert.That(loaded.RealmId, Is.EqualTo(progress.Realm.RealmId));
                Assert.That(loaded.RealmStage, Is.EqualTo(progress.Realm.RealmStage));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Test]
        public void Load_V3MissingAggregateBootstrapsFromLegacyMirror()
        {
            var directory = Path.Combine(Path.GetTempPath(), "immortal-loot-test-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "save.json");
            Directory.CreateDirectory(directory);
            try
            {
                var fixture = new PlayerSaveSnapshot
                {
                    SchemaVersion = 3,
                    Level = 6,
                    Exp = 456,
                    RealmId = "realm_qi_coalescence",
                    RealmStage = 2,
                    ProgressJson = "{}"
                };
                var payload = JsonUtility.ToJson(fixture);
                File.WriteAllText(path, JsonUtility.ToJson(new LegacyEnvelope
                {
                    schemaVersion = 3,
                    payload = payload,
                    checksum = Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(payload)))
                }));

                var loaded = new JsonPlayerSaveRepository(path).Load();
                var progress = PlayerProgressStateCodec.Deserialize(loaded.ProgressJson);

                Assert.That(progress.Realm.PlayerLevel, Is.EqualTo(6));
                Assert.That(progress.Realm.Experience, Is.EqualTo(456));
                Assert.That(progress.Realm.RealmId, Is.EqualTo("realm_qi_coalescence"));
                Assert.That(progress.Realm.RealmStage, Is.EqualTo(2));
                Assert.That(loaded.Level, Is.EqualTo(progress.Realm.PlayerLevel));
                Assert.That(loaded.Exp, Is.EqualTo(progress.Realm.Experience));
                Assert.That(loaded.RealmId, Is.EqualTo(progress.Realm.RealmId));
                Assert.That(loaded.RealmStage, Is.EqualTo(progress.Realm.RealmStage));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Test]
        public void Load_MigratesV2SnapshotIntoV3AggregateProgress()
        {
            var directory = Path.Combine(Path.GetTempPath(), "immortal-loot-test-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "save.json");
            Directory.CreateDirectory(directory);
            try
            {
                var legacy = new LegacyV2Snapshot
                {
                    SchemaVersion = 2, Level = 9, Exp = 321,
                    RealmId = "realm_qi_coalescence", RealmStage = 2,
                    PlayerId = "legacy-player", Nickname = "旧档修士", Kills = 17,
                    SoftCurrency = 1234, PremiumCurrency = 56, StageElapsedSeconds = 87,
                    InventoryJson = "{\"legacyInventory\":true}",
                    EquippedInstanceIdsJson = "{\"ids\":[\"legacy-equipped\"]}",
                    ProgressJson = "{}", LastActiveUnixSeconds = 111, SavedAtUnixSeconds = 99
                };
                var payload = JsonUtility.ToJson(legacy);
                File.WriteAllText(path, JsonUtility.ToJson(new LegacyEnvelope
                {
                    schemaVersion = 2,
                    payload = payload,
                    checksum = Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(payload)))
                }));

                var repository = new JsonPlayerSaveRepository(path);
                var loaded = repository.Load();
                var progress = PlayerProgressStateCodec.Deserialize(loaded.ProgressJson);

                Assert.That(loaded.SchemaVersion, Is.EqualTo(PlayerSaveSnapshot.CurrentSchemaVersion));
                Assert.That(progress.Realm.RealmId, Is.EqualTo("realm_qi_coalescence"));
                Assert.That(progress.Realm.RealmStage, Is.EqualTo(2));
                Assert.That(progress.Realm.PlayerLevel, Is.EqualTo(9));
                Assert.That(progress.Realm.Experience, Is.EqualTo(321));
                Assert.That(progress.CurrentStageId, Is.EqualTo("stage_1_5"));
                Assert.That(progress.Stage, Is.Not.Null);
                Assert.That(progress.Cultivation, Is.Not.Null);
                Assert.That(progress.SpiritualRoots, Is.Not.Null);
                Assert.That(loaded.ProgressJson, Is.Not.EqualTo("{}"));
                Assert.That(loaded.Level, Is.EqualTo(progress.Realm.PlayerLevel));
                Assert.That(loaded.Exp, Is.EqualTo(progress.Realm.Experience));
                Assert.That(loaded.RealmId, Is.EqualTo(progress.Realm.RealmId));
                Assert.That(loaded.RealmStage, Is.EqualTo(progress.Realm.RealmStage));
                Assert.That(loaded.PlayerId, Is.EqualTo("legacy-player"));
                Assert.That(loaded.Nickname, Is.EqualTo("旧档修士"));
                Assert.That(loaded.Kills, Is.EqualTo(17));
                Assert.That(loaded.SoftCurrency, Is.EqualTo(1234));
                Assert.That(loaded.PremiumCurrency, Is.EqualTo(56));
                Assert.That(loaded.StageElapsedSeconds, Is.EqualTo(87));
                Assert.That(loaded.InventoryJson, Does.Contain("legacyInventory"));
                Assert.That(loaded.EquippedInstanceIdsJson, Does.Contain("legacy-equipped"));
                Assert.That(loaded.LastActiveUnixSeconds, Is.EqualTo(111));

                repository.Save(loaded);
                var reloaded = repository.Load();
                var reloadedProgress = PlayerProgressStateCodec.Deserialize(reloaded.ProgressJson);
                Assert.That(reloaded.SchemaVersion, Is.EqualTo(PlayerSaveSnapshot.CurrentSchemaVersion));
                Assert.That(reloadedProgress.Realm.RealmId, Is.EqualTo("realm_qi_coalescence"));
                Assert.That(reloaded.Level, Is.EqualTo(reloadedProgress.Realm.PlayerLevel));
                Assert.That(reloaded.Exp, Is.EqualTo(reloadedProgress.Realm.Experience));
                Assert.That(reloaded.RealmId, Is.EqualTo(reloadedProgress.Realm.RealmId));
                Assert.That(reloaded.RealmStage, Is.EqualTo(reloadedProgress.Realm.RealmStage));
                var savedEnvelope = JsonUtility.FromJson<LegacyEnvelope>(File.ReadAllText(path));
                var savedPayload = JsonUtility.FromJson<PlayerSaveSnapshot>(savedEnvelope.payload);
                Assert.That(savedEnvelope.schemaVersion, Is.EqualTo(PlayerSaveSnapshot.CurrentSchemaVersion));
                Assert.That(savedPayload.SchemaVersion, Is.EqualTo(PlayerSaveSnapshot.CurrentSchemaVersion));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [TestCase(180d, "stage_1_10")]
        [TestCase(9999d, "stage_1_10")]
        [TestCase(-1d, "stage_1_1")]
        [TestCase(double.NaN, "stage_1_1")]
        [TestCase(double.PositiveInfinity, "stage_1_1")]
        public void MigrateV2_DerivesStageFromFrozenHistoricalPacing(double elapsedSeconds, string expectedStageId)
        {
            var progress = PlayerProgressStateCodec.MigrateV2(new PlayerSaveSnapshot
            {
                SchemaVersion = 2,
                StageElapsedSeconds = elapsedSeconds,
                ProgressJson = "{}"
            });

            Assert.That(progress.CurrentStageId, Is.EqualTo(expectedStageId));
        }

        [Test]
        public void Load_MigratesV1ThroughV2IntoV3WithoutLosingLastActiveTime()
        {
            var directory = Path.Combine(Path.GetTempPath(), "immortal-loot-test-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "save.json");
            Directory.CreateDirectory(directory);
            try
            {
                var legacy = new LegacyV2Snapshot
                {
                    SchemaVersion = 1, Level = 4, Exp = 55, RealmId = "realm_body_tempering",
                    RealmStage = 3, LastActiveUnixSeconds = 0, SavedAtUnixSeconds = 777
                };
                var payload = JsonUtility.ToJson(legacy);
                File.WriteAllText(path, JsonUtility.ToJson(new LegacyEnvelope
                {
                    schemaVersion = 1,
                    payload = payload,
                    checksum = Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(payload)))
                }));

                var loaded = new JsonPlayerSaveRepository(path).Load();
                var progress = PlayerProgressStateCodec.Deserialize(loaded.ProgressJson);

                Assert.That(loaded.SchemaVersion, Is.EqualTo(PlayerSaveSnapshot.CurrentSchemaVersion));
                Assert.That(loaded.LastActiveUnixSeconds, Is.EqualTo(777));
                Assert.That(progress.Realm.PlayerLevel, Is.EqualTo(4));
                Assert.That(progress.Realm.Experience, Is.EqualTo(55));
                Assert.That(progress.Realm.RealmStage, Is.EqualTo(3));
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

        [Serializable]
        private sealed class LegacyEnvelope
        {
            public int schemaVersion;
            public string payload;
            public string checksum;
        }

        [Serializable]
        private sealed class LegacyV2Snapshot
        {
            public int SchemaVersion = 2;
            public string PlayerId;
            public string Nickname;
            public int Level = 1;
            public long Exp;
            public string RealmId = "realm_body_tempering";
            public int RealmStage = 1;
            public int Kills;
            public long SoftCurrency;
            public long PremiumCurrency;
            public double StageElapsedSeconds;
            public long LastActiveUnixSeconds;
            public string InventoryJson = "{}";
            public string EquippedInstanceIdsJson = "{\"ids\":[]}";
            public string ProgressJson = "{}";
            public long SavedAtUnixSeconds;
        }
    }
}
