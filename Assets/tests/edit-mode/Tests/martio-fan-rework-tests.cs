using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class MartioFanReworkTests
    {
        sealed class World : IMartioOccupancy
        {
            public readonly Dictionary<ProjectileGridPoint,string> Occupants = new Dictionary<ProjectileGridPoint,string>();
            public string EnemyOccupying(ProjectileGridPoint cell) => Occupants.TryGetValue(cell,out var id)?id:null;
        }
        static ProjectileGridPoint P(int x,int y)=>new ProjectileGridPoint(x,y);
        static MartioProjectileSimulation Flight(bool explosion=false,bool piercing=false,int range=3)
            =>new MartioProjectileSimulation(new MartioPrototypeConfig(),P(2,8),P(0,-1),range,1,explosion,piercing,true,
                MartioRepeatHitPolicy.OncePerEnemy,MartioCornerPolicy.InteriorOnly,new MartioFlightBounds(6,10));

        [Test] public void LegacyUpgradeParametersResolveToFanWithoutChangingSavedCostOrIdentity()
        {
            var old=new TrickDefinition {Id="martio-moons",EffectId="martio-moons",Cost=4,
                Parameters=new[]{new AbilityParameter{Key="firstMoonDamage",Value=99},new AbilityParameter{Key="secondMoonDamage",Value=99}}};
            var resolved=AbilityBalance.Resolve(old);
            CollectionAssert.AreEquivalent(new[]{"lateralTravelRange","fanAngleDegrees"},resolved.Select(p=>p.Key));
            Assert.AreEqual(3,AbilityBalance.Value(old,"lateralTravelRange"));
            Assert.AreEqual(30,AbilityBalance.Value(old,"fanAngleDegrees"));
            Assert.AreEqual(4,old.Cost); Assert.AreEqual("martio-moons",old.Id);
            Assert.AreEqual(2,old.Parameters.Length); // Resolving does not rewrite user saves.
        }

        [TestCase(true)] [TestCase(false)]
        public void EditedFanValuesReachEditorAndLocalizedDescription(bool spanish)
        {
            var effect=new TrickDefinition {Id="martio-moons",EffectId="martio-moons",Parameters=new[]{
                new AbilityParameter{Key="lateralTravelRange",Value=4.6f},new AbilityParameter{Key="fanAngleDegrees",Value=24.6f}}};
            Assert.AreEqual(5,AbilityBalance.Value(effect,"lateralTravelRange"));
            Assert.AreEqual(25,AbilityBalance.Value(effect,"fanAngleDegrees"));
            Assert.AreEqual(25,AbilityBalance.ParameterEditorValue("martio-moons","fanAngleDegrees",24.6f,null));
            Assert.AreEqual(25,AbilityBalance.ParameterStoredValue("martio-moons","fanAngleDegrees",24.6f,null));
            string text=AbilitySummaryDescriptions.Describe(effect,null,spanish);
            StringAssert.Contains("25",text);StringAssert.Contains("5",text);
            StringAssert.DoesNotContain("{",text);
            StringAssert.Contains(spanish?"casillas":"tiles",text);
            StringAssert.DoesNotContain(spanish?"Daño de las lunas: 6":"Moon damage: 6",text);
        }

        [Test] public void NonfiniteLegacyValuesFallBackAndDiscreteTiesMatchRuntime()
        {
            var effect=new TrickDefinition {EffectId="martio-moons",Parameters=new[]{
                new AbilityParameter{Key="lateralTravelRange",Value=float.NaN},new AbilityParameter{Key="fanAngleDegrees",Value=float.PositiveInfinity}}};
            Assert.AreEqual(3,AbilityBalance.Value(effect,"lateralTravelRange"));Assert.AreEqual(30,AbilityBalance.Value(effect,"fanAngleDegrees"));
            Assert.AreEqual(4,AbilityBalance.ParameterStoredValue("martio-moons","lateralTravelRange",4.5f,null));
            Assert.AreEqual(26,AbilityBalance.ParameterStoredValue("martio-moons","fanAngleDegrees",25.5f,null));
            Assert.Throws<ArgumentException>(()=>MartioBalanceValues.Normalize("martio-moons","fanAngleDegrees",float.NaN));
        }

        [TestCase(0,-1)] [TestCase(0,1)] [TestCase(1,0)] [TestCase(-1,0)] [TestCase(2,-3)] [TestCase(-2,3)]
        public void FanUsesActualReleaseHeadingAndIndependentRanges(int x,int y)
        {
            var groups=MartioFanPattern.Groups(P(x,y),9,true);
            Assert.AreEqual(3,groups.Count);Assert.AreEqual(9,groups[0].Range);Assert.AreEqual(P(x,y),groups[0].Heading);
            foreach(var group in groups.Skip(1)) {
                double angle=Math.Atan2((double)x*group.Heading.Y-(double)y*group.Heading.X,
                    (double)x*group.Heading.X+(double)y*group.Heading.Y)*180/Math.PI;
                Assert.AreEqual(group.Side*30,angle,.002);Assert.AreEqual(3,group.Range);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void ExpiryHasExactlyThreeVisualEventsAndNoDamageEvenWithUpgradeOne(bool explosion)
        {
            var flight=Flight(explosion);var world=new World();world.Occupants[P(3,5)]="adjacent-to-end";
            var damage=new List<MartioDamageEvent>();var events=new List<MartioFlightEvent>();
            flight.Advance(10,world,damage.Add,events.Add);flight.Advance(10,world,damage.Add,events.Add);
            Assert.IsEmpty(damage);Assert.AreEqual(3,events.Count);Assert.IsTrue(events.All(e=>e.Kind==MartioFlightEventKind.RangeEnd));
        }

        [Test] public void NonPiercingImpactIsAlreadyTheTerminalEventAndDoesNotExplodeTwice()
        {
            var flight=Flight(true);var world=new World();world.Occupants[P(2,7)]="victim";world.Occupants[P(3,7)]="neighbor";
            var damage=new List<MartioDamageEvent>();var events=new List<MartioFlightEvent>();
            flight.Advance(10,world,damage.Add,events.Add);flight.Advance(10,world,damage.Add,events.Add);
            Assert.AreEqual(3,events.Count);Assert.IsTrue(events.All(e=>e.Kind==MartioFlightEventKind.Impact&&e.StopsProjectile));
            Assert.AreEqual(12,damage.Where(e=>e.EnemyId=="victim").Sum(e=>e.Damage));
            Assert.AreEqual(12,damage.Where(e=>e.EnemyId=="neighbor").Sum(e=>e.Damage));
        }

        [Test] public void PiercingHasContactEventsThenOnlyOneTerminalPerLane()
        {
            var flight=Flight(true,true);var world=new World();world.Occupants[P(2,7)]="victim";
            var damage=new List<MartioDamageEvent>();var events=new List<MartioFlightEvent>();
            for(int i=0;i<100;i++)flight.Advance(.1,world,damage.Add,events.Add);
            Assert.AreEqual(3,events.Count(e=>e.Kind==MartioFlightEventKind.Impact));
            Assert.AreEqual(3,events.Count(e=>e.StopsProjectile));
            Assert.AreEqual(12,damage.Where(e=>e.Kind==MartioDamageKind.Impact).Sum(e=>e.Damage));
        }

        [Test] public void EdgeExitStopsAtVisibleBoundaryWithoutReenteringOrHittingOutside()
        {
            var f=new MartioProjectileSimulation(new MartioPrototypeConfig(),P(0,0),P(-1,-1),7,4.5,false,true,false,
                MartioRepeatHitPolicy.OncePerEnemy,MartioCornerPolicy.InteriorOnly,new MartioFlightBounds(6,10));
            var world=new World();world.Occupants[P(-1,-1)]="outside";
            var damage=new List<MartioDamageEvent>();var events=new List<MartioFlightEvent>();
            f.Advance(0,world,damage.Add,events.Add);Assert.IsEmpty(events);
            f.Advance(10,world,damage.Add,events.Add);
            Assert.IsEmpty(damage);Assert.AreEqual(3,events.Count);
            Assert.IsTrue(events.All(e=>e.Kind==MartioFlightEventKind.BoardExit&&e.Position.X>=-.5&&e.Position.Y>=-.5));
        }
    }
}
