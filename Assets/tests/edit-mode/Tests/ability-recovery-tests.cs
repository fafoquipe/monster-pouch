using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class AbilityRecoveryTests
    {
        GameObject root;
        BoardManager board;
        CombatSimulation simulation;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp] public void SetUp()
        {
            root = new GameObject("ability-recovery-tests");
            board = root.AddComponent<BoardManager>(); board.BuildBoard();
        }
        [TearDown] public void TearDown() { simulation?.Stop(); Object.DestroyImmediate(root); }
        static void Parameter(TrickDefinition ability, string key, float value)
        {
            ability.Parameters = AbilityBalance.Resolve(ability).Where(p => p.Key != key)
                .Concat(new[] { new AbilityParameter { Key = key, Value = value } }).ToArray();
        }
        BattleUnit Unit(UnitDefinition definition, int x, int y, BoardSide side, int trick = -1)
        {
            var go = new GameObject(definition.Id + "-" + x + "-" + y); go.transform.SetParent(root.transform);
            var owned = new OwnedUnit(definition); if (trick >= 0) owned.Tricks[trick] = true;
            var unit = go.AddComponent<WhelpUnit>(); unit.Initialize(go.name, side, UnitStats.FromDefinition(definition, owned));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y)); return unit;
        }
        BattleUnit Enemy(int y = 4)
        {
            var unit = Unit(new UnitDefinition { Id = "target", MaxHealth = 10000, Damage = 0,
                AttackRange = 9, AttackInterval = 100, MoveInterval = 100 }, 2, y, BoardSide.Red);
            unit.IsPassiveTrainingTarget = true; return unit;
        }
        object Clock(BattleUnit unit) => ((IEnumerable)typeof(CombatSimulation).GetField("actors", Private).GetValue(simulation))
            .Cast<object>().Single(c => ReferenceEquals(c.GetType().GetField("Unit").GetValue(c), unit));
        static int Attacks(object clock) => (int)clock.GetType().GetField("Attacks").GetValue(clock);
        void Begin(BattleUnit actor, BattleUnit target, int incomeTick = -1)
        {
            simulation = new CombatSimulation(board, 43); simulation.Begin(new[] { actor, target });
            if (incomeTick >= 0) Clock(actor).GetType().GetField("NextIncome").SetValue(Clock(actor), incomeTick);
        }

        [TestCase(.3f)] [TestCase(.85f)]
        public void HymayFinishesLandingBeforeOneFullFirstHitPreparation(float travel)
        {
            var definition = DocumentedRoster.CreateHymay();
            definition.AttackRange = 9; definition.AttackInterval = 5; definition.AttackWindup = .4f;
            definition.AttackHold = definition.AttackRecovery = 0; definition.Damage = 7;
            definition.EnergyPerSecond = definition.EnergyPerAttack = definition.EnergyOnDamage = 0;
            Parameter(definition.BaseAbility, "castDuration", .1f);
            Parameter(definition.BaseAbility, "holdDuration", 0); Parameter(definition.BaseAbility, "recoveryDuration", 0);
            Parameter(definition.BaseAbility, "pullDuration", .2f); Parameter(definition.BaseAbility, "postArrivalStun", 0);
            Parameter(definition.BaseAbility, "damageOverride", 0);
            Parameter(definition.Tricks[2], "travelDuration", travel); Parameter(definition.Tricks[2], "distance", 1);
            var actor = Unit(definition, 2, 5, BoardSide.Blue, 2); var target = Enemy(3);
            Begin(actor, target); actor.AddEnergy(actor.BaseStats.EnergyMax);
            var clock = Clock(actor); bool flying = false; int attacksBeforeFlight = -1;
            float landed = -1, first = -1, release = -1;
            float landingDuration = AbilityTiming.Ticks(AbilityTiming.BackflipLandingDuration) * CombatSimulation.TickDuration;
            simulation.Impacted += (source, victim, damage) =>
            { if (source == actor && first >= 0 && damage > 0 && release < 0) release = simulation.Elapsed; };
            for (int i = 0; i < 60 && release < 0; i++)
            {
                int count = Attacks(clock); simulation.Step(.1f);
                if (!flying && actor.IsFlying) attacksBeforeFlight = count;
                if (flying && !actor.IsFlying && landed < 0) landed = simulation.Elapsed;
                flying |= actor.IsFlying;
                if (flying && (landed < 0 || simulation.Elapsed < landed + landingDuration - .001f))
                    Assert.AreEqual(attacksBeforeFlight, Attacks(clock), "Flight and the landing squash still own the body.");
                if (landed >= 0 && Attacks(clock) > count && first < 0)
                {
                    first = simulation.Elapsed;
                    Assert.AreEqual(attacksBeforeFlight + 1, Attacks(clock), "Only one new basic starts after landing.");
                    // The short authored cast lets an earlier basic launch while the hook travels.
                    // Its projectile may land during flight; it cannot be mistaken for this basic.
                    var basics = ((IEnumerable)typeof(CombatSimulation).GetField("pending", Private).GetValue(simulation))
                        .Cast<object>().Where(p => ReferenceEquals(p.GetType().GetField("Source").GetValue(p), clock) &&
                            !(bool)p.GetType().GetField("Super").GetValue(p) && (int)p.GetType().GetField("Damage").GetValue(p) > 0).ToArray();
                    Assert.AreEqual(1, basics.Length, "No earlier projectile may overlap the measured post-landing basic.");
                    int preparedTick = Mathf.RoundToInt(first / CombatSimulation.TickDuration);
                    int releaseTick = (int)basics[0].GetType().GetField("ReleaseTick").GetValue(basics[0]);
                    Assert.AreEqual(preparedTick + AbilityTiming.Ticks(definition.AttackWindup), releaseTick,
                        "The queued basic itself has exactly one complete first-hit preparation.");
                }
            }
            Assert.GreaterOrEqual(landed, 0, "Hymay never completed its flight.");
            Assert.GreaterOrEqual(first, 0, "No new basic preparation followed landing.");
            Assert.GreaterOrEqual(release, 0, "The measured post-landing basic never impacted.");
            Assert.That(first - landed, Is.EqualTo(landingDuration).Within(.001f));
            float projectile = CombatSimulation.GetProjectileTravelTime(actor, target);
            Assert.That(release - first - projectile, Is.EqualTo(.4f).Within(.001f), "Only one first-hit preparation follows idle.");
        }

        UnitDefinition Blotun(float windup, bool projectile = false)
        {
            var definition = DocumentedRoster.CreateBlotan();
            definition.AttackWindup = windup; definition.AttackInterval = 5;
            definition.AttackHold = definition.AttackRecovery = 0; definition.Damage = 3;
            definition.AttackRange = projectile ? 9 : 1; definition.ProjectileSpeed = 5;
            Parameter(definition.BaseAbility, "interval", 20);
            Parameter(definition.BaseAbility, "animationDuration", 0);
            return definition;
        }

        [Test] public void ExtractionCancelsUnreleasedBasicAndResumesOnePreparationAfterIdle()
        {
            var actor = Unit(Blotun(1), 2, 5, BoardSide.Blue); var target = Enemy();
            Begin(actor, target, 4); var clock = Clock(actor); int income = 0;
            simulation.CurrencyEarned += (side, amount) => { if (side == BoardSide.Blue) income += amount; };
            simulation.Step(.1f); Assert.AreEqual(1, Attacks(clock));
            simulation.Step(.3f); Assert.AreEqual(1, income);
            Assert.That(AbilityTiming.Duration(actor.BaseStats, "blotan-income", 0), Is.EqualTo(.72f).Within(.001f));
            simulation.Step(.7f); Assert.AreEqual(1, Attacks(clock)); Assert.AreEqual(10000, target.CurrentHealth);
            simulation.Step(.1f); Assert.AreEqual(2, Attacks(clock)); Assert.AreEqual(1, income);
            simulation.Step(.9f); Assert.AreEqual(10000, target.CurrentHealth);
            simulation.Step(.1f); Assert.AreEqual(9997, target.CurrentHealth);
            Assert.AreEqual(1, income, "The extraction cooldown does not add payouts or alter their interval.");
        }

        [Test] public void ExtractionPreservesProjectileThatAlreadyLeftTheCharacter()
        {
            var actor = Unit(Blotun(.1f, true), 2, 5, BoardSide.Blue); var target = Enemy(2);
            Begin(actor, target, 4); var clock = Clock(actor);
            simulation.Step(.2f); Assert.AreEqual(1, Attacks(clock)); Assert.AreEqual(10000, target.CurrentHealth);
            simulation.Step(.2f); Assert.AreEqual(1, Attacks(clock));
            simulation.Step(.4f); Assert.AreEqual(9997, target.CurrentHealth, "A launched projectile still lands during extraction.");
            Assert.AreEqual(1, Attacks(clock), "Extraction cannot launch a new basic.");
        }

        [Test] public void EditedExtractionDurationControlsRecoveryWithoutChangingItsPayout()
        {
            var definition = Blotun(.2f); Parameter(definition.BaseAbility, "animationDuration", 1.23f);
            var actor = Unit(definition, 2, 5, BoardSide.Blue); var target = Enemy();
            Begin(actor, target, 1); var clock = Clock(actor); int income = 0;
            simulation.CurrencyEarned += (side, amount) => { if (side == BoardSide.Blue) income += amount; };
            simulation.Step(1.3f); Assert.AreEqual(0, Attacks(clock)); Assert.AreEqual(1, income);
            simulation.Step(.1f); Assert.AreEqual(1, Attacks(clock));
            simulation.Step(.1f); Assert.AreEqual(10000, target.CurrentHealth);
            simulation.Step(.1f); Assert.AreEqual(9997, target.CurrentHealth);
        }

        [Test] public void RecoveryAndReleasedHitsAreIndependentOfRenderedFramePartition()
        {
            string Run(float frame)
            {
                simulation?.Stop();
                var arena = new GameObject("replay-board"); arena.transform.SetParent(root.transform);
                board = arena.AddComponent<BoardManager>(); board.BuildBoard();
                var actor = Unit(Blotun(.1f, true), 2, 5, BoardSide.Blue); var target = Enemy(2);
                Begin(actor, target, 4); var events = new List<string>();
                int Tick() => Mathf.RoundToInt(simulation.Elapsed * 10);
                simulation.CurrencyEarned += (side, amount) => events.Add("income:" + Tick() + ":" + amount);
                simulation.Attacked += (source, victim, ranged) => { if (source == actor) events.Add("attack:" + Tick()); };
                simulation.Impacted += (source, victim, damage) => { if (source == actor) events.Add("impact:" + Tick() + ":" + damage); };
                int steps = Mathf.RoundToInt(2.4f / frame);
                for (int i = 0; i < steps; i++) simulation.Step(frame);
                return string.Join("|", events) + ":hp=" + target.CurrentHealth;
            }
            Assert.AreEqual(Run(.1f), Run(.04f));
        }
    }
}
