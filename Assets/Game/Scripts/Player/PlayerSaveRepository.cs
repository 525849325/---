using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ImmortalLoot.Cultivation;
using ImmortalLoot.Realm;
using ImmortalLoot.SpiritualRoot;
using ImmortalLoot.Stage;
using UnityEngine;

namespace ImmortalLoot.Player
{
    [Serializable]
    public sealed class PlayerSaveSnapshot
    {
        public const int CurrentSchemaVersion = 3;
        public int SchemaVersion = CurrentSchemaVersion;
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

    [Serializable]
    public sealed class PlayerProgressState
    {
        public string CurrentStageId = "stage_1_1";
        public int GuideStep;
        public bool TaskClaimed;
        public RealmProgressState Realm = new RealmProgressState();
        public StageProgressState Stage = new StageProgressState();
        public CultivationMethodState Cultivation = new CultivationMethodState();
        public SpiritualRootState SpiritualRoots = new SpiritualRootState();
    }

    public static class PlayerProgressStateCodec
    {
        private const double V2FirstBossSeconds = 180d;
        private const int V2StagesBeforeBoss = 9;

        public static string Serialize(PlayerProgressState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            Normalize(state);
            return JsonUtility.ToJson(state);
        }

        public static PlayerProgressState Deserialize(string json)
        {
            var state = new PlayerProgressState();
            var hasCultivationExperience = !string.IsNullOrWhiteSpace(json) &&
                json.IndexOf("\"CultivationExperience\"", StringComparison.Ordinal) >= 0;
            if (!string.IsNullOrWhiteSpace(json)) JsonUtility.FromJsonOverwrite(json, state);
            if (!hasCultivationExperience && state.Realm != null)
                state.Realm.CultivationExperience = Math.Max(0, state.Realm.Experience);
            Normalize(state);
            return state;
        }

        public static PlayerProgressState MigrateV2(PlayerSaveSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var state = Deserialize(snapshot.ProgressJson);
            state.Realm.RealmId = string.IsNullOrWhiteSpace(snapshot.RealmId)
                ? "realm_body_tempering"
                : snapshot.RealmId;
            state.Realm.RealmStage = Math.Max(1, snapshot.RealmStage);
            state.Realm.PlayerLevel = Math.Max(1, snapshot.Level);
            state.Realm.Experience = Math.Max(0, snapshot.Exp);
            state.Realm.CultivationExperience = Math.Max(0, snapshot.Exp);
            state.CurrentStageId = DeriveV2StageId(snapshot.StageElapsedSeconds);
            return state;
        }

        private static string DeriveV2StageId(double elapsedSeconds)
        {
            // Frozen v2 pacing contract: first boss at 180 seconds, with stages 1-9 evenly spaced before it.
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d)
                elapsedSeconds = 0d;
            var secondsPerStage = V2FirstBossSeconds / V2StagesBeforeBoss;
            var cappedSeconds = Math.Min(elapsedSeconds, V2FirstBossSeconds);
            var stageNumber = Math.Clamp(1 + (int)Math.Floor(cappedSeconds / secondsPerStage), 1, 10);
            return $"stage_1_{stageNumber}";
        }

        internal static bool IsMissingAggregate(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return true;
            var start = 0;
            var end = json.Length - 1;
            while (start <= end && char.IsWhiteSpace(json[start])) start++;
            while (end >= start && char.IsWhiteSpace(json[end])) end--;
            if (start > end || json[start] != '{' || json[end] != '}') return false;
            start++;
            while (start < end && char.IsWhiteSpace(json[start])) start++;
            return start == end;
        }

        internal static PlayerProgressState ResolveAggregate(PlayerSaveSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return IsMissingAggregate(snapshot.ProgressJson)
                ? MigrateV2(snapshot)
                : Deserialize(snapshot.ProgressJson);
        }

        internal static void SyncLegacyMirror(PlayerSaveSnapshot snapshot, PlayerProgressState state)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (state == null) throw new ArgumentNullException(nameof(state));
            Normalize(state);
            snapshot.Level = state.Realm.PlayerLevel;
            snapshot.Exp = state.Realm.Experience;
            snapshot.RealmId = state.Realm.RealmId;
            snapshot.RealmStage = state.Realm.RealmStage;
        }

        private static void Normalize(PlayerProgressState state)
        {
            state.CurrentStageId = string.IsNullOrWhiteSpace(state.CurrentStageId) ? "stage_1_1" : state.CurrentStageId;
            state.GuideStep = Math.Max(0, state.GuideStep);

            state.Realm ??= new RealmProgressState();
            state.Realm.RealmId = string.IsNullOrWhiteSpace(state.Realm.RealmId) ? "realm_body_tempering" : state.Realm.RealmId;
            state.Realm.RealmStage = Math.Max(1, state.Realm.RealmStage);
            state.Realm.PlayerLevel = Math.Max(1, state.Realm.PlayerLevel);
            state.Realm.Experience = Math.Max(0, state.Realm.Experience);
            state.Realm.CultivationExperience = Math.Max(0, state.Realm.CultivationExperience);
            state.Realm.BreakthroughMaterial = Math.Max(0, state.Realm.BreakthroughMaterial);
            var pendingTribulation = state.Realm.PendingTribulation;
            if (pendingTribulation != null &&
                string.IsNullOrWhiteSpace(pendingTribulation.Token) &&
                string.IsNullOrWhiteSpace(pendingTribulation.TargetRealmId) &&
                pendingTribulation.ReservedMaterial <= 0 &&
                pendingTribulation.RequiredExp <= 0)
                state.Realm.PendingTribulation = null;

            state.Stage ??= new StageProgressState();
            state.Stage.ClearedStageIds ??= new List<string>();
            NormalizeIds(state.Stage.ClearedStageIds);

            state.Cultivation ??= new CultivationMethodState();
            state.Cultivation.LearnedMethodIds ??= new List<string>();
            NormalizeIds(state.Cultivation.LearnedMethodIds);
            state.Cultivation.PrimaryMethodId ??= string.Empty;
            var auxiliary = state.Cultivation.AuxiliaryMethodIds;
            if (auxiliary == null || auxiliary.Length != 2)
            {
                var normalized = new string[2];
                if (auxiliary != null) Array.Copy(auxiliary, normalized, Math.Min(auxiliary.Length, normalized.Length));
                state.Cultivation.AuxiliaryMethodIds = normalized;
            }
            for (var i = 0; i < state.Cultivation.AuxiliaryMethodIds.Length; i++)
                state.Cultivation.AuxiliaryMethodIds[i] ??= string.Empty;

            state.SpiritualRoots ??= new SpiritualRootState();
            state.SpiritualRoots.Roots ??= new List<SpiritualRootProgress>();
            state.SpiritualRoots.GrantRecords ??= new List<SpiritualRootGrantRecord>();
            NormalizeSpiritualRoots(state.SpiritualRoots);
        }

        private static void NormalizeSpiritualRoots(SpiritualRootState state)
        {
            var rootIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
            var rootWriteIndex = 0;
            for (var i = 0; i < state.Roots.Count; i++)
            {
                var progress = state.Roots[i];
                if (progress == null || string.IsNullOrWhiteSpace(progress.RootId)) continue;

                progress.RootId = progress.RootId.Trim();
                progress.Level = Math.Max(0, progress.Level);
                if (rootIndexes.TryGetValue(progress.RootId, out var existingIndex))
                {
                    state.Roots[existingIndex].Level = Math.Max(state.Roots[existingIndex].Level, progress.Level);
                    continue;
                }

                rootIndexes.Add(progress.RootId, rootWriteIndex);
                state.Roots[rootWriteIndex++] = progress;
            }
            if (rootWriteIndex < state.Roots.Count)
                state.Roots.RemoveRange(rootWriteIndex, state.Roots.Count - rootWriteIndex);

            var grantIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
            var grantWriteIndex = 0;
            for (var i = 0; i < state.GrantRecords.Count; i++)
            {
                var record = state.GrantRecords[i];
                if (record == null || string.IsNullOrWhiteSpace(record.TribulationToken)) continue;

                record.TribulationToken = record.TribulationToken.Trim();
                record.RootId = string.IsNullOrWhiteSpace(record.RootId) ? string.Empty : record.RootId.Trim();
                record.NewLevel = Math.Max(0, record.NewLevel);
                if (grantIndexes.TryGetValue(record.TribulationToken, out var existingIndex))
                {
                    var existing = state.GrantRecords[existingIndex];
                    if (string.IsNullOrEmpty(existing.RootId) && !string.IsNullOrEmpty(record.RootId))
                        existing.RootId = record.RootId;
                    if (string.Equals(existing.RootId, record.RootId, StringComparison.Ordinal))
                        existing.NewLevel = Math.Max(existing.NewLevel, record.NewLevel);
                    continue;
                }

                grantIndexes.Add(record.TribulationToken, grantWriteIndex);
                state.GrantRecords[grantWriteIndex++] = record;
            }
            if (grantWriteIndex < state.GrantRecords.Count)
                state.GrantRecords.RemoveRange(grantWriteIndex, state.GrantRecords.Count - grantWriteIndex);
        }

        private static void NormalizeIds(List<string> ids)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var writeIndex = 0;
            for (var i = 0; i < ids.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(ids[i])) continue;
                var id = ids[i].Trim();
                if (!seen.Add(id)) continue;
                ids[writeIndex++] = id;
            }
            if (writeIndex < ids.Count) ids.RemoveRange(writeIndex, ids.Count - writeIndex);
        }
    }

    [Serializable]
    internal sealed class SaveEnvelope
    {
        public int schemaVersion;
        public string payload;
        public string checksum;
    }

    public interface IPlayerSaveRepository
    {
        void Save(PlayerSaveSnapshot snapshot);
        PlayerSaveSnapshot Load();
        bool Exists { get; }
    }

    public sealed class JsonPlayerSaveRepository : IPlayerSaveRepository
    {
        private readonly string _path;
        private readonly Func<DateTime> _utcNow;
        public bool Exists => File.Exists(_path);
        public JsonPlayerSaveRepository(string path, Func<DateTime> utcNow = null)
        {
            _path = string.IsNullOrWhiteSpace(path) ? throw new ArgumentException("Save path is required.") : Path.GetFullPath(path);
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public void Save(PlayerSaveSnapshot snapshot)
        {
            if (snapshot == null || snapshot.SchemaVersion != PlayerSaveSnapshot.CurrentSchemaVersion) throw new InvalidOperationException("Unsupported save schema.");
            var progress = PlayerProgressStateCodec.ResolveAggregate(snapshot);
            PlayerProgressStateCodec.SyncLegacyMirror(snapshot, progress);
            snapshot.ProgressJson = PlayerProgressStateCodec.Serialize(progress);
            snapshot.SavedAtUnixSeconds = new DateTimeOffset(_utcNow()).ToUnixTimeSeconds();
            var payload = JsonUtility.ToJson(snapshot);
            var envelope = new SaveEnvelope { schemaVersion = PlayerSaveSnapshot.CurrentSchemaVersion, payload = payload, checksum = Checksum(payload) };
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(envelope), new UTF8Encoding(false));
            if (File.Exists(_path)) File.Replace(temporary, _path, null);
            else File.Move(temporary, _path);
        }

        public PlayerSaveSnapshot Load()
        {
            if (!Exists) return null;
            var envelope = JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(_path, Encoding.UTF8));
            if (envelope == null || envelope.schemaVersion < 1 || envelope.schemaVersion > PlayerSaveSnapshot.CurrentSchemaVersion || string.IsNullOrEmpty(envelope.payload) || !FixedEquals(envelope.checksum, Checksum(envelope.payload)))
                throw new InvalidDataException("Save checksum or schema is invalid.");
            var snapshot = JsonUtility.FromJson<PlayerSaveSnapshot>(envelope.payload);
            if (snapshot == null || snapshot.SchemaVersion < 1 || snapshot.SchemaVersion > PlayerSaveSnapshot.CurrentSchemaVersion) throw new InvalidDataException("Save payload is invalid.");
            if (snapshot.SchemaVersion == 1)
            {
                snapshot.LastActiveUnixSeconds = snapshot.SavedAtUnixSeconds;
                snapshot.SchemaVersion = 2;
            }
            if (snapshot.SchemaVersion == 2)
            {
                var progress = PlayerProgressStateCodec.MigrateV2(snapshot);
                PlayerProgressStateCodec.SyncLegacyMirror(snapshot, progress);
                snapshot.ProgressJson = PlayerProgressStateCodec.Serialize(progress);
                snapshot.SchemaVersion = PlayerSaveSnapshot.CurrentSchemaVersion;
            }
            else
            {
                var progress = PlayerProgressStateCodec.ResolveAggregate(snapshot);
                PlayerProgressStateCodec.SyncLegacyMirror(snapshot, progress);
                snapshot.ProgressJson = PlayerProgressStateCodec.Serialize(progress);
            }
            return snapshot;
        }

        private static string Checksum(string payload)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        }

        private static bool FixedEquals(string left, string right)
        {
            if (left == null || right == null) return false;
            var a = Encoding.UTF8.GetBytes(left); var b = Encoding.UTF8.GetBytes(right);
            return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
        }

        public static string TryQuarantine(string path, DateTime? utcNow = null, Func<string> uniqueSuffix = null)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            try
            {
                var stamp = (utcNow ?? DateTime.UtcNow).ToUniversalTime().ToString("yyyyMMdd'T'HHmmss");
                var suffix = uniqueSuffix?.Invoke() ?? Guid.NewGuid().ToString("N");
                var destination = path + ".corrupt-" + stamp + "-" + suffix;
                if (File.Exists(destination)) return null;
                File.Move(path, destination);
                return destination;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unable to quarantine corrupt save: " + exception.Message);
                return null;
            }
        }

        public static string DefaultPath
        {
            get
            {
#if UNITY_INCLUDE_TESTS
                if (!string.IsNullOrWhiteSpace(_defaultPathOverrideForTests)) return _defaultPathOverrideForTests;
#endif
                return Path.Combine(Application.persistentDataPath, "immortal-loot-save.json");
            }
        }

#if UNITY_INCLUDE_TESTS
        private static string _defaultPathOverrideForTests;

        public static IDisposable OverrideDefaultPathForTests(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A test save path is required.", nameof(path));
            var previous = _defaultPathOverrideForTests;
            _defaultPathOverrideForTests = Path.GetFullPath(path);
            return new DefaultPathOverrideScope(previous);
        }

        private sealed class DefaultPathOverrideScope : IDisposable
        {
            private readonly string _previous;
            private bool _disposed;

            public DefaultPathOverrideScope(string previous) => _previous = previous;

            public void Dispose()
            {
                if (_disposed) return;
                _defaultPathOverrideForTests = _previous;
                _disposed = true;
            }
        }
#endif

        public static JsonPlayerSaveRepository CreateDefault() => new JsonPlayerSaveRepository(DefaultPath);
    }
}
