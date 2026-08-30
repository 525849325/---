using System;
using System.Collections.Generic;
using System.IO;
using ImmortalLoot.Analytics;
using NUnit.Framework;
using UnityEngine;

namespace ImmortalLoot.Tests
{
    public sealed class ValidationFunnelTelemetryTests
    {
        [Test]
        public void TrackOnce_WritesStructuredCorrelatedEventOnlyOnce()
        {
            var sink = new MemorySink();
            var tracker = new ValidationFunnelTracker(sink, "session-1", () => new DateTime(2026, 8, 30, 1, 2, 3, DateTimeKind.Utc));

            tracker.TrackOnce("first_equipment_drop", 42.5f, 3, 120, "Rare", 18);
            tracker.TrackOnce("first_equipment_drop", 43f, 4, 130, "Epic", 20);

            Assert.That(sink.Events, Has.Count.EqualTo(1));
            Assert.That(sink.Events[0].sessionId, Is.EqualTo("session-1"));
            Assert.That(sink.Events[0].utc, Is.EqualTo("2026-08-30T01:02:03.0000000Z"));
            Assert.That(sink.Events[0].itemQuality, Is.EqualTo("Rare"));
            Assert.That(sink.Events[0].power, Is.EqualTo(120));
        }

        [Test]
        public void JsonlSink_WritesMachineReadableEventWithoutPlayerIdentity()
        {
            var path = Path.Combine(Path.GetTempPath(), "immortal-validation-" + Guid.NewGuid().ToString("N") + ".jsonl");
            try
            {
                new JsonlValidationEventSink(path).Write(new ValidationEvent { eventName = "boss_defeated", sessionId = "s", stage = 10 });
                var parsed = JsonUtility.FromJson<ValidationEvent>(File.ReadAllText(path));
                Assert.That(parsed.eventName, Is.EqualTo("boss_defeated"));
                Assert.That(parsed.stage, Is.EqualTo(10));
                Assert.That(File.ReadAllText(path), Does.Not.Contain("playerId"));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        private sealed class MemorySink : IValidationEventSink
        {
            public readonly List<ValidationEvent> Events = new List<ValidationEvent>();
            public void Write(ValidationEvent value) => Events.Add(value);
        }
    }
}
