using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Status;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class RosieTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        BoardManager board;
        CombatSimulation simulation;
        [SetUp] public void SetUp() { var go = new GameObject("rosie-test-board"); objects.Add(go); board = go.AddComponent<BoardManager>(); board.BuildBoard(); }
        [TearDown] public void TearDown() { simulation?.Stop(); foreach (var go in objects) if (go != null) Object.DestroyImmediate(go); objects.Clear(); }
        BattleUnit Unit(UnitDefinition definition, int x, int y, BoardSide side = BoardSide.Blue, params int[] upgrades)
        {
            var go = new GameObject(definition.Id + objects.Count); objects.Add(go);
            var owned = new OwnedUnit(definition); foreach (int upgrade in upgrades) owned.Tricks[upgrade] = true;
            var unit = go.AddComponent<WhelpUnit>(); unit.Initialize(go.name, side, UnitStats.FromDefinition(definition, owned));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y)); return unit;
        }
        BattleUnit Ally(int x, int y, BoardSide side = BoardSide.Blue) => Unit(new UnitDefinition
        { Id = "ally" + objects.Count, MaxHealth = 100, Damage = 0, AttackRange = 99, AttackInterval = 100, AttackWindup = .1f, MoveInterval = 100 }, x, y, side);
        void Begin(params BattleUnit[] units) { simulation = new CombatSimulation(board); simulation.Begin(units); }
        void Charge(BattleUnit unit) => unit.AddEnergy(unit.BaseStats.EnergyMax);
        static void Set(TrickDefinition effect, params AbilityParameter[] parameters) => effect.Parameters = parameters;
        static AbilityParameter P(string key, float value) => new AbilityParameter { Key = key, Value = value };
        List<ActiveStatus> Statuses(BattleUnit unit) { var states = new List<ActiveStatus>(); simulation.CollectStatuses(unit, states); return states; }

        [TestCase(BoardSide.Blue, 9, true)]
        [TestCase(BoardSide.Blue, 8, false)]
        [TestCase(BoardSide.Red, 0, true)]
        [TestCase(BoardSide.Red, 1, false)]
        public void LastRowTargetsNearest_OtherRowsTargetFarthest_OnBothSides(BoardSide side, int row, bool nearest)
        {
            int forward = side == BoardSide.Blue ? -1 : 1;
            var rosie = Unit(DocumentedRoster.CreateRosie(), 2, row, side);
            var near = Ally(2, row + forward, side); var far = Ally(2, row + forward * 4, side);
            var enemy = Ally(5, row + forward * 3, side == BoardSide.Blue ? BoardSide.Red : BoardSide.Blue);
            Begin(rosie, near, far, enemy); CombatSimulation.HealingRoseFlight flight = null;
            simulation.HealingRoseLaunched += f => flight = f; Charge(rosie); simulation.Step(.4f);
            Assert.NotNull(flight); Assert.AreSame((nearest ? near : far).CurrentCell, flight.Destination);
            Assert.AreEqual(3, flight.Width); Assert.AreNotSame(rosie.CurrentCell, flight.Destination);
        }

        [Test] public void RoseHealsInTravelOrder_WithinThreeTileLane_Once_AndNeverEnemies()
        {
            var rosie = Unit(DocumentedRoster.CreateRosie(), 2, 8);
            var first = Ally(2, 6); var left = Ally(1, 5); var right = Ally(3, 4); var outside = Ally(4, 5); var end = Ally(2, 2);
            var enemy = Ally(2, 5, BoardSide.Red);
            Begin(rosie, first, left, right, outside, end, enemy);
            foreach (var unit in new[] { first, left, right, outside, end, enemy }) unit.ApplyDamage(70);
            rosie.ApplyDamage(20); Charge(rosie); simulation.Step(.4f);
            Assert.AreEqual(11, rosie.CurrentHealth, "Self heals on release."); Assert.AreEqual(30, first.CurrentHealth); Assert.AreEqual(30, end.CurrentHealth);
            simulation.Step(.1f); Assert.AreEqual(30, first.CurrentHealth, "Still outside the aura.");
            simulation.Step(.2f); Assert.AreEqual(55, first.CurrentHealth); Assert.AreEqual(30, end.CurrentHealth);
            simulation.Step(1.4f);
            foreach (var unit in new[] { first, left, right, end }) Assert.AreEqual(55, unit.CurrentHealth, unit.UnitId);
            Assert.AreEqual(30, outside.CurrentHealth); Assert.LessOrEqual(enemy.CurrentHealth, 30, "Enemies can receive basic damage, never healing.");
            simulation.Step(.5f); Assert.AreEqual(55, first.CurrentHealth, "A travelling rose cannot heal twice.");
        }

        [Test] public void AllUpgradesAffectSelfAndFullHealthAllies_AndHasteExpires()
        {
            var rosie = Unit(DocumentedRoster.CreateRosie(), 2, 9, BoardSide.Blue, 0, 1, 2);
            var ally = Ally(2, 8); var enemy = Ally(5, 4, BoardSide.Red);
            Begin(rosie, ally, enemy); rosie.ApplyDamage(20); Charge(rosie); simulation.Step(.8f);
            Assert.AreEqual(5 + Mathf.RoundToInt(25 * .25f), rosie.CurrentHealth);
            Assert.AreEqual(Mathf.RoundToInt(25 * .1f), rosie.Shield); Assert.AreEqual(10, ally.Shield);
            Assert.IsTrue(Statuses(rosie).Any(s => s.Id == "rose-haste")); Assert.IsTrue(Statuses(ally).Any(s => s.Id == "rose-haste"));
            Assert.IsTrue(Statuses(rosie).Any(s => s.Id == "control-immunity")); Assert.IsTrue(Statuses(ally).Any(s => s.Id == "control-immunity"));
            simulation.Step(2.5f); Assert.IsTrue(Statuses(rosie).Any(s => s.Id == "rose-haste"));
            simulation.Step(.1f); Assert.IsFalse(Statuses(rosie).Any(s => s.Id == "rose-haste"));
            Assert.IsFalse(Statuses(rosie).Any(s => s.Id == "control-immunity"));
            simulation.Step(.4f); Assert.IsFalse(Statuses(ally).Any(s => s.Id == "rose-haste"));
            Assert.IsFalse(Statuses(ally).Any(s => s.Id == "control-immunity"));
            Assert.AreEqual(10, ally.Shield, "Shield persists until consumed or combat resets.");
        }

        [Test] public void EditedWidthSpeedPercentAndShieldAreUsedByCombat()
        {
            var definition = DocumentedRoster.CreateRosie();
            Set(definition.BaseAbility, P("healPercent", 40), P("width", 1), P("projectileSpeed", 6));
            Set(definition.Tricks[1], P("shieldHealthPercent", 20));
            var rosie = Unit(definition, 2, 8, BoardSide.Blue, 1); var center = Ally(2, 5); var side = Ally(3, 6); var enemy = Ally(5, 4, BoardSide.Red);
            Begin(rosie, center, side, enemy); center.ApplyDamage(70); side.ApplyDamage(70);
            CombatSimulation.HealingRoseFlight flight = null; simulation.HealingRoseLaunched += f => flight = f;
            Charge(rosie); simulation.Step(.9f);
            Assert.AreEqual(1.5f, flight.Duration, .001f); Assert.AreEqual(1, flight.Width);
            Assert.AreEqual(-1, flight.End.y, .001f);
            Assert.AreEqual(70, center.CurrentHealth); Assert.AreEqual(20, center.Shield);
            Assert.AreEqual(30, side.CurrentHealth); Assert.AreEqual(0, side.Shield);
        }

        [Test] public void DiagonalLaneUsesStraightPerpendicularWidth()
        {
            var rosie = Unit(DocumentedRoster.CreateRosie(), 0, 8); var diagonal = Ally(2, 6); var end = Ally(4, 4); var outside = Ally(4, 7); var enemy = Ally(5, 3, BoardSide.Red);
            Begin(rosie, diagonal, end, outside, enemy); diagonal.ApplyDamage(70); end.ApplyDamage(70); outside.ApplyDamage(70);
            Charge(rosie); simulation.Step(2.5f);
            Assert.AreEqual(55, diagonal.CurrentHealth); Assert.AreEqual(55, end.CurrentHealth); Assert.AreEqual(30, outside.CurrentHealth);
        }

        [Test] public void NoAlliesStillHealsSelf_AndStopCancelsFlight()
        {
            var rosie = Unit(DocumentedRoster.CreateRosie(), 2, 9, BoardSide.Blue, 0, 1, 2); var enemy = Ally(2, 4, BoardSide.Red);
            Begin(rosie, enemy); rosie.ApplyDamage(20); CombatSimulation.HealingRoseFlight flight = null;
            simulation.HealingRoseLaunched += f => flight = f; Charge(rosie); simulation.Step(.4f);
            Assert.NotNull(flight); Assert.AreEqual(11, rosie.CurrentHealth); Assert.Greater(rosie.Shield, 0);
            simulation.Stop(); Assert.IsFalse(flight.Active); Assert.IsFalse(Statuses(rosie).Any(s => s.Id == "rose-haste"));
        }

        [TestCase(BoardSide.Blue, 9)]
        [TestCase(BoardSide.Blue, 8)]
        [TestCase(BoardSide.Red, 0)]
        [TestCase(BoardSide.Red, 1)]
        public void RoseContinuesBeyondChosenAlly_HealsAtFarEdgeOnce_ThenLeavesBoard(BoardSide side, int row)
        {
            int forward = side == BoardSide.Blue ? -1 : 1;
            var definition = DocumentedRoster.CreateRosie(); definition.AttackInterval = 100;
            var rosie = Unit(definition, 2, row, side);
            var near = Ally(2, row + forward, side);
            var far = Ally(2, row + 4 * forward, side);
            var entering = Ally(4, row + 2 * forward, side);
            var enemy = Ally(5, row + 3 * forward, side == BoardSide.Blue ? BoardSide.Red : BoardSide.Blue);
            Begin(rosie, near, far, entering, enemy); entering.ApplyDamage(70);
            CombatSimulation.HealingRoseFlight flight = null;
            simulation.HealingRoseLaunched += f => flight = f;
            Charge(rosie); simulation.Step(.4f);
            bool back = row == (side == BoardSide.Blue ? 9 : 0);
            Assert.AreSame((back ? near : far).CurrentCell, flight.Destination);
            Vector2 end = flight.End;
            Assert.AreEqual(2, end.x, .001f);
            Assert.AreEqual(side == BoardSide.Blue ? -2 : 11, end.y, .001f);
            Assert.IsTrue(board.TryRepositionUnit(entering, board.GetCell(2, side == BoardSide.Blue ? 0 : 9)));
            simulation.Step(1.5f);
            Assert.IsTrue(flight.Active, "Crossing the selected ally must not end the flight.");
            Assert.AreEqual(30, entering.CurrentHealth);
            simulation.Step(2.5f);
            Assert.AreEqual(55, entering.CurrentHealth, "An ally beyond the aim point still receives healing once.");
            Assert.AreEqual(end, flight.End); Assert.IsFalse(flight.Active);
        }

        [Test] public void TenBasicAttacksChargeTwentyEnergy_AndSuperDoesNotGenerateEnergy()
        {
            var definition = DocumentedRoster.CreateRosie(); definition.AttackInterval = .3f; definition.AttackWindup = .1f; definition.AttackHold = definition.AttackRecovery = 0;
            var rosie = Unit(definition, 2, 9); var enemy = Ally(2, 4, BoardSide.Red); Begin(rosie, enemy);
            int attacks = 0; bool cast = false; simulation.Attacked += (a, t, p) => { if (a == rosie) attacks++; };
            simulation.HealingRoseLaunched += f => cast = true;
            simulation.Step(.1f); Assert.AreEqual(2, rosie.Energy);
            for (int i = 0; i < 100 && !cast; i++) simulation.Step(.1f);
            Assert.IsTrue(cast); Assert.AreEqual(10, attacks); Assert.AreEqual(0, rosie.Energy);
        }

        [Test] public void DescriptionUsesPercentagesAndEditedImmunityDuration_WithoutIncreasingHealing()
        {
            var definition = DocumentedRoster.CreateRosie();
            Assert.AreEqual("Elimina y bloquea stun, ralentización y demás controles durante 3 s, también en Rosie.", AbilityBalance.Description(definition.Tricks[2], definition));
            Assert.That(AbilityBalance.Description(definition.BaseAbility, definition), Does.Contain("25%"));
            Set(definition.Tricks[2], P("duration", 1));
            Assert.That(AbilityBalance.Description(definition.Tricks[2], definition), Does.Contain("durante 1 s"));
            var rosie = Unit(definition, 2, 9, BoardSide.Blue, 2); var ally = Ally(2, 8); var enemy = Ally(5, 4, BoardSide.Red);
            Begin(rosie, ally, enemy); ally.ApplyDamage(70); Charge(rosie); simulation.Step(.8f); Assert.AreEqual(55, ally.CurrentHealth);
            Assert.IsTrue(Statuses(ally).Any(s => s.Id == "control-immunity"));
            simulation.Step(1); Assert.IsFalse(Statuses(ally).Any(s => s.Id == "control-immunity"));
        }

        object Actor(BattleUnit unit)
        {
            var clocks = (System.Collections.IEnumerable)typeof(CombatSimulation).GetField("actors", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(simulation);
            foreach(var actor in clocks) if((BattleUnit)actor.GetType().GetField("Unit").GetValue(actor) == unit) return actor;
            return null;
        }
        void InvokeCombat(string method, params object[] args) =>
            typeof(CombatSimulation).GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(simulation, args);

        void GumHit(BattleUnit source, BattleUnit target)
        {
            var type = typeof(CombatSimulation).GetNestedType("PendingImpact", System.Reflection.BindingFlags.NonPublic);
            var impact = System.Activator.CreateInstance(type, true);
            type.GetField("Source").SetValue(impact, Actor(source));
            type.GetField("Effect").SetValue(impact, "tsu-gum");
            InvokeCombat("ApplyHitEffects", impact, Actor(target));
        }
        void Trap(BattleUnit source, BattleUnit target)
        {
            var type = typeof(CombatSimulation).GetNestedType("TileEffect", System.Reflection.BindingFlags.NonPublic);
            var trap = System.Activator.CreateInstance(type, true);
            type.GetField("Source").SetValue(trap, Actor(source));
            type.GetField("Cell").SetValue(trap, target.CurrentCell);
            type.GetField("Kind").SetValue(trap, "trap");
            var tiles = (System.Collections.IList)typeof(CombatSimulation).GetField("tiles", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(simulation);
            tiles.Add(trap); InvokeCombat("ResolveTileEffects");
        }

        [TestCase(false)] [TestCase(true)]
        public void RoseRemovesExistingStunGumSlowAndTauntOnlyWithThirdUpgrade(bool upgraded)
        {
            var rosie = Unit(DocumentedRoster.CreateRosie(), 2, 9, BoardSide.Blue, upgraded ? new[]{2} : new int[0]);
            var ally = Ally(2, 7);
            var tsu = Unit(DocumentedRoster.CreateTsu(), 2, 4, BoardSide.Red, 1);
            var jazar = Unit(DocumentedRoster.CreateJazar(), 5, 4, BoardSide.Red);
            var bugaloo = Unit(DocumentedRoster.CreateBugaloo(), 3, 7, BoardSide.Red);
            Begin(rosie, ally, tsu, jazar, bugaloo);
            InvokeCombat("Stun", Actor(ally), 50, "electric");
            GumHit(tsu, ally); InvokeCombat("Radiation", Actor(jazar), Actor(ally));
            InvokeCombat("PulseTaunt", Actor(bugaloo)); InvokeCombat("TickStatuses");
            Assert.IsTrue(ally.IsStunned && ally.IsRooted && ally.IsSlowed && ally.IsTaunted);
            Assert.IsTrue(simulation.GumRootedUnits.Contains(ally));
            InvokeCombat("ApplyHealingRose", Actor(rosie), Actor(ally));
            Assert.AreEqual(!upgraded, ally.IsStunned); Assert.AreEqual(!upgraded, ally.IsElectrocuted);
            Assert.AreEqual(!upgraded, ally.IsRooted); Assert.AreEqual(!upgraded, ally.IsSlowed);
            Assert.AreEqual(!upgraded, ally.IsTaunted); Assert.AreEqual(!upgraded, simulation.GumRootedUnits.Contains(ally));
            if(upgraded)
            {
                Assert.IsFalse(Statuses(ally).Any(s => new[]{"stun","electric-stun","root","slow","taunt","chocolate"}.Contains(s.Id)));
                Assert.AreEqual(0, Actor(ally).GetType().GetField("NextMove").GetValue(Actor(ally)));
            }
        }

        [Test] public void RoseStopsIncomingHookAndBreaksAlreadyCapturedGumTrap()
        {
            var rosie = Unit(DocumentedRoster.CreateRosie(), 2, 9, BoardSide.Blue, 2);
            var ally = Ally(2, 7); var trapped = Ally(3, 7);
            var hymay = Unit(DocumentedRoster.CreateHymay(), 2, 3, BoardSide.Red);
            var tsu = Unit(DocumentedRoster.CreateTsu(), 5, 4, BoardSide.Red);
            Begin(rosie, ally, trapped, hymay, tsu);
            InvokeCombat("Pull", Actor(hymay), Actor(ally)); Trap(tsu, trapped);
            Assert.IsTrue(ally.IsBeingPulled && ally.IsStunned); Assert.IsTrue(trapped.IsRooted);
            InvokeCombat("ApplyHealingRose", Actor(rosie), Actor(ally));
            InvokeCombat("ApplyHealingRose", Actor(rosie), Actor(trapped));
            Assert.IsFalse(ally.IsBeingPulled || ally.IsStunned || trapped.IsRooted);
            Assert.IsFalse(simulation.GroundEffects.Any(e => e.Kind == "trap"));
            InvokeCombat("ResolveControlMotions"); InvokeCombat("ResolveTileEffects"); InvokeCombat("TickStatuses");
            Assert.IsFalse(ally.IsBeingPulled || ally.IsStunned || trapped.IsRooted);
        }

        [Test] public void RoseProtectsSelfAndAllyFromEveryIncomingControlIncludingGumAndForcedMovement()
        {
            var rosie = Unit(DocumentedRoster.CreateRosie(), 2, 9, BoardSide.Blue, 2);
            var ally = Ally(2, 7);
            var tsu = Unit(DocumentedRoster.CreateTsu(), 5, 4, BoardSide.Red);
            var jazar = Unit(DocumentedRoster.CreateJazar(), 4, 4, BoardSide.Red);
            var hymay = Unit(DocumentedRoster.CreateHymay(), 2, 3, BoardSide.Red);
            var bugaloo = Unit(DocumentedRoster.CreateBugaloo(), 3, 8, BoardSide.Red);
            Begin(rosie, ally, tsu, jazar, hymay, bugaloo);
            foreach(var target in new[]{rosie, ally})
            {
                InvokeCombat("ApplyHealingRose", Actor(rosie), Actor(target));
                var cell = target.CurrentCell;
                foreach(string visual in new[]{"impact","electric","popow"}) InvokeCombat("Stun", Actor(target), 20, visual);
                GumHit(tsu, target); InvokeCombat("Radiation", Actor(jazar), Actor(target));
                InvokeCombat("Pull", Actor(hymay), Actor(target));
                InvokeCombat("Reposition", Actor(target), board.GetCell(cell.X + 1, cell.Y), true);
                Trap(tsu, target);
                Assert.AreSame(cell, target.CurrentCell);
                Assert.IsFalse(target.IsStunned || target.IsRooted || target.IsSlowed || target.IsBeingPulled);
                Assert.IsFalse(simulation.GumRootedUnits.Contains(target));
                int health = target.CurrentHealth;
                InvokeCombat("DirectDamage", Actor(tsu), Actor(target), 2);
                Assert.AreEqual(health - 2, target.CurrentHealth);
            }
            InvokeCombat("PulseTaunt", Actor(bugaloo));
            Assert.IsFalse(rosie.IsTaunted || ally.IsTaunted);
        }

        [Test] public void ImmunityBlocksControlForThreeSeconds_ButStillAllowsDamage_AndExpiresPerRecipient()
        {
            var rosie = Unit(DocumentedRoster.CreateRosie(), 2, 9, BoardSide.Blue, 2);
            var ally = Ally(2, 7); var enemy = Ally(5, 4, BoardSide.Red);
            Begin(rosie, ally, enemy); Charge(rosie); simulation.Step(.8f);
            foreach(var unit in new[] { rosie, ally })
            {
                InvokeCombat("Stun", Actor(unit), 10, "electric");
                Assert.IsFalse(unit.IsStunned);
                int health = unit.CurrentHealth;
                InvokeCombat("DirectDamage", Actor(enemy), Actor(unit), 2);
                Assert.AreEqual(health - 2, unit.CurrentHealth, "Control immunity does not prevent damage.");
            }
            simulation.Step(2.6f);
            InvokeCombat("Stun", Actor(rosie), 10, "electric"); Assert.IsTrue(rosie.IsStunned);
            InvokeCombat("Stun", Actor(ally), 10, "electric"); Assert.IsFalse(ally.IsStunned, "Ally was reached later.");
            simulation.Step(.6f);
            InvokeCombat("Stun", Actor(ally), 10, "electric"); Assert.IsTrue(ally.IsStunned);
        }

        [Test] public void BaseRoseDoesNotGrantControlImmunity()
        {
            var rosie = Unit(DocumentedRoster.CreateRosie(), 2, 9); var ally = Ally(2, 8); var enemy = Ally(5, 4, BoardSide.Red);
            Begin(rosie, ally, enemy); Charge(rosie); simulation.Step(.8f);
            Assert.IsFalse(Statuses(ally).Any(s => s.Id == "control-immunity"));
            InvokeCombat("Stun", Actor(ally), 10, "impact"); Assert.IsTrue(ally.IsStunned);
        }

        [Test] public void HasteChangesAttackCadenceWithoutChangingMovementBonus()
        {
            var definition = DocumentedRoster.CreateRosie(); Set(definition.Tricks[0], P("speedPercent", 30), P("duration", 3));
            var rosie = Unit(definition, 2, 9, BoardSide.Blue, 0);
            var allyDefinition = new UnitDefinition { Id = "cadence", MaxHealth = 100, Damage = 0, AttackRange = 99, AttackInterval = 1, AttackWindup = .1f, AttackHold = 0, AttackRecovery = 0, MoveInterval = .6f };
            var ally = Unit(allyDefinition, 2, 8); var enemy = Ally(5, 4, BoardSide.Red); Begin(rosie, ally, enemy);
            var times = new List<float>(); simulation.Attacked += (a, t, p) => { if (a == ally) times.Add(simulation.Elapsed); };
            Charge(rosie); simulation.Step(3.3f);
            Assert.GreaterOrEqual(times.Count, 4); Assert.AreEqual(.8f, times[2] - times[1], .001f, "1/1.3 seconds, quantized to the 100ms clock.");
            // SpeedBonus is the shared movement bonus; Rosie's separate attack multiplier must leave it untouched.
            var actors = (System.Collections.IEnumerable)typeof(CombatSimulation).GetField("actors", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(simulation);
            foreach (var actor in actors)
                Assert.AreEqual(0f, actor.GetType().GetField("SpeedBonus").GetValue(actor));
        }
    }
}
