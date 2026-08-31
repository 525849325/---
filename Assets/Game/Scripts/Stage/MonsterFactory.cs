using System;
using System.Collections.Generic;
using ImmortalLoot.Battle;
using ImmortalLoot.Character;
using ImmortalLoot.Config;

namespace ImmortalLoot.Stage
{
    public sealed class MonsterFactory
    {
        private readonly GameConfigCatalog _catalog;

        public MonsterFactory(GameConfigCatalog catalog) => _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

        public BattleActor Create(string monsterId, int levelScale = 0) => Create(monsterId, levelScale, 1f);

        public BattleActor Create(string monsterId, int levelScale, float statMultiplier)
        {
            if (!_catalog.Monsters.TryGetValue(monsterId, out var config)) throw new ConfigException($"Monster '{monsterId}' was not found.");
            if (float.IsNaN(statMultiplier) || float.IsInfinity(statMultiplier) || statMultiplier < 1f)
                throw new ArgumentOutOfRangeException(nameof(statMultiplier));
            var scale = (1f + Math.Max(0, levelScale) * 0.08f) * statMultiplier;
            var skills = new List<SkillConfig>();
            foreach (var skillId in config.SkillIds) skills.Add(_catalog.Skills[skillId]);
            return new BattleActor(config.Id, new CharacterStats
            {
                HP = config.MaxHp * scale,
                Attack = config.Attack * scale,
                Defense = config.Defense * scale,
                CritDamage = 1.5f
            }, config.AttackInterval, skills, config.Rank, config.EnrageSeconds);
        }
    }
}
