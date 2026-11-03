using System;
using System.Globalization;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Local;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class BalanceInspectorLayoutTests
    {
        static Type Inspector=>Type.GetType("MonsterPouchBalanceInspector, Assembly-CSharp-Editor");
        static object Call(string method,params object[] args)=>Inspector.GetMethod(method).Invoke(null,args);
        static string Summary(TrickDefinition ability,UnitDefinition owner,float fallback=99)
            =>(string)Call("AnimationSummary",ability,owner,fallback);
        static void Set(TrickDefinition ability,string key,float value)
            =>ability.Parameters=ability.Parameters.Where(p=>p.Key!=key).Concat(new[]{new AbilityParameter{Key=key,Value=value}}).ToArray();

        [TestCase("korran",true,"Eronya leader","Líder de Eronya")]
        [TestCase("golem-tectonico",false,"Erony","Erony")]
        [TestCase("tokoro",true,"Monster","Monster")]
        [TestCase("martio",false,"Whelp","Whelp")]
        public void CatalogNamesDistinguishHumansAndPieces(string id,bool hero,string english,string spanish)
        {
            Assert.AreEqual(english,CharacterCatalogLabels.Kind(id,hero,false));
            Assert.AreEqual(spanish,CharacterCatalogLabels.Kind(id,hero,true));
            Assert.AreEqual(hero?"Heroes":"Pieces",CharacterCatalogLabels.Group(hero,false));
            Assert.AreEqual(hero?"Héroes":"Piezas",CharacterCatalogLabels.Group(hero,true));
        }
        [Test] public void SelectorGroupsAndSortsWithoutMovingAnySavedCharacter()
        {
            var config=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
            try
            {
                config.Units=config.Units.Reverse().ToArray();
                var original=config.Units.Select(u=>u.Id).ToArray();
                using(var serialized=new SerializedObject(config))
                {
                    var units=serialized.FindProperty("Units");
                    var order=(int[])Call("CharacterDisplayOrder",units);
                    Assert.AreEqual(config.Units.Length,order.Distinct().Count());
                    var displayed=order.Select(i=>config.Units[i]).ToArray();
                    bool seenWhelp=false;
                    for(int i=0;i<displayed.Length;i++)
                    {
                        if(!displayed[i].IsMonster)seenWhelp=true;
                        else Assert.IsFalse(seenWhelp,"All Monsters must precede the Whelps.");
                        if(i>0&&displayed[i-1].IsMonster==displayed[i].IsMonster)
                            Assert.LessOrEqual(CultureInfo.GetCultureInfo("es").CompareInfo.Compare(
                                displayed[i-1].DisplayName,displayed[i].DisplayName,CompareOptions.IgnoreCase),0,"Sort by the current visible name.");
                    }
                    bool spanish=EditorPrefs.GetBool("MonsterPouch.Balance.Spanish",false);
                    foreach(int index in order)
                    {
                        var definition=config.Units[index];
                        string group=definition.IsMonster?(spanish?"Héroes":"Heroes"):(spanish?"Piezas":"Pieces");
                        string kind=EronyaRoster.IsId(definition.Id)?(definition.IsMonster?(spanish?"Líder de Eronya":"Eronya leader"):"Erony"):(definition.IsMonster?"Monster":"Whelp");
                        Assert.AreEqual(group+"/"+kind+"/"+definition.DisplayName,
                            (string)Call("CharacterMenuLabel",units.GetArrayElementAtIndex(index)));
                    }
                    Assert.IsFalse(serialized.hasModifiedProperties);
                    CollectionAssert.AreEqual(original,config.Units.Select(u=>u.Id).ToArray());
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(config);}
        }
        [Test] public void EditorDiscoversAllTwentyImportedEronyaAssetsWithoutRuntimeActivation()
        {
            var config=ScriptableObject.CreateInstance<MatchConfig>();
            try
            {
                config.Characters=Array.Empty<CharacterDefinition>();
                string before=EditorJsonUtility.ToJson(config);
                var editor=Type.GetType("MonsterPouchBalanceEditor, Assembly-CSharp-Editor");
                var found=(CharacterDefinition[])editor.GetMethod("FindEditableCharacters").Invoke(null,new object[]{config});
                Assert.AreEqual(found.Length,found.Distinct().Count(),"Configured/imported assets must not appear twice.");
                foreach(var definition in EronyaRoster.CreateAll())
                {
                    var asset=found.Single(c=>c.Id==definition.Id);
                    Assert.IsNotNull(asset.Attack,definition.Id);
                    Assert.IsNotNull(asset.Ability,definition.Id);
                    Assert.AreEqual(3,asset.Upgrades.Length,definition.Id);
                    Assert.IsTrue(asset.Upgrades.All(u=>u!=null),definition.Id);
                    Assert.AreEqual(definition.IsMonster,asset.General.IsMonster,definition.Id);
                }
                Assert.AreEqual(before,EditorJsonUtility.ToJson(config),"Discovery must not activate characters or rewrite values.");
            }
            finally{UnityEngine.Object.DestroyImmediate(config);}
        }
        [Test] public void OnlyAbilitiesWithTheirOwnAnimationHaveAnAnimationSection()
        {
            foreach(var unit in DocumentedRoster.CreateAll())
            foreach(var ability in new[]{unit.BaseAbility}.Concat(unit.Tricks))
            {
                if(ability==null)continue;
                bool shown=(bool)Call("HasOwnAnimation",ability);
                if(ability.EffectId.StartsWith("tsu-")||ability.EffectId=="jazar-radiation"||
                    !AbilityBalance.Resolve(ability).Any(p=>(bool)Call("IsAnimationParameter",ability.EffectId,p.Key))&&ability.EffectId!="anuik-revive")
                    Assert.IsFalse(shown,ability.EffectId);
            }
            Assert.IsTrue((bool)Call("HasOwnAnimation",DocumentedRoster.CreateTokoro().BaseAbility));
            Assert.IsTrue((bool)Call("HasOwnAnimation",DocumentedRoster.CreateStein().BaseAbility));
            Assert.IsTrue((bool)Call("HasOwnAnimation",DocumentedRoster.CreateSepora().BaseAbility));
        }
        [Test] public void PhaseSummaryMatchesRuntimeVisualDurationAfterEditing()
        {
            var tokoro=DocumentedRoster.CreateTokoro();
            Set(tokoro.BaseAbility,"castDuration",.65f);Set(tokoro.BaseAbility,"holdDuration",1);Set(tokoro.BaseAbility,"recoveryDuration",.2f);
            float actual=AbilityTiming.Duration(UnitStats.FromDefinition(tokoro),"tokoro-super",99);
            Assert.That(actual,Is.EqualTo(1.9f).Within(.0001f));
            Assert.That(Summary(tokoro.BaseAbility,tokoro),Does.Contain("total: 1,9 s").And.Contain("Preparación 0,7 s").And.Not.Contain("99"));
        }
        [Test] public void PopowShowsItsActualMovementTimeInsteadOfTheGenericClip()
        {
            var popow=DocumentedRoster.CreatePopow();Set(popow.BaseAbility,"travelDuration",.75f);Set(popow.BaseAbility,"castDuration",.2f);
            Assert.That(Summary(popow.BaseAbility,popow),Does.Contain("Recorrido visual: 0,8 s").And.Contain("Total hasta llegar: 1 s").And.Not.Contain("99"));
            Assert.That(Summary(popow.Tricks[0],popow),Does.Contain("Comparte").And.Contain("Recorrido visual: 0,8 s"));
        }
        [Test] public void ReactiveDurationIsPerAbilityAndResolvesItsZeroFallback()
        {
            var blotan=DocumentedRoster.CreateBlotan();
            Assert.That(Summary(blotan.BaseAbility,blotan,.48f),Does.Contain("0,48 s").And.Contain("respaldo"));
            Set(blotan.BaseAbility,"animationDuration",1.6f);
            Assert.That(Summary(blotan.BaseAbility,blotan,.48f),Does.Contain("1,6 s").And.Not.Contain("respaldo"));
            Assert.AreEqual(1.6f,AbilityTiming.Duration(UnitStats.FromDefinition(blotan),"blotan-income",.48f));
        }
        [Test] public void ReviveAndConeAnimationsShowTheirOwnTimers()
        {
            var anuik=DocumentedRoster.CreateAnuik();anuik.ReviveDelay=1.25f;
            Assert.That(Summary(anuik.Tricks[0],anuik),Does.Contain("1,3 s").And.Contain("Comparte"));
            var tokoro=DocumentedRoster.CreateTokoro();Set(tokoro.Tricks[1],"travelDuration",.8f);
            Assert.That(Summary(tokoro.Tricks[1],tokoro),Does.Contain("helados: 0,8 s"));
        }
        [TestCase("popow-dash","travelDuration",true)]
        [TestCase("popow-dash","duration",false)]
        [TestCase("aky-opening-flight","duration",true)]
        [TestCase("stein-stun","duration",false)]
        [TestCase("tokoro-headbutt","holdDuration",true)]
        public void AnimationFieldsAreSeparatedFromStatusEffectDuration(string ability,string key,bool expected)
            =>Assert.AreEqual(expected,(bool)Call("IsAnimationParameter",ability,key));
    }
}
