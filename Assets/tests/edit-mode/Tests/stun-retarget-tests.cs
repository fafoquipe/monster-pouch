using System;
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
    // Real scheduler/board regression fixture; requires the native EditMode runner.
    public sealed class StunRetargetNativeTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject root;
        BoardManager board;
        CombatSimulation simulation;
        BattleUnit actor, stenko, atrik;
        readonly List<BattleUnit> attacks = new List<BattleUnit>();

        [SetUp] public void Setup()
        {
            root = new GameObject("stun-retarget");
            board = root.AddComponent<BoardManager>();
            board.BuildBoard();
            attacks.Clear();
        }
        [TearDown] public void Cleanup()
        {
            simulation?.Stop();
            Object.DestroyImmediate(root);
        }
        UnitDefinition Definition(string id)
        {
            var d = DocumentedRoster.CreateAll().First(x => x.Id == id);
            // Keep identity/ruleset; isolate targeting from supers and opening passives.
            d.PassiveAbility = d.BaseAbility = d.MonsterUpgrade = null;
            d.Tricks = new TrickDefinition[3];
            d.MaxHealth = 1000; d.Damage = 7; d.AttackRange = 2;
            d.AttackInterval = 100; d.AttackWindup = .2f;
            d.AttackHold = d.AttackRecovery = 0; d.ProjectileSpeed = 100;
            d.MoveInterval = .2f; d.IQSpeed = 1;
            d.EnergyMax = 25; d.EnergyPerAttack = d.EnergyOnDamage = d.EnergyPerSecond = 0;
            return d;
        }
        BattleUnit Unit(UnitDefinition d, BoardSide side, int x, int y)
        {
            var go = new GameObject(d.Id);
            go.transform.SetParent(root.transform);
            var unit = go.AddComponent<MonsterUnit>();
            unit.Initialize(d.Id, side, UnitStats.FromDefinition(d));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y));
            return unit;
        }
        void Begin(string id, TargetPolicy policy = TargetPolicy.NearestReachable, float speed = 100, float windup = .2f)
        {
            var d = Definition(id); d.TargetPolicy = policy; d.ProjectileSpeed = speed; d.AttackWindup = windup;
            actor = Unit(d, BoardSide.Blue, 2, 6);
            stenko = Unit(new UnitDefinition { Id = "stein", MaxHealth = 1000, Damage = 0 }, BoardSide.Red, 2, 4);
            atrik = Unit(new UnitDefinition { Id = "atori", MaxHealth = 1000, Damage = 0 }, BoardSide.Red, 4, 0);
            simulation = new CombatSimulation(board, 1, 0);
            simulation.Begin(new[] { actor, stenko, atrik });
            stenko.IsPassiveTrainingTarget = atrik.IsPassiveTrainingTarget = true;
            if (policy == TargetPolicy.LowestHealth)
            {
                // Equal HP uses cell priority: distant Atrik (6) beats Stenko (1).
                // Make the intended opening victim uniquely weakest before the first decision.
                stenko.ApplyDamage(1);
                var opening = CombatTargetSelector.SelectTarget(board, actor, new[] { stenko, atrik });
                Assert.AreEqual(CombatTargetSelectionStatus.ReadyToAttack, opening.Status);
                Assert.AreSame(stenko, opening.Target);
            }
            simulation.Attacked += (a, target, projectile) => { if (a == actor) attacks.Add(target); };
            simulation.Step(.1f);
            Assert.AreSame(stenko, attacks.Single(), "The original lock must be acquired by normal AI.");
        }
        object Call(string name, params object[] supplied)
        {
            var method = typeof(CombatSimulation).GetMethod(name, Private);
            var args = method.GetParameters().Select(p => p.DefaultValue).ToArray();
            Array.Copy(supplied, args, supplied.Length);
            return method.Invoke(simulation, args);
        }
        object Clock(BattleUnit unit) => Call("Clock", unit);
        T Get<T>(BattleUnit unit, string name) => (T)Clock(unit).GetType().GetField(name).GetValue(Clock(unit));
        void Set(BattleUnit unit, string name, object value) => Clock(unit).GetType().GetField(name).SetValue(Clock(unit), value);
        void Stun(int ticks) => Call("Stun", Clock(actor), ticks);
        void Approach() => Assert.IsTrue(board.TryRepositionUnit(atrik, board.GetCell(2, 5)));
        bool Status(BattleUnit source, string kind, int ticks)
        {
            var type = typeof(CombatSimulation).GetMethod("EronyaApplyStatus", Private).GetParameters()[2].ParameterType;
            return (bool)Call("EronyaApplyStatus", Clock(source), Clock(actor), Enum.Parse(type, kind), "fixture-" + kind, 1f, ticks);
        }
        void ExpectRecovered(BattleUnit target)
        {
            Assert.IsFalse(actor.IsStunned);
            Assert.IsFalse(Get<bool>(actor, "StunRetargetPending"));
            Assert.AreSame(target, Get<BattleUnit>(actor, "LockedTarget"));
            Assert.AreSame(target, attacks.Last());
        }

        [TestCase("sepora")][TestCase("serya")]
        public void LastStunExpiryReacquiresNearestReachableOnTheNormalDecisionTick(string id)
        {
            Begin(id); Stun(5); Approach();
            simulation.Step(.4f);
            Assert.IsTrue(actor.IsStunned); Assert.AreEqual(1, attacks.Count);
            Assert.AreSame(stenko, Get<BattleUnit>(actor, "LockedTarget"));
            simulation.Step(.1f); ExpectRecovered(atrik); Assert.AreEqual(2, attacks.Count);
            // A consumed stun is not a permanent retarget-every-tick mode.
            Assert.IsTrue(board.TryRepositionUnit(atrik, board.GetCell(3, 5)));
            Assert.IsTrue(board.TryRepositionUnit(stenko, board.GetCell(2, 5)));
            simulation.Step(.5f);
            Assert.AreSame(atrik, Get<BattleUnit>(actor, "LockedTarget"));
        }

        [TestCase("sepora")][TestCase("serya")]
        public void RefreshWaitsForTheFinalDeadlineAndShorterStunsDoNotStack(string id)
        {
            Begin(id); Stun(5); Approach(); simulation.Step(.2f);
            Stun(10); Assert.AreEqual(13, Get<int>(actor, "StunUntil"));
            Stun(1); Assert.AreEqual(13, Get<int>(actor, "StunUntil"));
            simulation.Step(.9f);
            Assert.IsTrue(actor.IsStunned); Assert.AreEqual(1, attacks.Count);
            Assert.AreSame(stenko, Get<BattleUnit>(actor, "LockedTarget"));
            simulation.Step(.1f); ExpectRecovered(atrik);
        }

        [TestCase("sepora")][TestCase("serya")]
        public void ActiveTauntWinsAtRecoveryAndExpiredTauntCannotRestoreTheOldLock(string id)
        {
            Begin(id); Assert.IsTrue(Status(stenko, "Taunt", 12)); Stun(5); Approach();
            simulation.Step(.5f); ExpectRecovered(stenko); Assert.IsTrue(actor.IsTaunted);
            Assert.IsNull(Get<BattleUnit>(actor, "TargetBeforeTaunt"));
            simulation.Step(.7f);
            Assert.IsFalse(actor.IsTaunted);
            Assert.AreSame(atrik, Get<BattleUnit>(actor, "LockedTarget"));
        }

        [TestCase("sepora")][TestCase("serya")]
        public void ADeadOldTargetDoesNotPreventRecoverySelection(string id)
        {
            Begin(id); Stun(5); Approach(); stenko.ApplyDamage(1000);
            simulation.Step(.4f); Assert.IsTrue(actor.IsStunned); Assert.AreEqual(1, attacks.Count);
            // Death recognition retains its existing IQ/action deadlines.
            int nextDecision = Math.Max(6, Get<int>(actor, "NextAttack"));
            simulation.Step((nextDecision - 5) * .1f); ExpectRecovered(atrik);
        }

        [TestCase("bugui")][TestCase("serya")]
        public void RootsAloneKeepTheLockAndStillAllowThePreparedAttack(string id)
        {
            Begin(id); Assert.IsTrue(Status(atrik, "Root", 5)); Approach();
            Assert.IsFalse(Get<bool>(actor, "StunRetargetPending"));
            simulation.Step(.3f); Assert.IsTrue(actor.IsRooted); Assert.Less(stenko.CurrentHealth, 1000);
            simulation.Step(.2f);
            Assert.IsFalse(actor.IsRooted);
            Assert.AreSame(stenko, Get<BattleUnit>(actor, "LockedTarget"));
        }

        [TestCase("sepora")][TestCase("serya")]
        public void ARemainingRootDoesNotDelayStunRecoveryTargetSelection(string id)
        {
            Begin(id); Assert.IsTrue(Status(atrik, "Root", 20)); Stun(5); Approach();
            var cell = actor.CurrentCell;
            simulation.Step(.5f); ExpectRecovered(atrik);
            Assert.IsTrue(actor.IsRooted); Assert.AreSame(cell, actor.CurrentCell);
        }

        [TestCase("bugui")][TestCase("serya")]
        public void ReleasedProjectileKeepsItsBoundVictimAfterStunAndRetarget(string id)
        {
            Begin(id, speed: 2); simulation.Step(.2f);
            Assert.AreEqual(1000, stenko.CurrentHealth);
            Assert.AreEqual(1, simulation.PendingImpactCount);
            Stun(5); Approach(); simulation.Step(.5f); ExpectRecovered(atrik);
            actor.IsPassiveTrainingTarget = true;
            simulation.Step(.6f);
            Assert.AreEqual(993, stenko.CurrentHealth, "The released shot lands exactly once on Stenko.");
            Assert.AreEqual(1000, atrik.CurrentHealth, "The replacement shot has not arrived yet.");
        }

        [TestCase("bugui")][TestCase("serya")]
        public void StunCancelsTheOldPreparationAndRestartsAgainstTheReplacement(string id)
        {
            Begin(id, windup: 1); Assert.AreEqual(1, simulation.PendingImpactCount);
            Stun(5); Assert.AreEqual(0, simulation.PendingImpactCount); Approach();
            simulation.Step(.5f); ExpectRecovered(atrik);
            simulation.Step(1.1f);
            Assert.AreEqual(1000, stenko.CurrentHealth); Assert.AreEqual(993, atrik.CurrentHealth);
        }

        [TestCase("sepora")][TestCase("serya")]
        public void RecoveryUsesTheConfiguredPriorityInsteadOfAlwaysChoosingTheClosest(string id)
        {
            Begin(id, TargetPolicy.LowestHealth); Stun(5); Approach();
            stenko.ApplyDamage(200);
            var expected = CombatTargetSelector.SelectTarget(board, actor, new[] { stenko, atrik });
            Assert.AreSame(stenko, expected.Target);
            simulation.Step(.5f); ExpectRecovered(expected.Target);
        }

        [TestCase("sepora")][TestCase("serya")]
        public void RecoveryChoosesALegalPathWithoutAttackingOutOfRange(string id)
        {
            Begin(id); Stun(5);
            int priorHealth = stenko.CurrentHealth; // Selora's first ray is already committed.
            Assert.IsTrue(board.TryRepositionUnit(stenko, board.GetCell(0, 0)));
            var expected = CombatTargetSelector.SelectTarget(board, actor, new[] { stenko, atrik });
            Assert.AreEqual(CombatTargetSelectionStatus.MoveRequested, expected.Status);
            simulation.Step(.5f);
            Assert.IsFalse(actor.IsStunned); Assert.IsFalse(Get<bool>(actor, "StunRetargetPending"));
            Assert.AreSame(expected.Target, Get<BattleUnit>(actor, "LockedTarget"));
            Assert.AreEqual(1, attacks.Count, "Recovery must not manufacture an out-of-range hit.");
            Assert.AreEqual(priorHealth, stenko.CurrentHealth); Assert.AreEqual(1000, atrik.CurrentHealth);
        }

        [TestCase("sepora", false)][TestCase("serya", false)]
        [TestCase("sepora", true)][TestCase("serya", true)]
        public void UntargetableAndTunnellingNewcomersAreIgnored(string id, bool tunnel)
        {
            Begin(id); Stun(5);
            if (tunnel)
            {
                var method = typeof(CombatSimulation).GetMethods(Private).Single(m => m.Name == "EronyaReposition" && m.GetParameters().Length == 5);
                var kind = Enum.Parse(method.GetParameters()[3].ParameterType, "Tunnel");
                Assert.IsTrue((bool)method.Invoke(simulation, new[] { Clock(atrik), board.GetCell(2, 5), (object)true, kind, 20 }));
            }
            else { Approach(); Set(atrik, "StealthUntil", 30); }
            simulation.Step(.5f); ExpectRecovered(stenko);
        }

        [TestCase("sepora")][TestCase("serya")]
        public void EarlyCleanseConsumesThePendingInvalidationAtTheNextDecision(string id)
        {
            Begin(id); Stun(20); Approach(); simulation.Step(.2f);
            Call("ClearRoseControl", Clock(actor));
            // Existing cleanse owns its action deadlines; this change must not rewrite them.
            Set(actor, "NextAttack", 4); Set(actor, "NextMove", 4);
            simulation.Step(.1f); ExpectRecovered(atrik);
        }

        [TestCase("reset")][TestCase("death")][TestCase("stop")][TestCase("end")]
        public void LifecycleClearsThePendingTransition(string lifecycle)
        {
            Begin("serya"); Stun(20); Assert.IsTrue(Get<bool>(actor, "StunRetargetPending"));
            if (lifecycle == "reset") { actor.ResetForCombat(); simulation.Step(.1f); }
            if (lifecycle == "death") { actor.ApplyDamage(1000); simulation.Step(.1f); }
            if (lifecycle == "stop") simulation.Stop();
            if (lifecycle == "end") { stenko.ApplyDamage(1000); atrik.ApplyDamage(1000); simulation.Step(.1f); }
            Assert.IsFalse(Get<bool>(actor, "StunRetargetPending"));
        }
    }
}
