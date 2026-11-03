using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class MotionControlTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private BoardManager board;
        private CombatSimulation simulation;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("motion-control-board");
            objects.Add(go);
            board = go.AddComponent<BoardManager>();
            board.BuildBoard();
        }

        [TearDown]
        public void TearDown()
        {
            simulation?.Stop();
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AnuikRevivalImpactAddsThreeSecondStunOnlyWithItsTrick(bool impactTrick)
        {
            var definition = Stationary(DocumentedRoster.CreateAnuik());
            definition.ReviveDelay = .2f;
            var anuik = Unit(definition, 2, 5, BoardSide.Blue, impactTrick ? new[] { 1 } : new int[0]);
            var near = Target(2, 4);
            var outside = Target(5, 1);
            Begin(anuik, near, outside);
            anuik.ApplyDamage(10000);
            Until(() => anuik.IsReviving, "Anuik never started reviving.");
            Until(() => anuik.IsAlive, "Anuik never completed revival.");

            Assert.AreEqual(500, anuik.CurrentHealth);
            Assert.AreEqual(impactTrick ? 990 : 1000, near.CurrentHealth);
            Assert.AreEqual(new Vector2Int(2, impactTrick ? 3 : 4), near.CurrentCell.Coordinates);
            Assert.AreEqual(impactTrick, near.IsStunned);
            Assert.IsFalse(outside.IsStunned);
            Assert.AreEqual(1000, outside.CurrentHealth);
            simulation.Step(2.9f);
            Assert.AreEqual(impactTrick, near.IsStunned);
            simulation.Step(.1f);
            Assert.IsFalse(near.IsStunned, "The return stun must end exactly three seconds after revival.");
        }

        [Test]
        public void RevivalImpactPreservesDamageButRespectsControlImmunity()
        {
            var definition = Stationary(DocumentedRoster.CreateAnuik());
            definition.ReviveDelay = .2f;
            var anuik = Unit(definition, 2, 5, BoardSide.Blue, 1);
            var immune = Unit(Stationary(DocumentedRoster.CreateAtong()), 2, 4, BoardSide.Red, 2);
            Begin(anuik, immune);
            anuik.ApplyDamage(10000);
            Until(() => anuik.IsReviving, "Anuik never started reviving.");
            Until(() => anuik.IsAlive, "Anuik never completed revival.");

            Assert.AreEqual(990, immune.CurrentHealth);
            Assert.IsFalse(immune.IsStunned);
            Assert.AreEqual(new Vector2Int(2, 4), immune.CurrentCell.Coordinates);
        }

        [Test]
        public void HookStunsThroughoutPullAndForOneFullSecondAfterArrival()
        {
            var definition = Stationary(DocumentedRoster.CreateHymay());
            definition.BaseAbility.Parameters = new[] { Parameter("pullDuration", .6f) };
            var hymay = Unit(definition, 2, 7);
            var target = Target(2, 4);
            Begin(hymay, target);
            Charge(hymay);
            Until(() => target.IsBeingPulled, "The hook never began pulling its victim.");
            Assert.AreEqual(.6f, target.PullDuration, .001f);
            Assert.IsTrue(target.IsStunned);
            Assert.AreEqual(new Vector2Int(2, 6), target.CurrentCell.Coordinates);
            simulation.Step(.5f);
            Assert.IsTrue(target.IsBeingPulled);
            Assert.IsTrue(target.IsStunned);
            simulation.Step(.1f);
            Assert.IsFalse(target.IsBeingPulled);
            Assert.IsTrue(target.IsStunned, "Arrival must begin the additional one-second stun.");
            simulation.Step(.9f);
            Assert.IsTrue(target.IsStunned);
            simulation.Step(.1f);
            Assert.IsFalse(target.IsStunned);
        }

        [TestCase(.3f, .3f)]
        [TestCase(.85f, .9f)]
        public void BackstepWaitsForArrivalThenUsesEditedFlightDurationAndBlocksDamage(float edited, float effective)
        {
            var definition = Stationary(DocumentedRoster.CreateHymay());
            definition.BaseAbility.Parameters = new[] { Parameter("pullDuration", .4f) };
            definition.Tricks[2].Parameters = new[] { Parameter("distance", 2), Parameter("travelDuration", edited) };
            var hymay = Unit(definition, 2, 5, BoardSide.Blue, 2);
            var target = Target(2, 2);
            Begin(hymay, target);
            Charge(hymay);
            Until(() => target.IsBeingPulled, "The hook never began pulling its victim.");
            Assert.IsFalse(hymay.IsFlying);
            Assert.AreEqual(new Vector2Int(2, 5), hymay.CurrentCell.Coordinates);
            simulation.Step(.3f);
            Assert.IsFalse(hymay.IsFlying, "Backstep cannot start before the victim arrives.");
            simulation.Step(.1f);
            Assert.IsFalse(target.IsBeingPulled);
            Assert.IsTrue(hymay.IsFlying);
            Assert.AreEqual(effective, hymay.FlightDuration, .001f);
            Assert.AreEqual(new Vector2Int(2, 7), hymay.CurrentCell.Coordinates);
            Assert.AreSame(hymay, board.GetCell(2, 7).OccupiedBy);
            Assert.IsNull(board.GetCell(2, 5).OccupiedBy);
            Assert.AreEqual(0, hymay.ApplyDamage(17));
            simulation.Step(effective - .1f);
            Assert.IsTrue(hymay.IsFlying);
            Assert.AreEqual(0, hymay.ApplyDamage(17));
            simulation.Step(.1f);
            Assert.IsFalse(hymay.IsFlying);
            Assert.AreEqual(17, hymay.ApplyDamage(17), "Invulnerability must end on landing.");
            Assert.AreEqual(definition.MaxHealth, hymay.BaseStats.MaxHealth);
            Assert.AreEqual(definition.Damage, hymay.BaseStats.Attack);
            Assert.AreEqual(definition.AttackInterval, hymay.BaseStats.AttackInterval, .001f);
            Assert.AreEqual(definition.MoveInterval, hymay.BaseStats.MoveInterval, .001f);
        }

        [TestCase("source-reset")]
        [TestCase("target-reset")]
        [TestCase("source-death")]
        [TestCase("stop")]
        public void InterruptedHookCannotLaunchADelayedBackstep(string interruption)
        {
            var definition = Stationary(DocumentedRoster.CreateHymay());
            definition.BaseAbility.Parameters = new[] { Parameter("pullDuration", .8f) };
            var hymay = Unit(definition, 2, 7, BoardSide.Blue, 2);
            var ally = Target(5, 8, BoardSide.Blue);
            var target = Target(2, 4);
            Begin(hymay, ally, target);
            Charge(hymay);
            Until(() => target.IsBeingPulled, "The hook never began pulling its victim.");
            int retreats = 0;
            simulation.Moved += (unit, _, __) => { if (unit == hymay) retreats++; };
            switch (interruption)
            {
                case "source-reset": hymay.ResetForCombat(); break;
                case "target-reset": target.ResetForCombat(); break;
                case "source-death": hymay.ApplyDamage(10000); break;
                case "stop": simulation.Stop(); break;
            }
            simulation.Step(1.2f);
            Assert.AreEqual(0, retreats);
            Assert.IsFalse(hymay.IsFlying);
            Assert.IsFalse(target.IsBeingPulled);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResetOrStopClearsBackstepInvulnerability(bool stop)
        {
            var hymay = Unit(Stationary(DocumentedRoster.CreateHymay()), 2, 5, BoardSide.Blue, 2);
            var target = Target(2, 4);
            Begin(hymay, target);
            Charge(hymay);
            Until(() => hymay.IsFlying, "An adjacent victim must still allow the backstep.");
            Assert.IsFalse(target.IsBeingPulled, "An already adjacent victim has no artificial travel time.");
            if (stop) simulation.Stop(); else hymay.ResetForCombat();
            Assert.IsFalse(hymay.IsFlying);
            Assert.AreEqual(17, hymay.ApplyDamage(17));
        }

        [Test]
        public void HookCannotPullOrStunAControlImmuneTarget()
        {
            var hymay = Unit(Stationary(DocumentedRoster.CreateHymay()), 2, 7);
            var target = Unit(Stationary(DocumentedRoster.CreateAtong()), 2, 4, BoardSide.Red, 2);
            Begin(hymay, target);
            Charge(hymay);
            simulation.Step(1.5f);
            Assert.IsFalse(target.IsBeingPulled);
            Assert.IsFalse(target.IsStunned);
            Assert.AreEqual(new Vector2Int(2, 4), target.CurrentCell.Coordinates);
        }

        [Test]
        public void DashStunsEachEnemyOnContactAndMeasuresExpiryFromThatContact()
        {
            var popow = Popow();
            var near = Target(2, 6);
            var far = Target(2, 2);
            Begin(popow, near, far);
            Charge(popow);
            simulation.Step(.1f);
            Assert.IsFalse(near.IsStunned);
            Assert.IsFalse(far.IsStunned, "The far enemy cannot be stunned when the dash launches.");
            Until(() => near.IsStunned, "The travelling aura missed the near enemy.");
            float nearContact = simulation.Elapsed;
            Assert.IsFalse(far.IsStunned);
            Until(() => far.IsStunned, "The travelling aura missed the far enemy.");
            float farContact = simulation.Elapsed;
            Assert.Greater(farContact, nearContact);
            UntilTime(nearContact + .9f);
            Assert.IsTrue(near.IsStunned);
            simulation.Step(.1f);
            Assert.IsFalse(near.IsStunned, "A victim must be hit only once by the travelling aura.");
            Assert.IsTrue(far.IsStunned);
            UntilTime(farContact + .9f);
            Assert.IsTrue(far.IsStunned);
            simulation.Step(.1f);
            Assert.IsFalse(far.IsStunned);
        }

        [Test]
        public void DashChecksCurrentEnemyPositionsWhenTheAuraArrives()
        {
            var popow = Popow();
            var far = Target(2, 2);
            var leaver = Target(2, 4);
            var entrant = Target(3, 4);
            Begin(popow, far, leaver, entrant);
            Charge(popow);
            simulation.Step(.1f);
            Assert.IsTrue(board.TryRepositionUnit(leaver, board.GetCell(4, 4)));
            Assert.IsTrue(board.TryRepositionUnit(entrant, board.GetCell(2, 4)));
            simulation.Step(1f);
            Assert.IsFalse(leaver.IsStunned, "Leaving the route before contact must avoid the stun.");
            Assert.IsTrue(entrant.IsStunned, "Entering the route before contact must receive the stun.");
            Assert.IsTrue(far.IsStunned);
        }

        [TestCase("reset")]
        [TestCase("death")]
        [TestCase("stop")]
        public void CancelledDashCannotStunAnEnemyFartherAlongTheRoute(string interruption)
        {
            var popow = Popow();
            var ally = Target(5, 8, BoardSide.Blue);
            var near = Target(2, 6);
            var far = Target(2, 2);
            Begin(popow, ally, near, far);
            Charge(popow);
            Until(() => near.IsStunned, "The travelling aura missed the near enemy.");
            Assert.IsFalse(far.IsStunned);
            if (interruption == "reset") popow.ResetForCombat();
            else if (interruption == "death") popow.ApplyDamage(10000);
            else simulation.Stop();
            simulation.Step(1f);
            Assert.IsFalse(far.IsStunned, "An interrupted dash must discard its untravelled contacts.");
        }

        [Test]
        public void StunThatResetsDashAnimationAlsoCancelsUntravelledContacts()
        {
            var popow = Popow();
            var far = Target(2, 2);
            var definition = Stationary(DocumentedRoster.CreateStein());
            definition.ProjectileSpeed = 100;
            definition.BaseAbility.Parameters=new[]{Parameter("castDuration",.2f)};
            var stein = Unit(definition, 3, 6, BoardSide.Red);
            Begin(popow, far, stein);
            Charge(stein);
            Charge(popow);
            simulation.Step(.1f);
            int attackVersion = popow.AttackResetVersion;
            Assert.IsFalse(far.IsStunned);
            Until(() => popow.IsStunned, "Stein must interrupt the dash before the aura reaches its far target.");
            Assert.Greater(popow.AttackResetVersion, attackVersion);
            Assert.IsFalse(far.IsStunned);
            simulation.Step(1f);
            Assert.IsFalse(far.IsStunned, "A stopped dash animation cannot leave an invisible damaging aura travelling onward.");
        }

        private BattleUnit Popow()
        {
            var definition = Stationary(DocumentedRoster.CreatePopow());
            definition.BaseAbility.Parameters = new[] { Parameter("travelDuration", 1f) };
            return Unit(definition, 2, 8);
        }

        private static AbilityParameter Parameter(string key, float value) => new AbilityParameter { Key = key, Value = value };

        private static UnitDefinition Stationary(UnitDefinition definition)
        {
            definition.MaxHealth = 1000;
            definition.Damage = 0;
            definition.AttackRange = 9;
            definition.AttackInterval = 100;
            definition.AttackWindup = .1f;
            definition.MoveInterval = 100;
            definition.EnergyPerAttack = definition.EnergyOnDamage = definition.EnergyPerSecond = 0;
            return definition;
        }

        private BattleUnit Target(int x, int y, BoardSide side = BoardSide.Red) =>
            Unit(Stationary(new UnitDefinition { Id = "target" }), x, y, side);

        private BattleUnit Unit(UnitDefinition definition, int x, int y, BoardSide side = BoardSide.Blue, params int[] tricks)
        {
            var owned = new OwnedUnit(definition);
            foreach (int index in tricks) owned.Tricks[index] = true;
            var go = new GameObject(definition.Id + "-" + objects.Count);
            objects.Add(go);
            BattleUnit unit = definition.IsMonster ? (BattleUnit)go.AddComponent<MonsterUnit>() : go.AddComponent<WhelpUnit>();
            unit.Initialize(go.name, side, UnitStats.FromDefinition(definition, owned));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y));
            return unit;
        }

        private void Begin(params BattleUnit[] units)
        {
            simulation = new CombatSimulation(board);
            simulation.Begin(units);
        }

        private static void Charge(BattleUnit unit) => unit.AddEnergy(unit.BaseStats.EnergyMax);

        private void Until(Func<bool> condition, string failure)
        {
            for (int i = 0; i < 30 && !condition(); i++) simulation.Step(.1f);
            Assert.IsTrue(condition(), failure);
        }

        private void UntilTime(float time)
        {
            while (simulation.Elapsed < time - .001f && !simulation.Finished) simulation.Step(.1f);
            Assert.AreEqual(time, simulation.Elapsed, .001f);
        }
    }
}
