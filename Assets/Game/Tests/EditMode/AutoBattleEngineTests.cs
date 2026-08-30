using System.Collections.Generic;
using ImmortalLoot.Battle;
using ImmortalLoot.Character;
using ImmortalLoot.Config;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class AutoBattleEngineTests
    {
        [Test]
        public void Tick_AutoCastsSkillAndDamageOverTimeThenWins()
        {
            var skill = new SkillConfig
            {
                Id = "burn", Type = SkillType.Active, EffectType = SkillEffectType.DamageOverTime,
                Element = ElementType.Fire, Cooldown = 10, Multiplier = 1, EffectValue = 0.5f, Duration = 3
            };
            var engine = CreateEngine(20, 45, new[] { skill });
            var events = new List<BattleEvent>();
            engine.EventRaised += events.Add;
            for (var i = 0; i < 20 && engine.State == BattleState.Running; i++) engine.Tick(0.25f);
            Assert.That(engine.State, Is.EqualTo(BattleState.Victory));
            Assert.That(events.Exists(value => value.Type == BattleEventType.SkillCast), Is.True);
            Assert.That(events.Exists(value => value.Type == BattleEventType.DamageOverTime), Is.True);
        }

        [Test]
        public void PauseSpeedAndSkipControlLogicWithoutBindingPresentation()
        {
            var engine = CreateEngine(10, 100);
            engine.SetPaused(true);
            engine.Tick(5f);
            Assert.That(engine.Enemy.Hp, Is.EqualTo(100));
            engine.SetPaused(false);
            engine.SetSpeed(2f);
            engine.Tick(0.5f);
            Assert.That(engine.Enemy.Hp, Is.LessThan(100));
            var eventCount = 0;
            engine.EventRaised += _ => eventCount++;
            engine.SuppressPresentationEvents = true;
            engine.SkipToResult();
            Assert.That(engine.State, Is.EqualTo(BattleState.Victory));
            Assert.That(eventCount, Is.EqualTo(0));
        }

        [Test]
        public void BossEnragesAtConfiguredTime()
        {
            var player = new BattleActor("player", new CharacterStats { HP = 1000, Attack = 1 }, 5f);
            var boss = new BattleActor("boss", new CharacterStats { HP = 1000, Attack = 2 }, 1f, rank: MonsterRank.Boss, enrageSeconds: 1f);
            var engine = new AutoBattleEngine(player, boss, Calculator());
            var enraged = false;
            engine.EventRaised += value => enraged |= value.Type == BattleEventType.Enrage;
            engine.Tick(1f);
            Assert.That(enraged, Is.True);
        }

        [Test]
        public void BuffAndDebuffIncreaseSubsequentDamage()
        {
            var buff = new SkillConfig { Id = "buff", Type = SkillType.Active, EffectType = SkillEffectType.Buff, Cooldown = 99, EffectValue = 0.5f, Duration = 10 };
            var engine = CreateEngine(10, 100, new[] { buff });
            engine.Tick(1f);
            Assert.That(engine.Enemy.Hp, Is.EqualTo(85f).Within(0.01f));
        }

        [Test]
        public void AreaSkillDamagesEveryEnemyAndEncounterEndsAfterAllDie()
        {
            var aoe = new SkillConfig
            {
                Id = "chain", Type = SkillType.Active, TargetType = SkillTargetType.AllEnemies,
                EffectType = SkillEffectType.Damage, Element = ElementType.Lightning,
                Cooldown = 99, Multiplier = 1f
            };
            var player = new BattleActor("player", new CharacterStats { HP = 100, Attack = 20, CritDamage = 1.5f }, 10f, new[] { aoe });
            var enemies = new[]
            {
                new BattleActor("enemy_a", new CharacterStats { HP = 15, Attack = 1 }, 10f),
                new BattleActor("enemy_b", new CharacterStats { HP = 15, Attack = 1 }, 10f)
            };
            var engine = new AutoBattleEngine(player, enemies, Calculator());
            engine.Tick(0.1f);
            Assert.That(engine.State, Is.EqualTo(BattleState.Victory));
            Assert.That(enemies[0].IsAlive, Is.False);
            Assert.That(enemies[1].IsAlive, Is.False);
        }

        [Test]
        public void EncounterRejectsMoreThanTwentyEnemies()
        {
            var enemies = new List<BattleActor>();
            for (var i = 0; i < 21; i++) enemies.Add(new BattleActor("enemy_" + i, new CharacterStats { HP = 1, Attack = 1 }, 1f));
            Assert.That(() => new AutoBattleEngine(
                new BattleActor("player", new CharacterStats { HP = 1, Attack = 1 }, 1f), enemies, Calculator()),
                Throws.ArgumentException);
        }

        private static AutoBattleEngine CreateEngine(float attack, float enemyHp, IEnumerable<SkillConfig> skills = null)
        {
            var player = new BattleActor("player", new CharacterStats { HP = 100, Attack = attack, CritDamage = 1.5f }, 1f, skills);
            var enemy = new BattleActor("enemy", new CharacterStats { HP = enemyHp, Attack = 1, CritDamage = 1.5f }, 10f);
            return new AutoBattleEngine(player, enemy, Calculator());
        }

        private static DamageCalculator Calculator() => new DamageCalculator(new DamageFormulaConfig(), new DamageCalculatorTests.FixedRandom(0.99f));
    }
}
