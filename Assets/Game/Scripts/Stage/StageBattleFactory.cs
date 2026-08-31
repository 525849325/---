using System;
using System.Collections.Generic;
using ImmortalLoot.Battle;
using ImmortalLoot.Config;

namespace ImmortalLoot.Stage
{
    public sealed class StageBattleFactory
    {
        private readonly GameConfigCatalog _catalog;
        private readonly MonsterFactory _monsters;
        private readonly DamageCalculator _damage;
        private readonly CycleScalingPolicy _cycleScaling;

        public StageBattleFactory(GameConfigCatalog catalog, MonsterFactory monsters, DamageCalculator damage)
            : this(catalog, monsters, damage, null)
        {
        }

        public StageBattleFactory(GameConfigCatalog catalog, MonsterFactory monsters, DamageCalculator damage, CycleScalingPolicy cycleScaling)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _monsters = monsters ?? throw new ArgumentNullException(nameof(monsters));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
            _cycleScaling = cycleScaling;
        }

        public AutoBattleEngine Create(string stageId, BattleActor player) => Create(stageId, player, 1);

        public AutoBattleEngine Create(string stageId, BattleActor player, int cycleIndex)
        {
            if (!_catalog.Stages.TryGetValue(stageId, out var stage)) throw new ConfigException($"Stage '{stageId}' was not found.");
            if (cycleIndex > 1 && _cycleScaling == null)
                throw new InvalidOperationException("A cycle scaling policy is required for encounters after cycle one.");
            var cycleMultiplier = _cycleScaling?.EnemyMultiplier(cycleIndex) ?? 1f;
            var enemies = new List<BattleActor>();
            foreach (var monsterId in stage.MonsterGroup)
                enemies.Add(_monsters.Create(monsterId, stage.Chapter - 1, cycleMultiplier));
            return new AutoBattleEngine(player, enemies, _damage);
        }
    }
}
