using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class AbilitySummaryDescriptionTests
    {
        [TestCase(true)] [TestCase(false)]
        public void EveryCurrentCharacterHasConciseSummariesWithoutTechnicalFlightDetails(bool spanish)
        {
            var characters=Resources.LoadAll<CharacterDefinition>("MonsterPouch/Characters");
            Assert.IsNotEmpty(characters,"Load the real resource catalogue.");
            var registered=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").Characters;
            CollectionAssert.IsSubsetOf(registered.Select(c=>c.Id),characters.Select(c=>c.Id));
            Assert.AreEqual(characters.Length,characters.Select(c=>c.Id).Distinct().Count(),"Stable character IDs must be unique.");
            int abilities=0;
            foreach(var character in characters)
            {
                var unit=character.CreateRuntimeDefinition();
                foreach(var ability in new[]{unit.BaseAbility,unit.PassiveAbility}.Concat(unit.Tricks).Where(a=>a!=null))
                {
                    string before=JsonUtility.ToJson(ability);
                    Assert.IsTrue(AbilitySummaryDescriptions.HasSummary(ability.EffectId),character.General.DisplayName+" / "+ability.Id);
                    string text=GameText.AbilityDescription(ability,unit,spanish);
                    Assert.IsNotEmpty(text,ability.Id);
                    Assert.LessOrEqual(text.Length,260,ability.Id+": keep the card concise.");
                    Assert.That(text,Does.Not.Contain("{").And.Not.Contain("}"),ability.Id);
                    string lower=text.ToLowerInvariant();
                    foreach(string technical in new[]{"velocidad de vuelo","velocidad del proyectil","projectile speed","flight speed","casillas/s","tiles/s","castduration","recoveryduration"})
                        Assert.That(lower,Does.Not.Contain(technical),ability.Id);
                    Assert.AreEqual(before,JsonUtility.ToJson(ability),"Presentation must not modify balance: "+ability.Id);
                    abilities++;
                }
            }
            Assert.GreaterOrEqual(abilities,registered.Sum(c=>new[]{c.CreateRuntimeDefinition().BaseAbility,c.CreateRuntimeDefinition().PassiveAbility}.Concat(c.CreateRuntimeDefinition().Tricks).Count(a=>a!=null)));
        }

        [TestCase(true)] [TestCase(false)]
        public void MartioLiveParametersAndNamesNeverFallBackToStaleSavedCopy(bool spanish)
        {
            var unit=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/martio/Character").CreateRuntimeDefinition();
            var all=new[]{unit.BaseAbility}.Concat(unit.Tricks).ToArray();
            var es=new[]{"Sistema planetario","Onda expansiva","Perforación","Abanico planetario"};
            var en=new[]{"Planetary System","Shock Wave","Piercing","Planetary Fan"};
            for(int i=0;i<all.Length;i++)
            {
                var ability=all[i];ability.Description="STALE SAVED COPY 98765";ability.Name=spanish?en[i]:es[i];
                ability.Parameters=AbilityBalance.Resolve(ability).Select((p,j)=>new AbilityParameter{Key=p.Key,Value=31+j*6}).ToArray();
                string before=JsonUtility.ToJson(ability),text=GameText.AbilityDescription(ability,unit,spanish);
                Assert.That(text,Does.Not.Contain("STALE").And.Not.Contain("98765").And.Not.Contain("{"));
                foreach(var parameter in ability.Parameters)
                    Assert.That(text,Does.Contain(parameter.Value.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)),ability.EffectId+":"+parameter.Key);
                Assert.AreEqual((spanish?es:en)[i],GameText.AbilityName(ability,spanish));
                Assert.AreEqual(before,JsonUtility.ToJson(ability));
            }
        }

        [TestCase(true)] [TestCase(false)]
        public void FlowerInvulnerabilityUsesAbsoluteLifeInsteadOfSubtractingEditedBaseLife(bool spanish)
        {
            var unit=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/flo/Character").CreateRuntimeDefinition();
            unit.BaseAbility.Parameters=new[]{new AbilityParameter{Key="health",Value=50}};
            unit.Tricks[2].Parameters=new[]{new AbilityParameter{Key="health",Value=1},new AbilityParameter{Key="duration",Value=2}};
            string text=GameText.AbilityDescription(unit.Tricks[2],unit,spanish);
            Assert.That(text,Does.Contain(spanish?"tiene 1 de vida":"has 1 health").And.Contain(spanish?"INVULNERABILIDAD: 2 s":"INVULNERABILITY: 2 s"));
            Assert.That(text,Does.Not.Contain("49").And.Not.Contain("50"));
            unit.Tricks[2].Parameters[0].Value=3;
            Assert.That(GameText.AbilityDescription(unit.Tricks[2],unit,spanish),Does.Contain(spanish?"tiene 3 de vida":"has 3 health"));
        }

        [TestCase(true)] [TestCase(false)]
        public void SummaryNumbersFollowLiveStatsAndKeepBasicAttackPercentagesClear(bool spanish)
        {
            var flo=DocumentedRoster.CreateFlo();
            flo.BaseAbility.Parameters=new[]{new AbilityParameter{Key="heal",Value=12}};
            flo.Tricks[0].Parameters=new[]{new AbilityParameter{Key="heal",Value=35}};
            Assert.That(GameText.AbilityDescription(flo.Tricks[0],flo,spanish),Does.Contain("35").And.Not.Contain("+23"));
            var tokoro=DocumentedRoster.CreateTokoro();
            tokoro.BaseAbility.Parameters=new[]{new AbilityParameter{Key="damageOverride",Value=27}};
            Assert.That(GameText.AbilityDescription(tokoro.BaseAbility,tokoro,spanish),Does.Contain(spanish?"Daño: 27":"Damage: 27"));
            tokoro.BaseAbility.Parameters[0].Value=-1;
            Assert.That(GameText.AbilityDescription(tokoro.BaseAbility,tokoro,spanish),Does.Contain(spanish?"Daño: 10":"Damage: 10"));
            var atori=DocumentedRoster.CreateAtori();
            atori.Tricks[1].Parameters=new[]{new AbilityParameter{Key="speedPercent",Value=25}};
            Assert.That(GameText.AbilityDescription(atori.Tricks[1],atori,spanish),Does.Contain("+25 %"));
        }

        [Test]
        public void AutomaticHeadbuttSummaryMatchesRealImpactEvenWithDifferentBasicDamage()
        {
            var root=new GameObject("headbutt-summary-combat");
            CombatSimulation simulation=null;
            try
            {
                var board=root.AddComponent<BoardManager>();board.BuildBoard();
                var definition=DocumentedRoster.CreateTokoro();
                definition.Damage=99;definition.MaxHealth=1000;definition.AttackInterval=100;definition.MoveInterval=100;
                definition.EnergyPerAttack=definition.EnergyOnDamage=definition.EnergyPerSecond=0;
                definition.BaseAbility.Parameters=new[]{new AbilityParameter{Key="damageOverride",Value=-1},new AbilityParameter{Key="castDuration",Value=0}};
                BattleUnit Spawn(UnitDefinition unit,int y,BoardSide side)
                {
                    var go=new GameObject(unit.Id);go.transform.SetParent(root.transform);
                    var actor=go.AddComponent<WhelpUnit>();actor.Initialize(unit.Id,side,UnitStats.FromDefinition(unit));
                    Assert.IsTrue(board.TryOccupyCell(actor,2,y));return actor;
                }
                var caster=Spawn(definition,5,BoardSide.Blue);
                var target=Spawn(new UnitDefinition{Id="summary-target",MaxHealth=1000,Damage=0,AttackRange=9,AttackInterval=100,MoveInterval=100},4,BoardSide.Red);
                simulation=new CombatSimulation(board);simulation.Begin(new[]{caster,target});
                caster.AddEnergy(100);simulation.Step(.1f);
                Assert.AreEqual(990,target.CurrentHealth,"Automatic headbutt uses 10 fixed damage, not the 99-point basic attack.");
                Assert.That(GameText.AbilityDescription(definition.BaseAbility,definition,true),Does.Contain("Daño: 10"));
            }
            finally {simulation?.Stop();Object.DestroyImmediate(root);}
        }

        [TestCase(true)] [TestCase(false)]
        public void SquareAreaUsesActualCombatGeometryForAnEvenConfiguredWidth(bool spanish)
        {
            var unit=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/kazuru/Character").CreateRuntimeDefinition();
            unit.BaseAbility.Parameters=new[]{new AbilityParameter{Key="width",Value=10}};
            var cells=CombatSimulation.PokrakBubbleCells(Vector2Int.zero,10);
            Assert.AreEqual(121,cells.Count,"Combat centers an even width as an 11-by-11 square.");
            Assert.That(GameText.AbilityDescription(unit.BaseAbility,unit,spanish),Does.Contain("11×11").And.Not.Contain("10×10"));
        }

        [TestCase(true)] [TestCase(false)]
        public void MarkSummaryFollowsConfiguredFrontDistanceAndStatesNextPreparationRestriction(bool spanish)
        {
            var unit=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/tauris/Character").CreateRuntimeDefinition();
            unit.Tricks[0].Parameters=new[]{new AbilityParameter{Key="energy",Value=5},new AbilityParameter{Key="range",Value=4}};
            string text=GameText.AbilityDescription(unit.Tricks[0],unit,spanish);
            Assert.That(text,Does.Contain(spanish?"4 casillas delante":"4 tiles ahead").And.Contain("+5"));
            Assert.That(text,Does.Contain(spanish?"siguiente preparación":"next preparation"));
        }

        [TestCase(true)] [TestCase(false)]
        public void FlightKeepsItsGameplayProtectionAndLiveDuration(bool spanish)
        {
            var unit=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/aky/Character").CreateRuntimeDefinition();
            string text=GameText.AbilityDescription(unit.Tricks[0],unit,spanish);
            var duration=AbilityBalance.Resolve(unit.Tricks[0]).Single(p=>p.Key=="duration").Value.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(text,Does.Contain(duration+" s"));
            Assert.That(text,Does.Contain(spanish?"No puede ser atacado":"Cannot be targeted"));
        }
    }
}
