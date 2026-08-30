using System;
using System.Collections.Generic;
using ImmortalLoot.Character;
using ImmortalLoot.Config;

namespace ImmortalLoot.Battle
{
    public enum BattleState { Running, Paused, Victory, Defeat }
    public enum BattleEventType { BasicAttack, SkillCast, DamageOverTime, Heal, Enrage }

    public readonly struct BattleEvent
    {
        public BattleEventType Type { get; }
        public string SourceId { get; }
        public string TargetId { get; }
        public string SkillId { get; }
        public float Value { get; }
        public bool IsCritical { get; }

        public BattleEvent(BattleEventType type, string sourceId, string targetId, float value, string skillId = "", bool isCritical = false)
        {
            Type = type; SourceId = sourceId; TargetId = targetId; Value = value; SkillId = skillId; IsCritical = isCritical;
        }
    }

    public sealed class BattleActor
    {
        private readonly List<SkillRuntime> _skills = new List<SkillRuntime>();
        private readonly List<TimedEffect> _effects = new List<TimedEffect>();
        internal float AttackTimer;
        internal bool Enraged;

        public string Id { get; }
        public MonsterRank Rank { get; }
        public CharacterStats Stats { get; }
        public float MaxHp { get; }
        public float Hp { get; private set; }
        public float BasicAttackInterval { get; }
        public float EnrageSeconds { get; }
        public bool IsAlive => Hp > 0f;
        internal IReadOnlyList<SkillRuntime> Skills => _skills;
        internal List<TimedEffect> Effects => _effects;

        public BattleActor(string id, CharacterStats stats, float basicAttackInterval, IEnumerable<SkillConfig> skills = null, MonsterRank rank = MonsterRank.Normal, float enrageSeconds = 0f)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Actor id is required.") : id;
            Stats = stats?.Clone() ?? throw new ArgumentNullException(nameof(stats));
            MaxHp = Math.Max(1f, Stats.HP);
            Hp = MaxHp;
            BasicAttackInterval = Math.Max(0.1f, basicAttackInterval);
            Rank = rank;
            EnrageSeconds = Math.Max(0f, enrageSeconds);
            if (skills == null) return;
            foreach (var skill in skills)
            {
                if (skill.Type == SkillType.Passive) Stats.DamageBonus += Math.Max(0f, skill.EffectValue);
                else _skills.Add(new SkillRuntime(skill));
            }
        }

        internal void TakeDamage(float amount) => Hp = Math.Max(0f, Hp - Math.Max(0f, amount));
        internal float Heal(float amount)
        {
            var before = Hp;
            Hp = Math.Min(MaxHp, Hp + Math.Max(0f, amount));
            return Hp - before;
        }
    }

    internal sealed class SkillRuntime
    {
        public SkillConfig Config { get; }
        public float CooldownRemaining;
        public SkillRuntime(SkillConfig config) { Config = config ?? throw new ArgumentNullException(nameof(config)); CooldownRemaining = 0f; }
    }

    internal sealed class TimedEffect
    {
        public SkillEffectType Type;
        public string SourceId;
        public string SkillId;
        public float Value;
        public float Remaining;
        public float TickTimer;
    }

    public sealed class AutoBattleEngine
    {
        private readonly DamageCalculator _damage;
        private readonly List<BattleActor> _enemies;
        private float _elapsed;
        public BattleActor Player { get; }
        public BattleActor Enemy
        {
            get
            {
                foreach (var enemy in _enemies) if (enemy.IsAlive) return enemy;
                return _enemies[0];
            }
        }
        public IReadOnlyList<BattleActor> Enemies => _enemies;
        public BattleState State { get; private set; } = BattleState.Running;
        public float Speed { get; private set; } = 1f;
        public bool SuppressPresentationEvents { get; set; }
        public event Action<BattleEvent> EventRaised;
        public event Action<BattleState> Finished;

        public AutoBattleEngine(BattleActor player, BattleActor enemy, DamageCalculator damage)
            : this(player, new[] { enemy }, damage) { }

        public AutoBattleEngine(BattleActor player, IEnumerable<BattleActor> enemies, DamageCalculator damage)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
            _enemies = enemies == null ? throw new ArgumentNullException(nameof(enemies)) : new List<BattleActor>(enemies);
            if (_enemies.Count == 0 || _enemies.Count > 20) throw new ArgumentException("An encounter requires 1 to 20 enemies.", nameof(enemies));
            if (_enemies.Exists(value => value == null)) throw new ArgumentException("Enemy list cannot contain null.", nameof(enemies));
        }

        public void SetPaused(bool paused)
        {
            if (State == BattleState.Victory || State == BattleState.Defeat) return;
            State = paused ? BattleState.Paused : BattleState.Running;
        }

        public void SetSpeed(float speed) => Speed = Math.Max(0.25f, Math.Min(10f, speed));

        public void Tick(float unscaledDeltaTime)
        {
            if (State != BattleState.Running || unscaledDeltaTime <= 0f) return;
            var delta = unscaledDeltaTime * Speed;
            _elapsed += delta;
            CheckEnrage();
            TickActor(Player, Enemy, delta);
            if (!AnyEnemyAlive()) { Finish(BattleState.Victory); return; }
            foreach (var enemy in _enemies)
            {
                if (!enemy.IsAlive) continue;
                TickActor(enemy, Player, delta);
                if (!Player.IsAlive) { Finish(BattleState.Defeat); return; }
            }
        }

        public void SkipToResult(float maxSimulationSeconds = 300f)
        {
            var previous = SuppressPresentationEvents;
            SuppressPresentationEvents = true;
            var simulated = 0f;
            while (State == BattleState.Running && simulated < maxSimulationSeconds)
            {
                Tick(0.05f);
                simulated += 0.05f;
            }
            SuppressPresentationEvents = previous;
        }

        private void TickActor(BattleActor source, BattleActor target, float delta)
        {
            TickEffects(source, delta);
            if (!source.IsAlive || !target.IsAlive) return;
            foreach (var skill in source.Skills)
            {
                skill.CooldownRemaining -= delta;
                if (skill.CooldownRemaining <= 0f)
                {
                    CastSkill(source, target, skill.Config);
                    skill.CooldownRemaining = Math.Max(0.1f, skill.Config.Cooldown);
                    if (!target.IsAlive) return;
                }
            }
            source.AttackTimer += delta;
            while (source.AttackTimer >= source.BasicAttackInterval && target.IsAlive)
            {
                source.AttackTimer -= source.BasicAttackInterval;
                DealDamage(source, target, 1f, ElementType.None, BattleEventType.BasicAttack, string.Empty, true);
            }
        }

        private void CastSkill(BattleActor source, BattleActor target, SkillConfig skill)
        {
            Emit(new BattleEvent(BattleEventType.SkillCast, source.Id, target.Id, 0f, skill.Id));
            if (ReferenceEquals(source, Player) && skill.TargetType == SkillTargetType.AllEnemies)
            {
                foreach (var enemy in _enemies) if (enemy.IsAlive) ApplySkill(source, enemy, skill);
                return;
            }
            ApplySkill(source, target, skill);
        }

        private void ApplySkill(BattleActor source, BattleActor target, SkillConfig skill)
        {
            switch (skill.EffectType)
            {
                case SkillEffectType.Damage:
                    DealDamage(source, target, skill.Multiplier, skill.Element, BattleEventType.SkillCast, skill.Id, true);
                    break;
                case SkillEffectType.DamageOverTime:
                    DealDamage(source, target, skill.Multiplier, skill.Element, BattleEventType.SkillCast, skill.Id, true);
                    target.Effects.Add(new TimedEffect { Type = SkillEffectType.DamageOverTime, SourceId = source.Id, SkillId = skill.Id, Value = source.Stats.Attack * skill.EffectValue, Remaining = skill.Duration, TickTimer = 1f });
                    break;
                case SkillEffectType.Heal:
                    var healed = source.Heal(source.MaxHp * skill.EffectValue);
                    Emit(new BattleEvent(BattleEventType.Heal, source.Id, source.Id, healed, skill.Id));
                    break;
                case SkillEffectType.Buff:
                    source.Effects.Add(new TimedEffect { Type = SkillEffectType.Buff, SourceId = source.Id, SkillId = skill.Id, Value = skill.EffectValue, Remaining = skill.Duration });
                    break;
                case SkillEffectType.Debuff:
                    target.Effects.Add(new TimedEffect { Type = SkillEffectType.Debuff, SourceId = source.Id, SkillId = skill.Id, Value = skill.EffectValue, Remaining = skill.Duration });
                    break;
            }
        }

        private void DealDamage(BattleActor source, BattleActor target, float multiplier, ElementType element, BattleEventType eventType, string skillId, bool canCrit)
        {
            var sourceStats = source.Stats.Clone();
            var targetStats = target.Stats.Clone();
            foreach (var effect in source.Effects) if (effect.Type == SkillEffectType.Buff) sourceStats.DamageBonus += effect.Value;
            foreach (var effect in target.Effects) if (effect.Type == SkillEffectType.Debuff) targetStats.DamageReduction -= effect.Value;
            if (source.Enraged) sourceStats.DamageBonus += 1f;
            var result = _damage.Calculate(new DamageRequest(sourceStats, targetStats, multiplier, element, canCrit));
            target.TakeDamage(result.Amount);
            Emit(new BattleEvent(eventType, source.Id, target.Id, result.Amount, skillId, result.IsCritical));
            if (source.Stats.LifeSteal > 0f) source.Heal(result.Amount * Math.Min(0.95f, source.Stats.LifeSteal));
        }

        private void TickEffects(BattleActor actor, float delta)
        {
            for (var i = actor.Effects.Count - 1; i >= 0; i--)
            {
                var effect = actor.Effects[i];
                effect.Remaining -= delta;
                if (effect.Type == SkillEffectType.DamageOverTime)
                {
                    effect.TickTimer -= delta;
                    while (effect.TickTimer <= 0f && actor.IsAlive)
                    {
                        actor.TakeDamage(Math.Max(1f, effect.Value));
                        Emit(new BattleEvent(BattleEventType.DamageOverTime, effect.SourceId, actor.Id, Math.Max(1f, effect.Value), effect.SkillId));
                        effect.TickTimer += 1f;
                    }
                }
                if (effect.Remaining <= 0f) actor.Effects.RemoveAt(i);
            }
        }

        private void CheckEnrage()
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.Rank != MonsterRank.Boss || enemy.Enraged || enemy.EnrageSeconds <= 0f || _elapsed < enemy.EnrageSeconds) continue;
                enemy.Enraged = true;
                Emit(new BattleEvent(BattleEventType.Enrage, enemy.Id, Player.Id, 1f));
            }
        }

        private bool AnyEnemyAlive()
        {
            foreach (var enemy in _enemies) if (enemy.IsAlive) return true;
            return false;
        }

        private void Finish(BattleState result)
        {
            if (State == BattleState.Victory || State == BattleState.Defeat) return;
            State = result;
            Finished?.Invoke(result);
        }

        private void Emit(BattleEvent value)
        {
            if (!SuppressPresentationEvents) EventRaised?.Invoke(value);
        }
    }
}
