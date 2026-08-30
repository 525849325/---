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

        public StageBattleFactory(GameConfigCatalog catalog, MonsterFactory monsters, DamageCalculator damage)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _monsters = monsters ?? throw new ArgumentNullException(nameof(monsters));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
        }

        public AutoBattleEngine Create(string stageId, BattleActor player)
        {
            if (!_catalog.Stages.TryGetValue(stageId, out var stage)) throw new ConfigException($"Stage '{stageId}' was not found.");
            var enemies = new List<BattleActor>();
            foreach (var monsterId in stage.MonsterGroup) enemies.Add(_monsters.Create(monsterId, stage.Chapter - 1));
            return new AutoBattleEngine(player, enemies, _damage);
        }
    }
}
