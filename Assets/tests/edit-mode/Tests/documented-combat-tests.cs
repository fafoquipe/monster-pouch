using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class DocumentedCombatTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private BoardManager board;
        private CombatSimulation simulation;
        [SetUp] public void SetUp()
        { var go = new GameObject("pdf-board"); objects.Add(go); board = go.AddComponent<BoardManager>(); board.BuildBoard(); }
        [TearDown] public void TearDown()
        { simulation?.Stop(); for (int i = objects.Count-1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]); objects.Clear(); }
        private BattleUnit Unit(UnitDefinition definition, int x = 2, int y = 5, BoardSide side = BoardSide.Blue, params int[] tricks)
        {
            var owned = new OwnedUnit(definition); foreach (int index in tricks) owned.Tricks[index] = true;
            var go = new GameObject(side + "-" + definition.Id + objects.Count); objects.Add(go);
            BattleUnit unit = definition.IsMonster ? (BattleUnit)go.AddComponent<MonsterUnit>() : go.AddComponent<WhelpUnit>();
            unit.Initialize(go.name, side, UnitStats.FromDefinition(definition, owned)); Assert.IsTrue(board.TryOccupyCell(unit, x, y)); return unit;
        }
        private BattleUnit Enemy(int x = 2, int y = 4, int health = 1000, int damage = 0)
            => Unit(new UnitDefinition { Id = "target" + objects.Count, MaxHealth = health, Damage = damage, AttackRange = 9, AttackInterval = 100, AttackWindup = .1f, MoveInterval = 100 }, x, y, BoardSide.Red);
        private CombatSimulation Begin(params BattleUnit[] units)
        { simulation = new CombatSimulation(board); simulation.Begin(units); return simulation; }
        private static UnitDefinition Fast(UnitDefinition definition, int damage = 10)
        { definition.Damage = damage; definition.AttackInterval = .2f; definition.AttackWindup = .1f; return definition; }

        // Count actual contacts: authored anticipation/hold/recovery may span several ticks.
        void UntilHits(BattleUnit source,int required)
        {
            int hits=0;
            System.Action<BattleUnit,BattleUnit,int> listener=(actor,target,damage)=>{if(actor==source)hits++;};
            simulation.Impacted+=listener;
            for(int tick=0;tick<200&&hits<required;tick++)simulation.Step(.1f);
            simulation.Impacted-=listener;Assert.AreEqual(required,hits);
        }

        [Test] public void Stein_BasicAttackHitsOnlyOneTargetWithoutStunning()
        {
            var stein = Unit(Fast(DocumentedRoster.CreateStein())); var first = Enemy(); var second = Enemy(3,4);
            Begin(stein, first, second).Step(.4f);
            Assert.AreEqual(990, first.CurrentHealth); Assert.AreEqual(1000, second.CurrentHealth);
            Assert.IsFalse(first.IsStunned);Assert.Greater(stein.Energy,0);
        }
        [Test] public void Stein_NeverDuplicatesItsBasicOnALoneEnemy()
        {
            var stein = Unit(Fast(DocumentedRoster.CreateStein())); var enemy = Enemy(); Begin(stein, enemy).Step(.4f);
            Assert.AreEqual(990, enemy.CurrentHealth);
        }
        [TestCase(25f)]
        [TestCase(40f)]
        public void Stein_TransfersConfiguredEnergyToHimselfAndHorizontalNeighbors(float amount)
        {
            var definition=DocumentedRoster.CreateStein();
            definition.Tricks[2].Parameters=new[]{new AbilityParameter{Key="energy",Value=amount}};
            var stein = Unit(definition,2,5,BoardSide.Blue,2);
            var left = Unit(DocumentedRoster.CreateKayon(),1,5);
            var right = Unit(DocumentedRoster.CreateFlo(),3,5);
            var upper = Unit(DocumentedRoster.CreateFlo(),2,6);
            var distant = Unit(DocumentedRoster.CreateKayon(),4,5);
            var enemy = Unit(DocumentedRoster.CreateStein(),2,4,BoardSide.Red);
            Begin(stein,left,right,upper,distant,enemy);
            Assert.AreEqual(amount,stein.Energy,"Stein receives his own opening gift exactly once.");
            Assert.AreEqual(amount,left.Energy);Assert.AreEqual(amount,right.Energy);
            Assert.AreEqual(0,upper.Energy);Assert.AreEqual(0,distant.Energy);Assert.AreEqual(0,enemy.Energy);
        }
        [Test] public void Stein_WithoutEnergyGiftUpgradeDoesNotGrantOpeningEnergy()
        {
            var stein=Unit(DocumentedRoster.CreateStein());
            var ally=Unit(DocumentedRoster.CreateKayon(),1,5);
            Begin(stein,ally,Enemy());
            Assert.AreEqual(0,stein.Energy);Assert.AreEqual(0,ally.Energy);
        }
        [Test] public void Anuik_SecondReturnUsesQuarterHealthThenFinalDeath()
        {
            var anuik = Unit(DocumentedRoster.CreateAnuik(),2,5,BoardSide.Blue,0); var enemy = Enemy(); Begin(anuik,enemy);
            anuik.ApplyDamage(1000); simulation.Step(.9f); Assert.AreEqual(45,anuik.CurrentHealth);
            anuik.ApplyDamage(1000); simulation.Step(.9f); Assert.AreEqual(23,anuik.CurrentHealth);
            anuik.ApplyDamage(1000); simulation.Step(.1f); Assert.IsTrue(simulation.Finished); Assert.AreEqual(BoardSide.Red,simulation.Winner);
        }
        [Test] public void Anuik_ThirdUpgradeRicochetsOnceToAnotherEnemyAndUsesEditedMultiplier()
        {
            UnitDefinition definition = DocumentedRoster.CreateAnuik();
            Assert.AreEqual("anuik-bounce", definition.Tricks[2].EffectId);
            Assert.IsFalse(definition.Tricks.Any(trick => trick.EffectId == "anuik-revive-jump"));
            Assert.AreEqual(.5f, AbilityBalance.Resolve(definition.Tricks[2]).Single(parameter => parameter.Key == "bounceDamageMultiplier").Value, .0001f);
            definition.Damage = 20; definition.AttackInterval = 100; definition.AttackWindup = .1f; definition.ProjectileSpeed = 100;
            definition.Tricks[2].Parameters = new[] { new AbilityParameter { Key = "bounceDamageMultiplier", Value = .25f } };
            var anuik = Unit(definition, 2, 5, BoardSide.Blue, 2);
            var primary = Enemy(2, 4, health: 100);
            var secondary = Enemy(4, 4, health: 100);
            UnitDefinition allyDefinition = DocumentedRoster.CreateAtori();
            allyDefinition.Damage = 0; allyDefinition.AttackInterval = 100;
            var ally = Unit(allyDefinition, 3, 5, BoardSide.Blue);
            int ricochets = 0;
            Begin(anuik, primary, secondary, ally);
            simulation.ProjectileBounced += (unit, origin, target, duration) => { if (unit == anuik) ricochets++; };

            simulation.Step(.3f);
            Assert.AreEqual(80, primary.CurrentHealth);
            Assert.AreEqual(100, secondary.CurrentHealth, "The ricochet has its own bounded travel step.");
            simulation.Step(.2f);
            Assert.AreEqual(95, secondary.CurrentHealth, "The editable 0.25 multiplier applies to the original 20 damage.");
            Assert.AreEqual(ally.BaseStats.MaxHealth, ally.CurrentHealth, "Ricochets never select allies.");
            Assert.AreEqual(1, ricochets, "A ricochet cannot recursively create another ricochet.");
            Assert.AreEqual(0, simulation.PendingImpactCount);
        }

        [Test] public void Anuik_RicochetDoesNothingWhenNoSecondEnemyExists()
        {
            UnitDefinition definition = DocumentedRoster.CreateAnuik();
            definition.Damage = 20; definition.AttackInterval = 100; definition.AttackWindup = .1f; definition.ProjectileSpeed = 100;
            var anuik = Unit(definition, 2, 5, BoardSide.Blue, 2);
            var onlyEnemy = Enemy(2, 4, health: 100);
            int ricochets = 0;
            Begin(anuik, onlyEnemy);
            simulation.AbilityUsed += (unit, effect) => { if (effect == "anuik-bounce") ricochets++; };
            simulation.Step(.5f);
            Assert.AreEqual(80, onlyEnemy.CurrentHealth);
            Assert.AreEqual(0, ricochets);
            Assert.AreEqual(0, simulation.PendingImpactCount);
        }
        [Test] public void Bugaloo_ProtectionDoesNotReduceReflectedDamage()
        {
            var buga = Unit(DocumentedRoster.CreateBugaloo(),2,5,BoardSide.Blue,0);
            // Melee attacker resolves after one tick, before Bugaloo's first post-super punch.
            var melee = new UnitDefinition { Id="punch",MaxHealth=1000,Damage=20,AttackRange=1,AttackInterval=100,AttackWindup=.1f };
            var punch = Unit(melee,2,4,BoardSide.Red);
            Begin(buga,punch); buga.AddEnergy(100); simulation.Step(.2f);
            Assert.AreEqual(100,buga.CurrentHealth); Assert.AreEqual(1000,punch.CurrentHealth);
            simulation.Step(.5f); Assert.AreEqual(980,punch.CurrentHealth);
        }
        [Test] public void Tauris_ChargedBiteExecutesButHungerReplacesItWithHalfCurrentHealth()
        {
            var tauris = Unit(DocumentedRoster.CreateTauris(),2,5,BoardSide.Blue,2); var enemy = Enemy(); Begin(tauris,enemy);
            tauris.AddEnergy(100); simulation.Step(1f); Assert.AreEqual(500,enemy.CurrentHealth); Assert.IsTrue(enemy.IsAlive);
        }
        [Test] public void Tauris_ChargedBiteActuallyExecutesWithoutHunger()
        {
            var tauris=Unit(DocumentedRoster.CreateTauris()); var enemy=Enemy(); Begin(tauris,enemy); tauris.AddEnergy(100); simulation.Step(1f);
            Assert.IsFalse(enemy.IsAlive); Assert.IsTrue(simulation.Finished);
        }
        [TestCase(3,4,3,5,2,4)] [TestCase(2,4,1,4,3,4)]
        public void SweepUsesTargetAndTwoAdjacentRingCells(int tx,int ty,int ax,int ay,int bx,int by)
        {
            var cells=CombatSimulation.SweepCells(new Vector2Int(2,5),new Vector2Int(tx,ty));
            Assert.AreEqual(3,cells.Count); CollectionAssert.Contains(cells,new Vector2Int(tx,ty)); CollectionAssert.Contains(cells,new Vector2Int(ax,ay)); CollectionAssert.Contains(cells,new Vector2Int(bx,by));
        }
        [Test] public void PopowDash_StunsCrossedEnemiesAndMovesWithoutOverlapping()
        {
            var popow=Unit(DocumentedRoster.CreatePopow(),2,8); var near=Enemy(2,6); var far=Enemy(2,2); Begin(popow,near,far); popow.AddEnergy(100); simulation.Step(.1f);
            Assert.IsFalse(near.IsStunned); Assert.IsFalse(far.IsStunned);
            simulation.Step(.4f);
            Assert.IsTrue(near.IsStunned); Assert.IsTrue(far.IsStunned);
            Assert.IsTrue(near.IsPopowStunned&&far.IsPopowStunned);Assert.IsFalse(near.IsElectrocuted); Assert.AreEqual(1,popow.CurrentCell.Y); Assert.AreSame(far,board.GetCell(2,2).OccupiedBy);
        }
        [Test] public void Sepora_StormHitsThreeDistinctTargetsThenRestoresNormalCadenceAndEnergy()
        {
            var definition=DocumentedRoster.CreateSepora();definition.AttackInterval=1.5f;
            definition.EnergyPerSecond=10;definition.EnergyOnDamage=30;
            var sepora=Unit(definition);var a=Enemy();var b=Enemy(3,5);var c=Enemy(2,6);var d=Enemy(4,5);
            Begin(sepora,a,b,c,d);sepora.AddEnergy(100);
            var times=new List<float>();var attackedTargets=new List<BattleUnit>();
            simulation.Attacked+=(source,target,projectile)=>{if(source==sepora){times.Add(simulation.Elapsed);attackedTargets.Add(target);}};
            simulation.Step(.1f);Assert.IsEmpty(times);simulation.Step(.6f);
            Assert.AreEqual(3,times.Count);Assert.AreEqual(994,a.CurrentHealth);Assert.AreEqual(994,b.CurrentHealth);Assert.AreEqual(994,c.CurrentHealth);Assert.AreEqual(1000,d.CurrentHealth);
            Assert.IsTrue(sepora.IsEnergyLocked);Assert.AreEqual(0,sepora.AddEnergy(100));
            simulation.Step(4.9f);Assert.AreEqual(15,times.Count);Assert.AreEqual(0,sepora.Energy);
            simulation.Step(.1f);Assert.IsFalse(sepora.IsEnergyLocked);
            Assert.AreEqual(16,times.Count);Assert.AreEqual(5.7f,times.Last(),.001f);
            Assert.AreEqual(.3f,CombatSimulation.GetAttackWindup(sepora),.001f);
            var firstBasicTarget=attackedTargets.Last();int beforeFirstHit=firstBasicTarget.CurrentHealth;
            Assert.AreEqual(3910,new[]{a,b,c,d}.Sum(u=>u.CurrentHealth),"The first basic must prepare after the storm, not hit immediately.");
            simulation.Step(.2f);Assert.AreEqual(beforeFirstHit,firstBasicTarget.CurrentHealth);
            simulation.Step(.1f);Assert.AreEqual(beforeFirstHit-6,firstBasicTarget.CurrentHealth,"The prepared ray must land exactly when its first-hit preparation ends.");
            simulation.Step(1.1f);Assert.AreEqual(16,times.Count);
            simulation.Step(.1f);Assert.AreEqual(17,times.Count);Assert.AreEqual(7.2f,times.Last(),.001f);
            Assert.Greater(sepora.Energy,0);
        }
        [Test] public void Sepora_ChargedRayHitsOnReleaseWithoutDuplicatingMissingTargets()
        {
            var sepora=Unit(DocumentedRoster.CreateSepora());var enemy=Enemy(2,1);Begin(sepora,enemy);sepora.AddEnergy(100);
            Assert.AreEqual(0,CombatSimulation.GetAttackWindup(sepora));Assert.AreEqual(0,CombatSimulation.GetImpactDelay(sepora,enemy));
            simulation.Step(.1f);Assert.AreEqual(1000,enemy.CurrentHealth);simulation.Step(.6f);Assert.AreEqual(994,enemy.CurrentHealth);
        }
        [Test] public void Trimol_BoulderHitsEveryCrossedEnemyIncludingAnEnemyEnteringItsPath()
        {
            var trimol=Unit(DocumentedRoster.CreateTrimol(),2,8,BoardSide.Blue,0,1);
            var near=Enemy(2,6);var far=Enemy(2,2);var entrant=Enemy(3,4);var outside=Enemy(4,4);
            Begin(trimol,near,far,entrant,outside);simulation.Step(.2f);
            Assert.Less(near.CurrentHealth,1000);Assert.IsTrue(board.TryRepositionUnit(entrant,board.GetCell(2,4)));
            simulation.Step(.4f);Assert.Less(far.CurrentHealth,1000);Assert.Less(entrant.CurrentHealth,1000);Assert.AreEqual(1000,outside.CurrentHealth);
        }
        [Test] public void Flo_ChargedSummonUsesFreeCellEvenWithBlockedFrontAndNoEnemyInRange()
        {
            var flo=Unit(DocumentedRoster.CreateFlo(),2,7,BoardSide.Blue,1);var enemy=Enemy(5,0);
            Assert.AreEqual(1,flo.BaseStats.AttackRange);Assert.IsTrue(board.TrySetCellBlocked(2,6,true));
            Begin(flo,enemy).Step(.6f);Assert.AreEqual(1,simulation.CombatOnlyUnits.Count);Assert.AreEqual(0,flo.Energy);
        }
        [Test] public void Sepora_KillEmitsOnePermanentDamageReward()
        {
            var sepora=Unit(Fast(DocumentedRoster.CreateSepora()),2,5,BoardSide.Blue,2); var enemy=Enemy(health:1); Begin(sepora,enemy);
            int reward=0; simulation.PermanentDamageEarned+=(unit,amount)=>reward+=amount; simulation.Step(.4f); Assert.AreEqual(1,reward);
        }
        [Test] public void Atong_SecondHitIsCritical()
        {
            var atong=Unit(Fast(DocumentedRoster.CreateAtong())); var enemy=Enemy(); Begin(atong,enemy);UntilHits(atong,2);
            Assert.AreEqual(975,enemy.CurrentHealth);
        }
        [Test] public void Flo_FullEnergySpawnsFlowerWithExactHealthAndNoPouchCopy()
        {
            var flo=Unit(DocumentedRoster.CreateFlo(),2,7,BoardSide.Blue,1); var enemy=Enemy(2,0); Begin(flo,enemy).Step(.6f);
            Assert.AreEqual(1,simulation.CombatOnlyUnits.Count); var flower=simulation.CombatOnlyUnits[0]; Assert.AreEqual(20,flower.CurrentHealth); Assert.AreEqual(new Vector2Int(2,6),flower.CurrentCell.Coordinates); Assert.IsTrue(flower.IsCombatSummon);
        }
        [Test] public void Flo_FlowerDeathHealsAdjacentAlly()
        {
            var allyDefinition=DocumentedRoster.CreateAtori();allyDefinition.AttackRange=9;allyDefinition.AttackInterval=100;
            var flo=Unit(DocumentedRoster.CreateFlo(),2,7,BoardSide.Blue,1); var ally=Unit(allyDefinition,1,6); var enemy=Enemy(5,0); Begin(flo,ally,enemy).Step(.6f);
            ally.ApplyDamage(25); var flower=simulation.CombatOnlyUnits[0]; flower.ApplyDamage(100); simulation.Step(.1f); Assert.AreEqual(ally.BaseStats.MaxHealth-5,ally.CurrentHealth);
        }
        [Test] public void Kayon_DoubleSummonsGainFourHealthAndSurviveSummoner()
        {
            var kayon=Unit(DocumentedRoster.CreateKayon(),2,7,BoardSide.Blue,0,2); var enemy=Enemy(5,0); Begin(kayon,enemy); kayon.AddEnergy(100); simulation.Step(.5f);
            Assert.AreEqual(2,simulation.CombatOnlyUnits.Count); Assert.IsTrue(simulation.CombatOnlyUnits.All(u=>u.BaseStats.MaxHealth==18));
            kayon.ApplyDamage(1000); simulation.Step(.1f); Assert.IsTrue(simulation.CombatOnlyUnits.All(u=>u.IsAlive)); Assert.IsFalse(simulation.Finished);
        }
        [Test] public void Gochan_BasicCrossDoesNotHitDiagonal()
        {
            var definition=Fast(DocumentedRoster.CreateGochan()); definition.TargetPolicy=TargetPolicy.LowestHealth;
            var gochan=Unit(definition,2,7); var center=Enemy(2,4,health:999); var side=Enemy(3,4); var diagonal=Enemy(3,3);
            Begin(gochan,center,side,diagonal).Step(.5f); Assert.AreEqual(989,center.CurrentHealth); Assert.AreEqual(990,side.CurrentHealth); Assert.AreEqual(1000,diagonal.CurrentHealth);
        }
        [Test] public void Gochan_SuperDealsTwentyInSquareAndBurnsFourTimes()
        {
            var definition=DocumentedRoster.CreateGochan(); definition.Damage=0; definition.AttackInterval=100; definition.AttackWindup=.1f;
            var gochan=Unit(definition,2,7,BoardSide.Blue,1); var target=Enemy(2,4); var diagonal=Enemy(3,3); Begin(gochan,target,diagonal); gochan.AddEnergy(100); simulation.Step(5f);
            Assert.AreEqual(972,target.CurrentHealth); Assert.AreEqual(972,diagonal.CurrentHealth);
        }
        [Test] public void Aky_DrainHealingUsesActualEnergyRemoved()
        {
            var aky=Unit(Fast(DocumentedRoster.CreateAky()),2,5,BoardSide.Blue,1); var enemy=Unit(DocumentedRoster.CreateBugaloo(),2,4,BoardSide.Red);
            Begin(aky,enemy); aky.ApplyDamage(20); enemy.AddEnergy(10); simulation.Step(.2f);
            // The target's first basic grants25energy before the impact: drain20 -> heal10.
            Assert.AreEqual(aky.BaseStats.MaxHealth-10,aky.CurrentHealth); Assert.AreEqual(15,enemy.Energy);
        }
        [Test] public void Tsu_FourthAttackRootsButLeavesAttacksEnabled()
        {
            var tsu=Unit(Fast(DocumentedRoster.CreateTsu())); var target=Enemy(); Begin(tsu,target);UntilHits(tsu,4);simulation.Step(.1f);
            Assert.IsTrue(target.IsRooted); Assert.IsFalse(target.IsStunned);
        }
        [Test] public void Tsu_DurationUpgradeExtendsRootWithoutCreatingADeathTrap()
        {
            var tsu=Unit(Fast(DocumentedRoster.CreateTsu(),0),2,5,BoardSide.Blue,0);
            var ally=Unit(Fast(DocumentedRoster.CreateAnuik(),0),0,9);var enemy=Enemy();
            Begin(tsu,ally,enemy);UntilHits(tsu,4);simulation.Step(.1f);Assert.IsTrue(enemy.IsRooted);
            tsu.ApplyDamage(1000);simulation.Step(2.5f);
            Assert.IsTrue(enemy.IsRooted,"The upgraded gum lasts beyond the base duration.");
            Assert.IsFalse(simulation.GroundEffects.Any(e=>e.Kind=="trap"));
            simulation.Step(3f);Assert.IsFalse(enemy.IsRooted);
        }
        [Test] public void Tokoro_StunHealingBeginsAtImpactTimeZero()
        {
            var tokoro=Unit(Fast(DocumentedRoster.CreateTokoro()),2,5,BoardSide.Blue,0); var enemy=Enemy(); Begin(tokoro,enemy); tokoro.ApplyDamage(30); tokoro.AddEnergy(100); simulation.Step(.6f);
            Assert.IsTrue(tokoro.IsStunned); Assert.AreEqual(tokoro.BaseStats.MaxHealth-22,tokoro.CurrentHealth);
            simulation.Step(1); Assert.AreEqual(tokoro.BaseStats.MaxHealth-14,tokoro.CurrentHealth);
        }
        [Test] public void Hymay_PullsAndBackstepsIntoFreeCells()
        {
            var hymay=Unit(Fast(DocumentedRoster.CreateHymay()),2,7,BoardSide.Blue,2); var enemy=Enemy(2,4); Begin(hymay,enemy); hymay.AddEnergy(100); simulation.Step(1.3f);
            Assert.AreEqual(new Vector2Int(2,6),enemy.CurrentCell.Coordinates); Assert.AreEqual(new Vector2Int(2,8),hymay.CurrentCell.Coordinates);
        }
        [Test] public void DocumentedMonster_OnlyOneUpgradePerMatchAndNeverChargedTwice()
        {
            var config=ScriptableObject.CreateInstance<MatchConfig>(); config.Units=DocumentedRoster.CreateAll(); config.RoundIncome=new[]{100};
            try
            {
                var pouch=new PouchState(config,1,"anuik",BoardSide.Blue); pouch.BeginPreparation(1);
                Assert.IsTrue(pouch.TryUpgradeMonster(2,out _)); int afterPurchase=pouch.Coins;
                Assert.IsFalse(pouch.TryUpgradeMonster(0,out _)); Assert.IsFalse(pouch.TryUpgradeMonster(1,out _)); Assert.AreEqual(afterPurchase,pouch.Coins);
                int coins=pouch.Coins; Assert.IsFalse(pouch.TryUpgradeMonster(2,out _)); Assert.AreEqual(coins,pouch.Coins); Assert.AreEqual(1,pouch.Monster.TrickCount);
                pouch.BeginPreparation(2); Assert.IsFalse(pouch.TryUpgradeMonster(0,out _));
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test] public void Atori_HardHeadReducesIncomingDamageByTwentyPercent()
        {
            var atori=Unit(DocumentedRoster.CreateAtori(),2,5,BoardSide.Blue,0); var enemy=Enemy(damage:10); Begin(atori,enemy).Step(.4f);
            Assert.AreEqual(atori.BaseStats.MaxHealth-8,atori.CurrentHealth);
        }
        [Test] public void Sepora_StealthReleasesExistingTargetAndExpires()
        {
            var sepora=Unit(DocumentedRoster.CreateSepora(),2,5,BoardSide.Blue,0); var ally=Unit(DocumentedRoster.CreateBlotan(),1,7); var enemy=Enemy();
            Begin(sepora,ally,enemy); sepora.ApplyDamage(sepora.BaseStats.MaxHealth/2+1); simulation.Step(.1f); Assert.IsTrue(sepora.IsInvisible);
            simulation.Step(2); Assert.IsFalse(sepora.IsInvisible);
        }
        [Test] public void Flo_EternalFlowerSurvivesDamageDuringFirstSecond()
        {
            var flo=Unit(DocumentedRoster.CreateFlo(),2,7,BoardSide.Blue,1,2);
            var enemyDefinition=new UnitDefinition{Id="flower-attacker",MaxHealth=1000,Damage=1,AttackRange=9,AttackInterval=.2f,AttackWindup=.1f,MoveInterval=.1f,TargetPolicy=TargetPolicy.LowestHealth};
            var enemy=Unit(enemyDefinition,2,5,BoardSide.Red); Begin(flo,enemy).Step(.6f);
            var trace = new List<string>();
            simulation.Attacked += (source, target, _) => { if (source == enemy) trace.Add("attack " + simulation.Elapsed + " -> " + target.UnitId); };
            simulation.Impacted += (source, target, damage) => { if (source == enemy) trace.Add("hit " + simulation.Elapsed + " -> " + target.UnitId + " for " + damage); };
            enemy.ResetForCombat(); // Reacquire the new flower after its extraction completes.
            simulation.Step(.9f);
            var flower=simulation.CombatOnlyUnits.First(); Assert.IsTrue(flower.IsAlive); Assert.AreEqual(1,flower.CurrentHealth);
            simulation.Step(.5f); Assert.IsFalse(flower.IsAlive, "elapsed=" + simulation.Elapsed + "; " + string.Join("; ", trace));
        }
        [Test] public void Jazar_RadiationSlowsSubsequentAttackCycles()
        {
            var jazar=Unit(Fast(DocumentedRoster.CreateJazar(),0),2,5,BoardSide.Blue,1);
            var definition=new UnitDefinition{Id="rapid",MaxHealth=1000,Damage=0,AttackRange=9,AttackInterval=.4f,AttackWindup=.1f};
            var enemy=Unit(definition,2,4,BoardSide.Red); Begin(jazar,enemy); var times=new List<float>(); simulation.Attacked+=(a,b,c)=>{if(a==enemy)times.Add(simulation.Elapsed);};
            simulation.Step(2); Assert.GreaterOrEqual(times.Count,3); Assert.GreaterOrEqual(times[2]-times[1],.79f);
        }
        [Test] public void Atong_OpeningImmunityBlocksStickyGum()
        {
            var atongDefinition=DocumentedRoster.CreateAtong(); atongDefinition.MaxHealth=1000; atongDefinition.Damage=0;
            var atong=Unit(atongDefinition,2,4,BoardSide.Red,2); var tsu=Unit(Fast(DocumentedRoster.CreateTsu())); Begin(tsu,atong).Step(1.1f);
            Assert.IsFalse(atong.IsRooted); Assert.IsFalse(atong.IsStunned);
        }
        [Test] public void Blotan_EarnsCurrencyWhileAliveAtExactIntervals()
        {
            var definition=DocumentedRoster.CreateBlotan(); definition.MaxHealth=1000; definition.Damage=1; definition.AttackRange=9;
            var blotan=Unit(definition,2,5,BoardSide.Blue,0); var enemy=Enemy(health:10000); Begin(blotan,enemy); int coins=0;
            simulation.CurrencyEarned+=(side,amount)=>coins+=amount;
            simulation.Step(9.9f); Assert.AreEqual(0,coins); simulation.Step(.1f); Assert.AreEqual(1,coins); simulation.Step(10); Assert.AreEqual(2,coins);
        }
        [Test] public void Blotan_SurvivalTheftFiresExactlyOnceAtRoundEnd()
        {
            var blotan=Unit(Fast(DocumentedRoster.CreateBlotan()),2,5,BoardSide.Blue,2); var enemy=Enemy(health:1); Begin(blotan,enemy); int stolen=0;
            simulation.CurrencyStolen+=(side,amount)=>stolen+=amount; simulation.Step(2); simulation.Step(2); Assert.AreEqual(2,stolen);
        }
        [Test] public void Trimol_OpeningBoulderPiercesSameColumnAndStuns()
        {
            var trimol=Unit(DocumentedRoster.CreateTrimol(),2,8,BoardSide.Blue,0,1,2); var near=Enemy(2,6); var far=Enemy(2,1); Begin(trimol,near,far).Step(.7f);
            Assert.Less(near.CurrentHealth,1000); Assert.Less(far.CurrentHealth,1000); Assert.IsTrue(far.IsStunned);
        }
        [Test] public void Tokoro_ConesAreUniqueAndConsumedByExistingOccupants()
        {
            var tokoro=Unit(Fast(DocumentedRoster.CreateTokoro()),2,5,BoardSide.Blue,1); var enemy=Enemy(); Begin(tokoro,enemy); tokoro.AddEnergy(100); simulation.Step(1f);
            Assert.IsTrue(simulation.GroundEffects.Any(e=>e.Kind=="cone")); Assert.IsFalse(simulation.GroundEffects.Any(e=>e.Cell==enemy.CurrentCell));
            simulation.Step(1); var cones=simulation.GroundEffects.Where(e=>e.Kind=="cone").ToList(); Assert.AreEqual(cones.Select(c=>c.Cell).Distinct().Count(),cones.Count);
        }
        [Test] public void Tokoro_FirstThreeSecondsRecoveryStopsAttackingAndHeals()
        {
            var definition=DocumentedRoster.CreateTokoro(); definition.MaxHealth=1000; definition.Damage=1; definition.EnergyMax=0;
            var tokoro=Unit(definition,2,5,BoardSide.Blue,2); var enemy=Enemy(health:10000); Begin(tokoro,enemy); int attacks=0; simulation.Attacked+=(a,b,c)=>{if(a==tokoro)attacks++;};
            tokoro.ApplyDamage(300); Assert.IsTrue(tokoro.IsSleeping);
            simulation.Step(1); Assert.AreEqual(0,attacks); Assert.AreEqual(760,tokoro.CurrentHealth);
            simulation.Step(1.9f); Assert.AreEqual(0,attacks); Assert.IsTrue(tokoro.IsSleeping);
            simulation.Step(.1f); Assert.IsFalse(tokoro.IsSleeping); Assert.AreEqual(880,tokoro.CurrentHealth);
            simulation.Step(27); Assert.IsFalse(tokoro.IsSleeping); Assert.Greater(attacks,0);
        }
        [Test] public void Hymay_AlreadyAdjacentTargetStillGetsBackstep()
        {
            var hymay=Unit(Fast(DocumentedRoster.CreateHymay()),2,5,BoardSide.Blue,2); var enemy=Enemy(); Begin(hymay,enemy); hymay.AddEnergy(100); simulation.Step(.6f);
            Assert.AreEqual(new Vector2Int(2,6),hymay.CurrentCell.Coordinates); Assert.AreEqual(new Vector2Int(2,4),enemy.CurrentCell.Coordinates);
        }
        [Test] public void CombatCurrencyNeverGoesNegativeWhenStealExceedsBalance()
        {
            var config=ScriptableObject.CreateInstance<MatchConfig>();
            try { var pouch=new PouchState(config,1,"bugaloo",BoardSide.Blue); Assert.AreEqual(0,pouch.AddCombatCoins(-2)); Assert.AreEqual(0,pouch.Coins); Assert.AreEqual(1,pouch.AddCombatCoins(1)); Assert.AreEqual(-1,pouch.AddCombatCoins(-2)); Assert.AreEqual(0,pouch.Coins); }
            finally { Object.DestroyImmediate(config); }
        }
    }
}
