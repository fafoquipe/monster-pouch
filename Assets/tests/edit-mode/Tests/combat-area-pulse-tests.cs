using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class CombatAreaPulseTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        BoardManager board;
        CombatSimulation simulation;
        [SetUp] public void SetUp()
        {
            var go = new GameObject("area-pulse-board"); objects.Add(go);
            board = go.AddComponent<BoardManager>(); board.BuildBoard();
        }
        [TearDown] public void TearDown()
        {
            simulation?.Stop();
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }
        BattleUnit Unit(UnitDefinition definition, int x, int y, BoardSide side = BoardSide.Blue, int trick = -1)
        {
            var owned = new OwnedUnit(definition); if (trick >= 0) owned.Tricks[trick] = true;
            var go = new GameObject(side + "-" + definition.Id + objects.Count); objects.Add(go);
            BattleUnit unit = definition.IsMonster ? (BattleUnit)go.AddComponent<MonsterUnit>() : go.AddComponent<WhelpUnit>();
            unit.Initialize(go.name, side, UnitStats.FromDefinition(definition, owned));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y)); return unit;
        }
        static UnitDefinition Stationary(UnitDefinition definition)
        {
            definition.MaxHealth = 1000; definition.Damage = 0; definition.AttackRange = 9;
            definition.AttackWindup = .1f; definition.AttackInterval = 100; definition.MoveInterval = 100;
            definition.EnergyPerSecond = definition.EnergyPerAttack = definition.EnergyOnDamage = 0;
            definition.ProjectileSpeed = 100; return definition;
        }
        BattleUnit Enemy(int x, int y) => Unit(Stationary(new UnitDefinition { Id = "target" + objects.Count }), x, y, BoardSide.Red);
        BattleUnit Bugaloo(float taunt = 1, float reflect = 3, int x = 2, int y = 5)
        {
            var definition = Stationary(DocumentedRoster.CreateBugaloo());
            definition.BaseAbility.Parameters = new[] {
                new AbilityParameter { Key = "tauntDuration", Value = taunt },
                new AbilityParameter { Key = "duration", Value = reflect },
                new AbilityParameter { Key = "radius", Value = 2 }
            };
            return Unit(definition, x, y);
        }
        void Begin(params BattleUnit[] units) { simulation = new CombatSimulation(board); simulation.Begin(units); }
        void Activate(BattleUnit source) { source.AddEnergy(source.BaseStats.EnergyMax); simulation.Step(.1f); }
        static Vector2Int[] Positions(IEnumerable<BoardCell> cells) => cells.Select(cell => cell.Coordinates).ToArray();

        [Test] public void TauntIsOneSnapshotWithExactCellsAndDoesNotRecruitLatecomers()
        {
            var source = Bugaloo(x: 0, y: 0); var victim = Enemy(1, 0); var late = Enemy(5, 9);
            Begin(source, victim, late);
            var pulses = new List<BoardCell[]>();
            simulation.AreaEffect += (actor, kind, cells) => { if (kind == "bugaloo-taunt") pulses.Add(cells); };
            Activate(source);
            Assert.IsTrue(victim.IsTaunted); Assert.IsFalse(late.IsTaunted);
            Assert.IsTrue(board.TryRepositionUnit(late, board.GetCell(2, 2))); simulation.Step(.5f);
            Assert.IsFalse(late.IsTaunted, "Entering the old pulse cannot apply control.");
            Assert.AreEqual(1, pulses.Count);
            CollectionAssert.AreEquivalent(from x in Enumerable.Range(0, 3) from y in Enumerable.Range(0, 3)
                select new Vector2Int(x, y), Positions(pulses[0]));
        }

        [Test] public void TauntPersistsOutsideItsRadiusAfterReflectionEndsThenExpiresPrecisely()
        {
            var source = Bugaloo(1, .3f); var victim = Enemy(2, 4); Begin(source, victim);
            Activate(source); Assert.IsTrue(board.TryRepositionUnit(victim, board.GetCell(5, 0)));
            simulation.Step(.3f); Assert.IsFalse(source.IsReflecting); Assert.IsTrue(victim.IsTaunted);
            simulation.Step(.6f); Assert.IsTrue(victim.IsTaunted);
            simulation.Step(.1f); Assert.IsFalse(victim.IsTaunted);
        }

        [TestCase("death")] [TestCase("source-reset")] [TestCase("victim-reset")] [TestCase("stop")]
        public void TauntClearsWhenItsSourceOrCombatBecomesInvalid(string ending)
        {
            var source = Bugaloo(); var victim = Enemy(2, 4);
            var ally = Unit(Stationary(new UnitDefinition { Id = "ally" }), 4, 5);
            Begin(source, victim, ally); Activate(source); Assert.IsTrue(victim.IsTaunted);
            int resetVersion = victim.AttackResetVersion;
            if (ending == "death") source.ApplyDamage(10000);
            else if (ending == "source-reset") source.ResetForCombat();
            else if (ending == "victim-reset") victim.ResetForCombat();
            else simulation.Stop();
            if (ending != "stop") simulation.Step(.1f);
            Assert.IsFalse(victim.IsTaunted);
            if (ending == "death") Assert.Greater(victim.AttackResetVersion, resetVersion, "Death retains normal attack cancellation and reaction handling.");
        }

        [Test] public void TauntCannotCaptureControlImmuneAtongAndZeroDurationDoesNotApplyControl()
        {
            var source = Bugaloo();
            var immune = Unit(Stationary(DocumentedRoster.CreateAtong()), 2, 4, BoardSide.Red, 2);
            var victim = Enemy(3, 5); Begin(source, immune, victim); Activate(source);
            Assert.IsFalse(immune.IsTaunted); Assert.IsTrue(victim.IsTaunted);
            simulation.Stop();
            var zero = Bugaloo(0, x: 5, y: 4); Begin(zero, victim); Activate(zero);
            Assert.IsTrue(zero.IsReflecting); Assert.IsFalse(victim.IsTaunted);
        }

        [Test] public void LegacyCustomReflectionDurationIsPreservedAsTauntFallback()
        {
            var definition = DocumentedRoster.CreateBugaloo();
            definition.BaseAbility.Parameters = new[] { new AbilityParameter { Key = "duration", Value = .7f } };
            Assert.AreEqual(.7f, AbilityBalance.Resolve(definition.BaseAbility).Single(value => value.Key == "tauntDuration").Value);
            definition.BaseAbility.Parameters = new[] { new AbilityParameter { Key = "duration", Value = .7f }, new AbilityParameter { Key = "tauntDuration", Value = 0 } };
            Assert.AreEqual(0, AbilityBalance.Resolve(definition.BaseAbility).Single(value => value.Key == "tauntDuration").Value);
        }

        [Test] public void TauntReleasesWhenSeporaHidesItsSource()
        {
            var source = Bugaloo(); var victim = Enemy(2, 4);
            var sepora = Unit(Stationary(DocumentedRoster.CreateSepora()), 3, 5, trick: 0);
            Begin(source, victim, sepora); Activate(source); Assert.IsTrue(victim.IsTaunted);
            sepora.ApplyDamage(600); simulation.Step(.1f);
            Assert.IsTrue(source.IsInvisible); Assert.IsFalse(victim.IsTaunted);
        }

        [TestCase(false)] [TestCase(true)]
        public void GochanEmitsOneExactImpactAreaIncludingEmptyCells(bool expanded)
        {
            var definition = Stationary(DocumentedRoster.CreateGochan()); definition.Damage = 10;
            var source = Unit(definition, 2, 7, trick: expanded ? 2 : -1);
            var target = Enemy(2, 4); var other = Enemy(3, 4); Begin(source, target, other);
            var pulses = new List<BoardCell[]>(); string effect = null;
            simulation.AreaEffect += (actor, kind, cells) => { if (actor == source) { effect = kind; pulses.Add(cells); } };
            simulation.Step(.4f);
            Assert.AreEqual(1, pulses.Count); Assert.AreEqual(expanded ? "gochan-square" : "gochan-cross", effect);
            var expected = from x in Enumerable.Range(1, 3) from y in Enumerable.Range(3, 3)
                where expanded || x == 2 || y == 4 select new Vector2Int(x, y);
            CollectionAssert.AreEquivalent(expected, Positions(pulses[0]));
            Assert.AreEqual(990, target.CurrentHealth); Assert.AreEqual(990, other.CurrentHealth);
        }

        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void DiamondBasicSplashHitsOnlyEnemiesInItsEditedFootprint(BoardSide side)
        {
            var definition=Stationary(DocumentedRoster.CreateGochan());definition.Damage=10;
            definition.Tricks[2].Area=new AbilityAreaDefinition{Shape=AbilityAreaShape.Diamond,DiamondRadius=1};
            var source=Unit(definition,2,7,side,2);
            var opponent=side==BoardSide.Blue?BoardSide.Red:BoardSide.Blue;
            var target=Unit(Stationary(new UnitDefinition{Id="primary"}),2,4,opponent);target.IsPassiveTrainingTarget=true;
            var adjacent=Unit(Stationary(new UnitDefinition{Id="adjacent"}),3,4,opponent);adjacent.IsPassiveTrainingTarget=true;
            var diagonal=Unit(Stationary(new UnitDefinition{Id="diagonal"}),3,3,opponent);diagonal.IsPassiveTrainingTarget=true;
            var ally=Unit(Stationary(new UnitDefinition{Id="ally"}),1,4,side);ally.IsPassiveTrainingTarget=true;
            Begin(source,target,adjacent,diagonal,ally);
            var footprints=new List<BoardCell[]>();
            simulation.AreaEffect+=(actor,effect,cells)=>{if(actor==source&&effect=="edited-area:gochan-expanded-seal")footprints.Add(cells);};
            simulation.Step(.4f);
            Assert.AreEqual(990,target.CurrentHealth);Assert.AreEqual(990,adjacent.CurrentHealth);
            Assert.AreEqual(1000,diagonal.CurrentHealth);Assert.AreEqual(1000,ally.CurrentHealth);
            Assert.AreEqual(1,footprints.Count);
            CollectionAssert.AreEquivalent(new[]{board.GetCell(2,4),board.GetCell(1,4),board.GetCell(3,4),board.GetCell(2,3),board.GetCell(2,5)},footprints[0]);
        }

        [Test] public void AnuikRevivalAreaIsCapturedBeforeEnemiesArePushed()
        {
            var definition = Stationary(DocumentedRoster.CreateAnuik()); definition.ReviveDelay = .2f;
            var source = Unit(definition, 2, 5, trick: 1); var target = Enemy(2, 4); Begin(source, target);
            var pulses = new List<BoardCell[]>();
            simulation.AreaEffect += (actor, kind, cells) => { if (kind == "anuik-revive-impact") pulses.Add(cells); };
            source.ApplyDamage(10000); simulation.Step(.4f);
            Assert.AreEqual(1, pulses.Count);
            CollectionAssert.AreEquivalent(from x in Enumerable.Range(1, 3) from y in Enumerable.Range(4, 3)
                select new Vector2Int(x, y), Positions(pulses[0]));
            Assert.AreEqual(990, target.CurrentHealth); Assert.AreEqual(new Vector2Int(2, 3), target.CurrentCell.Coordinates);
        }
    }
}
