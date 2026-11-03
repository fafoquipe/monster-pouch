using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class TaurisAbilityDurationTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private BoardManager board;
        private BattleUnit actor;
        private UnitPresentation view;
        private UnitArt art;
        private Texture2D texture;
        private Sprite[] frames;
        private CombatSimulation simulation;

        [SetUp]
        public void SetUp()
        {
            var boardObject = new GameObject("tauris-duration-board");
            objects.Add(boardObject);
            board = boardObject.AddComponent<BoardManager>();
            board.BuildBoard();
            UnitDefinition definition = DocumentedRoster.CreateTauris();
            definition.MaxHealth = 1000;
            definition.Damage = 10;
            definition.AttackInterval = .7f;
            definition.AttackWindup = .2f;
            definition.IQSpeed = 10;
            definition.EnergyPerAttack = definition.EnergyPerSecond = definition.EnergyOnDamage = 0;
            actor = CreateUnit(definition, 2, 5, BoardSide.Blue);
            texture = new Texture2D(5, 1, TextureFormat.RGBA32, false);
            frames = new Sprite[5];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(texture, new Rect(i, 0, 1, 1), new Vector2(.5f, 0), 1);
            art = new UnitArt { Id = "tauris", Portrait = frames[0], ReferencePixelWidth = 1, AbilityAnimationDuration = 5 };
            for (int direction = 0; direction < 8; direction++)
                art.DirectionalAnimations[direction] = new UnitDirectionalAnimation
                {
                    Idle = new[] { frames[0] },
                    Attack = new[] { frames[0], frames[1], frames[0] }, AttackContactFrame = 1,
                    Special = new[] { frames[1], frames[2], frames[3], frames[4] },
                    SpecialDuration = .58f, FlipX = direction >= 5, SpecialFlipX = false
                };
            view = actor.gameObject.AddComponent<UnitPresentation>();
            view.Configure(actor, null, art);
        }

        [TearDown]
        public void TearDown()
        {
            simulation?.Stop();
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            foreach (Sprite frame in frames) Object.DestroyImmediate(frame);
            Object.DestroyImmediate(texture);
        }

        [TestCase(1.4f)]
        [TestCase(5f)]
        public void LethalBiteCompletesItsConfiguredCastThenResumesBasicAttacks(float duration)
        {
            art.AbilityAnimationDuration = 9;
            ConfigureAbilityTiming(duration);
            BattleUnit victim = CreateTarget(2, 4, 100);
            BattleUnit replacement = CreateTarget(3, 4, 1000);
            simulation = new CombatSimulation(board);
            simulation.Begin(new[] { actor, victim, replacement });
            view.Configure(actor, null, art);
            UnitStats originalStats = actor.BaseStats;
            int supers = 0, replacementAttacks = 0;
            simulation.Attacked += (source, target, projectile) =>
            {
                if (source != actor) return;
                if (target == replacement) replacementAttacks++;
                view.Attack(target.transform.position, projectile, CombatSimulation.GetImpactDelay(source, target));
            };
            simulation.AbilityUsed += (source, kind) =>
            {
                if (source != actor) return;
                if (kind == "tauris-super") supers++;
                view.UseAbility(kind);
            };
            actor.AddEnergy(actor.BaseStats.EnergyMax);
            simulation.Step(.1f);
            view.AdvancePresentation(0);
            Assert.AreEqual(1, supers);
            Assert.IsTrue(view.IsUsingAbility);
            int attackVersion = actor.AttackResetVersion;

            Advance(duration-.01f);
            Assert.IsTrue(victim.IsAlive,"The target lives until the configured bite cast completes.");
            Assert.IsTrue(view.IsUsingAbility);
            Advance(.02f);
            Assert.IsFalse(victim.IsAlive);
            Assert.Greater(actor.AttackResetVersion,attackVersion);
            Assert.IsFalse(view.IsUsingAbility);
            Advance(.5f);
            Assert.Greater(replacementAttacks,0,"Basic attacks resume after the bite.");
            Assert.Less(replacement.CurrentHealth,1000);
            Assert.AreEqual(1, supers);
            Assert.AreSame(originalStats, actor.BaseStats);
            Assert.AreEqual(.7f, actor.BaseStats.AttackInterval, .00001f);
            Assert.AreEqual(.2f, actor.BaseStats.AttackWindup, .00001f);
            Assert.AreEqual(10, actor.BaseStats.Attack);
            Advance(.002f);
            Assert.IsFalse(view.IsUsingAbility);
        }

        [Test]
        public void DurationAndAuthoredSpecialMirrorRemainValidInAllEightDirections()
        {
            var directions = new[] { new Vector2Int(0, -1), new Vector2Int(1, -1), new Vector2Int(1, 0),
                new Vector2Int(1, 1), new Vector2Int(0, 1), new Vector2Int(-1, 1), new Vector2Int(-1, 0), new Vector2Int(-1, -1) };
            art.AbilityAnimationDuration = 9;
            ConfigureAbilityTiming(.7f,.7f);
            for (int direction = 0; direction < directions.Length; direction++)
            {
                view.Configure(actor, null, art);
                view.Face(directions[direction]);
                view.UseAbility("tauris-super");
                view.AdvancePresentation(.2f);
                actor.InterruptAttack();
                view.AdvancePresentation(.5f);
                Assert.AreEqual((UnitFacing)direction, view.Facing);
                Assert.AreSame(frames[3], view.Renderer.sprite);
                Assert.IsFalse(view.Renderer.flipX, "The special keeps its own authored direction, independent of the basic mirror.");
                Assert.IsTrue(view.IsUsingAbility);
                view.AdvancePresentation(.701f);
                Assert.IsFalse(view.IsUsingAbility);
            }
        }

        [Test]
        public void HungerKillRewardDoesNotRestartTheSameBitesVisualClock()
        {
            art.AbilityAnimationDuration = 9;
            ConfigureAbilityTiming(.7f,.7f);
            view.UseAbility("tauris-super");
            view.AdvancePresentation(.7f);
            view.UseAbility("tauris-kill");
            actor.InterruptAttack();
            view.AdvancePresentation(.699f);
            Assert.IsTrue(view.IsUsingAbility);
            Assert.AreSame(frames[4], view.Renderer.sprite);
            view.AdvancePresentation(.002f);
            Assert.IsFalse(view.IsUsingAbility, "The kill reward must not extend a 1.4-second bite to 2.1 seconds.");
        }

        [TestCase("stun")]
        [TestCase("combat-reset")]
        [TestCase("reset-then-new-attack")]
        [TestCase("death")]
        [TestCase("revive")]
        [TestCase("freeze")]
        public void RealInterruptionStillCancelsAnExtendedBite(string transition)
        {
            view.UseAbility("tauris-super");
            view.AdvancePresentation(.3f);
            if (transition == "stun") { actor.IsStunned = true; actor.InterruptAttack(); }
            else if (transition == "combat-reset") actor.ResetForCombat();
            else if (transition == "reset-then-new-attack")
            {
                actor.ResetForCombat();
                view.Attack(Vector3.right, false, .2f);
            }
            else if (transition == "death") view.Die();
            else if (transition == "revive") view.BeginRevive(.8f);
            else view.Freeze();
            view.AdvancePresentation(0);
            Assert.IsFalse(view.IsUsingAbility);
            if (transition == "reset-then-new-attack")
                Assert.IsTrue(view.IsAttacking, "Cancelling the old bite must not cancel an attack started after the combat reset.");
        }

        private void ConfigureAbilityTiming(float duration,float recovery=0)
        {
            var definition=DocumentedRoster.CreateTauris();definition.MaxHealth=1000;definition.Damage=10;
            definition.AttackInterval=.7f;definition.AttackWindup=.2f;definition.IQSpeed=10;
            definition.AttackHold=definition.AttackRecovery=0; // Test the bite recovery independently.
            definition.EnergyPerAttack=definition.EnergyPerSecond=definition.EnergyOnDamage=0;
            definition.BaseAbility.Parameters=new[]{new AbilityParameter{Key="castDuration",Value=duration},
                new AbilityParameter{Key="holdDuration",Value=0},new AbilityParameter{Key="recoveryDuration",Value=recovery}};
            actor.Initialize(actor.UnitId,actor.Side,UnitStats.FromDefinition(definition));
        }

        private void Advance(float seconds)
        {
            simulation.Step(seconds);
            view.AdvancePresentation(seconds);
        }

        private BattleUnit CreateTarget(int x, int y, int health) => CreateUnit(new UnitDefinition
        {
            Id = "target", MaxHealth = health, Damage = 0, AttackRange = 9,
            AttackInterval = 100, AttackWindup = .1f, MoveInterval = 100
        }, x, y, BoardSide.Red);

        private BattleUnit CreateUnit(UnitDefinition definition, int x, int y, BoardSide side)
        {
            var unitObject = new GameObject(definition.Id + "-" + objects.Count);
            objects.Add(unitObject);
            BattleUnit unit = definition.IsMonster ? (BattleUnit)unitObject.AddComponent<MonsterUnit>() : unitObject.AddComponent<WhelpUnit>();
            unit.Initialize(unitObject.name, side, UnitStats.FromDefinition(definition));
            unit.transform.position = new Vector3(x, -y, 0);
            Assert.IsTrue(board.TryOccupyCell(unit, x, y));
            return unit;
        }
    }
}
