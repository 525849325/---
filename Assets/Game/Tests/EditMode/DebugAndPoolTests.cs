using System;
using ImmortalLoot.Config;
using ImmortalLoot.Core;
using ImmortalLoot.Debugging;
using ImmortalLoot.Equipment;
using NUnit.Framework;
using UnityEngine;

namespace ImmortalLoot.Tests
{
    public sealed class DebugAndPoolTests
    {
        [Test]
        public void DebugService_ExecutesHighValueDevelopmentCommands()
        {
            var catalog = new JsonConfigRepository(new ResourcesConfigSource()).LoadAll();
            var state = new DebugGameState();
            var service = new GameDebugService(state, catalog, new FixedRandom());
            service.AddSoftCurrency(1000); service.AddPremiumCurrency(60); service.AddExp(500); service.LevelUp(4); service.Breakthrough();
            service.UnlockStage("stage_1_10"); service.LearnMethod("method_cinder_scripture"); service.SetRoot("root_fire", 3);
            service.SimulateOffline8Hours(new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc)); service.SimulatePayment(60);
            var item = service.GenerateEquipment("weapon_cloudsteel_blade", 10, EquipmentQuality.Mythic, "attack_flat");
            Assert.That(state.SoftCurrency, Is.EqualTo(1000));
            Assert.That(state.Level, Is.EqualTo(5));
            Assert.That(state.UnlockedStages, Does.Contain("stage_1_10"));
            Assert.That(item.Affixes[0].AffixId, Is.EqualTo("attack_flat"));
            Assert.That((new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc) - state.LastOfflineTimeUtc).TotalHours, Is.EqualTo(8));
        }

        [Test]
        public void GameObjectPool_ReusesReturnedObject()
        {
            var prefab = new GameObject("pool-prefab");
            var pool = new GameObjectPool(prefab, null, 1);
            var first = pool.Rent(); pool.Return(first); var second = pool.Rent();
            Assert.That(second, Is.SameAs(first));
            Assert.That(pool.CreatedCount, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(second); UnityEngine.Object.DestroyImmediate(prefab);
        }

        private sealed class FixedRandom : IRandomSource
        {
            public float Value() => 0.5f;
            public int Range(int minInclusive, int maxExclusive) => minInclusive;
            public float Range(float minInclusive, float maxInclusive) => minInclusive;
        }
    }
}
