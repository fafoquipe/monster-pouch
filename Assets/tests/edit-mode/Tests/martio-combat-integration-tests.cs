using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class MartioCombatIntegrationTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        BoardManager board; CombatSimulation sim;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [SetUp] public void Setup() { var go = new GameObject("Martio integration board"); objects.Add(go); board = go.AddComponent<BoardManager>(); board.BuildBoard(); }
        [TearDown] public void Cleanup() { sim?.Stop(); foreach (var go in objects) if (go != null) Object.DestroyImmediate(go); objects.Clear(); sim = null; }
        BattleUnit Unit(UnitDefinition definition, int x, int y, BoardSide side, params int[] upgrades)
        {
            var go = new GameObject(definition.Id + objects.Count); objects.Add(go);
            var owned = new OwnedUnit(definition); foreach (int upgrade in upgrades) owned.Tricks[upgrade] = true;
            var unit = go.AddComponent<WhelpUnit>(); unit.Initialize(go.name, side, UnitStats.FromDefinition(definition, owned));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y)); return unit;
        }
        BattleUnit Martio(params int[] upgrades) => Unit(MartioDefinition.Create(), 2, 8, BoardSide.Blue, upgrades);
        BattleUnit Target(int x, int y) => Unit(new UnitDefinition { Id = "target", MaxHealth = 1000, Damage = 0, AttackInterval = 100, MoveInterval = 100 }, x, y, BoardSide.Red);
        object Actor(BattleUnit unit) => ((IEnumerable)typeof(CombatSimulation).GetField("actors", Private).GetValue(sim)).Cast<object>().Single(a => a.GetType().GetField("Unit").GetValue(a) == (object)unit);
        void Lock(BattleUnit unit) { var actor = Actor(unit); actor.GetType().GetField("NextMove").SetValue(actor, 99999); actor.GetType().GetField("NextAttack").SetValue(actor, 99999); }
        void Begin(params BattleUnit[] units) { sim = new CombatSimulation(board); sim.Begin(units); foreach (var unit in units) Lock(unit); }
        void Shoot(BattleUnit martio, BattleUnit target) { typeof(CombatSimulation).GetMethod("QueueBasicAttack", Private).Invoke(sim, new[] { Actor(martio), target }); Lock(martio); }

        [Test] public void RegisteredCharacterKeepsEditableStatsArtHopsAndSquareIcons()
        {
            var config = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
            var character = config.Characters.Single(c => c.Id == "martio");
            Assert.AreEqual("", character.Validate()); var definition = character.CreateRuntimeDefinition();
            Assert.AreEqual(character.General.MaxHealth, definition.MaxHealth); Assert.AreEqual(character.Attack.Range, definition.AttackRange);
            Assert.AreEqual(character.Attack.Interval, definition.AttackInterval); Assert.AreEqual(character.Attack.Windup, definition.AttackWindup);
            Assert.AreEqual(character.Attack.ProjectileSpeed, definition.ProjectileSpeed); Assert.AreEqual(character.General.BaseCost, definition.BaseCost);
            CollectionAssert.AreEqual(character.Upgrades.Select(a=>a.Definition.Cost),definition.Tricks.Select(t=>t.Cost));
            Assert.AreEqual(AttackKind.Ranged, definition.AttackKind);
            Assert.AreEqual(UnitTravelMotion.WholeBodyHop, character.Visuals.TravelMotion);
            Assert.IsTrue(character.Visuals.WholeBodyHopUsesPoses, "Martio120 uses its authored launch/air/landing poses.");
            Assert.IsTrue(character.Visuals.DirectionalAnimations.All(a => a.HopLeft.Length == 3 && a.HopRight.Length == 3 && a.AttackContactFrame == 2));
            var art = Resources.Load<MartioVisualAssets>("MonsterPouch/Martio/MartioVisualAssets");
            Assert.IsNotNull(art); Assert.AreEqual(3, art.UpgradeIcons.Length);
            Assert.IsTrue(art.UpgradeIcons.All(s => s != null && s.rect.width == s.rect.height));
            Assert.IsFalse(art.SampleShockwave(false, true, .1f, out _, out _, out _));
            Assert.IsTrue(art.SampleShockwave(true, true, .1f, out _, out _, out _));
        }
        [TestCase(false, 0)] [TestCase(true, 12)]
        public void FirstUpgradeAddsAdjacentExplosionWithoutDuplicatingCentralDamage(bool explosion, int neighborDamage)
        {
            var martio = Martio(explosion ? new[] { 0 } : Array.Empty<int>()); var target = Target(2, 7); var neighbor = Target(3, 7);
            Begin(martio, target, neighbor); Shoot(martio, target); sim.Step(.8f);
            Assert.AreEqual(988, target.CurrentHealth); Assert.AreEqual(1000 - neighborDamage, neighbor.CurrentHealth);
            Assert.AreEqual(3, sim.MartioContacts.Count); Assert.IsTrue(sim.MartioContacts.All(c => c.Explodes == explosion));
        }
        [Test] public void FanUpgradeLaunchesThreeIndependentGroupsWithoutChangingCentralRange()
        {
            var martio = Martio(1, 2); var target = Target(2, 4);
            Begin(martio, target); Shoot(martio, target); sim.Step(.35f);
            Assert.AreEqual(3, sim.MartioVolleys.Count);
            Assert.AreEqual(7, sim.MartioVolleys.Single(v => v.FanSide == 0).Flight.Range);
            Assert.IsTrue(sim.MartioVolleys.Where(v => v.FanSide != 0).All(v => v.Flight.Range == 3));
            var ids = sim.MartioVolleys.Select(v => v.Id).ToArray();
            Assert.AreEqual(3, ids.Distinct().Count());
        }
        [Test] public void SourceResetInvalidatesRetainedContactsAndClearsItsRemainingFlights()
        {
            var martio=Martio(1,2);var target=Target(2,7);
            Begin(martio,target);Shoot(martio,target);sim.Step(.8f);
            Assert.IsNotEmpty(sim.MartioContacts);
            martio.ResetForCombat();
            Assert.IsTrue(sim.MartioContacts.All(c=>!c.IsCurrent));
            sim.Step(.1f);
            Assert.IsEmpty(sim.MartioContacts);Assert.IsEmpty(sim.MartioVolleys);
        }
        [Test] public void PiercingHasOneDirectHitPerEnemyAndCanDamageItAgainThroughAdjacentExplosions()
        {
            var martio = Martio(0, 1); var first = Target(2, 7); var second = Target(2, 6); var flank = Target(3, 6);
            Begin(martio, first, second, flank); Shoot(martio, first);
            var hits = new Dictionary<int, CombatSimulation.MartioContact>();
            for (int i = 0; i < 20; i++) { sim.Step(.1f); foreach (var hit in sim.MartioContacts) hits[hit.Id] = hit; }
            var firstHits = hits.Values.Where(h => h.Cell == first.CurrentCell).ToArray();
            Assert.AreEqual(3, firstHits.Length); Assert.AreEqual(3, firstHits.Select(h => h.Projectile).Distinct().Count());
            Assert.Less(first.CurrentHealth, 988); Assert.Less(second.CurrentHealth, 1000);
        }
        [Test] public void SevenCellFlightContinuesPastFourCellAcquisitionRangeAndStops()
        {
            var martio = Martio(1); var acquired = Target(2, 4); var far = Target(2, 1); var beyond = Target(2, 0);
            Begin(martio, acquired, far, beyond); Shoot(martio, acquired); sim.Step(2f);
            Assert.Less(far.CurrentHealth, 1000); Assert.AreEqual(1000, beyond.CurrentHealth); Assert.IsEmpty(sim.MartioVolleys);
        }
        [Test] public void InterruptionBeforeReleaseCancelsAllThreeProjectiles()
        {
            var martio = Martio(); var target = Target(2, 7); Begin(martio, target); Shoot(martio, target); sim.Step(.1f);
            typeof(CombatSimulation).GetMethod("InterruptAttack", Private).Invoke(sim, new[] { Actor(martio) }); Lock(martio); sim.Step(.8f);
            Assert.AreEqual(1000, target.CurrentHealth); Assert.IsEmpty(sim.MartioVolleys); Assert.IsEmpty(sim.MartioContacts);
        }
        [Test] public void ExistingShieldResolverAbsorbsMartioDamage()
        {
            var martio = Martio(); var target = Target(2, 7); Begin(martio, target); target.AddShield(20); Shoot(martio, target); sim.Step(.8f);
            Assert.AreEqual(1000, target.CurrentHealth); Assert.AreEqual(8, target.Shield);
        }
        [Test] public void BasicAttackBonusAffectsMarsOnceWithoutChangingMoonsOrSplash()
        {
            var martio = Martio(0); var target = Target(2, 7); var neighbor = Target(3, 7);
            Begin(martio, target, neighbor); var actor = Actor(martio);
            actor.GetType().GetField("NextBasicBonus").SetValue(actor, 5);
            Shoot(martio, target); sim.Step(.8f);
            Assert.AreEqual(983, target.CurrentHealth); Assert.AreEqual(988, neighbor.CurrentHealth);
            Assert.AreEqual(0, actor.GetType().GetField("NextBasicBonus").GetValue(actor));
        }
        [Test] public void CadenceDoesNotWaitForPreviousSevenCellVolleyAndStopClearsFlights()
        {
            var martio = Martio(1); var target = Target(2, 4); Begin(martio, target);
            var actor = Actor(martio); actor.GetType().GetField("NextAttack").SetValue(actor, 1);
            var launches = new List<float>(); sim.Attacked += (source, _, __) => { if (source == martio) launches.Add(sim.Elapsed); };
            int peak = 0; for (int i = 0; i < 45; i++) { sim.Step(.1f); peak = Math.Max(peak, sim.MartioVolleys.Count); }
            Assert.GreaterOrEqual(launches.Count, 3); for (int i = 1; i < launches.Count; i++) Assert.AreEqual(1.3f, launches[i] - launches[i - 1], .001f);
            Assert.GreaterOrEqual(peak, 2); sim.Stop(); Assert.IsEmpty(sim.MartioVolleys); Assert.IsEmpty(sim.MartioContacts);
        }
    }
}
