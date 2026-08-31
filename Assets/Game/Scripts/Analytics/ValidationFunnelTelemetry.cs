using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ImmortalLoot.Analytics
{
    [Serializable]
    public sealed class ValidationEvent
    {
        public string eventName;
        public string sessionId;
        public string utc;
        public double elapsedSeconds;
        public int stage;
        public long power;
        public string itemQuality;
        public long value;
    }

    public interface IValidationEventSink
    {
        void Write(ValidationEvent value);
    }

    public sealed class JsonlValidationEventSink : IValidationEventSink
    {
        private readonly string _path;

        public JsonlValidationEventSink(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Telemetry path is required.", nameof(path));
            _path = path;
        }

        public void Write(ValidationEvent value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.AppendAllText(_path, JsonUtility.ToJson(value) + Environment.NewLine);
            }
            catch (Exception exception) { Debug.LogWarning("Validation telemetry write failed: " + exception.Message); }
        }
    }

    public sealed class ValidationFunnelTracker
    {
        private readonly IValidationEventSink _sink;
        private readonly Func<DateTime> _utcNow;
        private readonly Func<bool> _isCollectionAllowed;
        private readonly HashSet<string> _recorded = new HashSet<string>(StringComparer.Ordinal);
        private readonly string _sessionId;

        public ValidationFunnelTracker(
            IValidationEventSink sink,
            string sessionId = null,
            Func<DateTime> utcNow = null,
            Func<bool> isCollectionAllowed = null)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _sessionId = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : sessionId;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _isCollectionAllowed = isCollectionAllowed ?? (() => true);
        }

        public void TrackOnce(string eventName, double elapsedSeconds = 0d, int stage = 0, long power = 0, string itemQuality = "", long value = 0)
        {
            if (string.IsNullOrWhiteSpace(eventName)) throw new ArgumentException("Event name is required.", nameof(eventName));
            if (!_isCollectionAllowed()) return;
            if (!_recorded.Add(eventName)) return;
            _sink.Write(new ValidationEvent
            {
                eventName = eventName,
                sessionId = _sessionId,
                utc = _utcNow().ToString("O"),
                elapsedSeconds = Math.Max(0d, elapsedSeconds),
                stage = Math.Max(0, stage),
                power = Math.Max(0, power),
                itemQuality = itemQuality ?? string.Empty,
                value = Math.Max(0, value)
            });
        }
    }
}
