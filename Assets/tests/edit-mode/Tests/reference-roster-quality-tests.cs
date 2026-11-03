using System;
using System.Linq;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class ReferenceRosterQualityTests
    {
        static CharacterDefinition Character(string id)=>AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/resources/MonsterPouch/Characters/"+id+"/Character.asset");
        static readonly string[] Ids={"aky","anuik","atong","atori","blotan","bugaloo","bugui","flo","gochan","hymay","jazar","kayon","popow","sepora","stein","tauris","tokoro","trimol","tsu"};
        [Test] public void AllNineteenCharactersUseTheirOwnCleanSpritesInEveryAnimationAndDirection()
        {
            foreach(var id in Ids)
            {
                var art=Character(id).Visuals;
                Assert.GreaterOrEqual(art.WorldWidth*art.DisplayScale*1.2f,1.34f,id+" became too small");
                for(int d=0;d<8;d++)
                {
                    var c=art.Animation((UnitFacing)d);
                    foreach(var seq in new[]{c.Idle,c.Move,c.HopLeft,c.HopRight,c.Attack,c.Special,c.Death})
                    {
                        Assert.IsNotEmpty(seq,id+" direction "+d);
                        foreach(var sprite in seq)
                        {
                            Assert.IsNotNull(sprite,id+" direction "+d);
                            StringAssert.EndsWith(id=="stein"?"/stein-scientist.png":id=="blotan"?"/blotan-moonken.png":"/"+id+"-clean-v3.png",AssetDatabase.GetAssetPath(sprite));
                        }
                    }
                }
            }
        }
        [Test] public void AtongIsTheLargestAndItsVisibleWidthIsAtLeastEightyPercentLarger()
        {
            var a=Character("atong").Visuals;float width=a.WorldWidth*a.DisplayScale*1.2f;
            Assert.That(width,Is.EqualTo(2.15f).Within(.01f));
            Assert.Greater(width,.77f*1.22f*1.2f*1.8f);
            Assert.IsTrue(Ids.All(id=>{var v=Character(id).Visuals;return width>=v.WorldWidth*v.DisplayScale*1.2f-.001f;}));
        }
        [Test] public void SteinHasStrongAttackAndAVisiblyCollapsedDefeat()
        {
            var art=Character("stein").Visuals;
            Assert.AreEqual(UnitTravelMotion.AlternatingHop,art.TravelMotion);
            for(int d=0;d<8;d++)
            {
                var c=art.Animation((UnitFacing)d);
                Assert.GreaterOrEqual(c.AttackLungeDistance,.20f);
                Assert.GreaterOrEqual(c.SpecialPunchScale,.12f);
                Assert.Less(c.Death.Last().rect.height,c.Idle[0].rect.height*.78f,"Defeat must read as fallen in direction "+d);
            }
        }
    }
}
