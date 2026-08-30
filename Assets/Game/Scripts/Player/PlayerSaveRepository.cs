using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ImmortalLoot.Player
{
    [Serializable]
    public sealed class PlayerSaveSnapshot
    {
        public const int CurrentSchemaVersion = 2;
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
                snapshot.SchemaVersion = PlayerSaveSnapshot.CurrentSchemaVersion;
                snapshot.LastActiveUnixSeconds = snapshot.SavedAtUnixSeconds;
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

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "immortal-loot-save.json");
        public static JsonPlayerSaveRepository CreateDefault() => new JsonPlayerSaveRepository(DefaultPath);
    }
}
