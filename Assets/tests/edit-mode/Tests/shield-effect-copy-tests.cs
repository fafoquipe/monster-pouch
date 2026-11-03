using System;
using System.Linq;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ShieldEffectCopyTests
{
    static readonly string[] BonusEffects={"la-minera-fractura","zephalontropo-presion-calibrada","zephalontropo-camara-de-precision"};
    static Type Labels=>Type.GetType("EronyaEditorParameterLabels, Assembly-CSharp-Editor");

    [TestCase("guarda-veterano-dialogo-con-la-raiz")]
    [TestCase("protectora-del-mausoleo-memoria-de-la-roca")]
    [TestCase("protectora-del-mausoleo-memoria-compartida")]
    [TestCase("zephalontropo-presion-calibrada")]
    [TestCase("zephalontropo-camara-de-precision")]
    public void OnlyApprovedEffectsReturnWithEffectiveParameters(string effect)
    {
        Assert.IsFalse(AbilityBalance.IsRetiredArmorEffect(effect));
        var values=AbilityBalance.Resolve(new TrickDefinition{EffectId=effect});
        Assert.IsNotEmpty(values);
        Assert.IsFalse(values.Any(p=>p.Key=="armor"||p.Key=="armorPenetration"||p.Key=="armorReduction"));
        Assert.IsTrue(AbilityBalance.IsRetiredArmorEffect("padding"));
        Assert.IsTrue(AbilityBalance.IsRetiredArmorEffect("reinforced"));
    }

    [TestCase(true,"Daño extra contra escudos")]
    [TestCase(false,"Bonus damage against shields")]
    public void ShieldBonusIsAnIndependentEditablePointValue(bool spanish,string expected)
    {
        Assert.IsNotNull(Labels);
        foreach(string effect in BonusEffects)
        {
            Assert.AreEqual(expected,Labels.GetMethod("Label").Invoke(null,new object[]{effect,"shieldBonusDamage",spanish}));
            Assert.IsFalse((bool)Labels.GetMethod("IsFraction").Invoke(null,new object[]{effect,"shieldBonusDamage"}));
            foreach(float points in new[]{0f,7f,11f})
            {
                Assert.AreEqual(points,Labels.GetMethod("EditorValue").Invoke(null,new object[]{effect,"shieldBonusDamage",points,null}));
                Assert.AreEqual(points,Labels.GetMethod("StoredValue").Invoke(null,new object[]{effect,"shieldBonusDamage",points,null}));
            }
        }
    }

    [Test] public void GuardaReductionUsesPercentagePointsAndKeepsTheExistingClock()
    {
        string effect="guarda-veterano-dialogo-con-la-raiz";
        var values=AbilityBalance.Resolve(new TrickDefinition{EffectId=effect,Parameters=new[]{P("stationaryDuration",3),P("reduction",13)}});
        Assert.AreEqual(3,values.Single(p=>p.Key=="stationaryDuration").Value);
        Assert.AreEqual(13,values.Single(p=>p.Key=="reduction").Value);
        Assert.IsFalse((bool)Labels.GetMethod("IsFraction").Invoke(null,new object[]{effect,"reduction"}));
        Assert.AreEqual(13f,Labels.GetMethod("EditorValue").Invoke(null,new object[]{effect,"reduction",13f,null}));
        Assert.IsFalse(values.Any(p=>p.Key=="duration"),"The stationary condition is not a second timed buff.");
    }

    [Test] public void SixRealCatalogCardsReflectEditedValuesWithoutMutatingHumanAssets()
    {
        Check("guarda-veterano","guarda-veterano-dialogo-con-la-raiz",new[]{P("stationaryDuration",3),P("reduction",13)},"3","13%");
        Check("protectora-del-mausoleo","protectora-del-mausoleo-memoria-de-la-roca",new[]{P("shield",18),P("duration",6)},"18","6");
        Check("protectora-del-mausoleo","protectora-del-mausoleo-memoria-compartida",new[]{P("targetCount",3),P("range",5)},"3","5");
        Check("la-minera","la-minera-fractura",new[]{P("damage",23),P("shieldBonusDamage",11),P("range",1)},"23","11");
        Check("zephalontropo","zephalontropo-presion-calibrada",new[]{P("shieldBonusDamage",7)},"7");
        Check("zephalontropo","zephalontropo-camara-de-precision",new[]{P("shieldBonusDamage",0)},"0");
    }
    static AbilityParameter P(string key,float value)=>new AbilityParameter{Key=key,Value=value};
    static void Check(string id,string effect,AbilityParameter[] edits,params string[] expectedNumbers)
    {
        var source=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/"+id+"/Character");Assert.IsNotNull(source);
        string before=EditorJsonUtility.ToJson(source);
        var owner=JsonUtility.FromJson<UnitDefinition>(JsonUtility.ToJson(source.CreateRuntimeDefinition()));
        var ability=new[]{owner.BaseAbility,owner.PassiveAbility}.Concat(owner.Tricks).Single(a=>a!=null&&(a.EffectId??a.Id)==effect);
        ability.Parameters=ability.Parameters.Where(p=>!edits.Any(e=>e.Key==p.Key)).Concat(edits).ToArray();
        foreach(bool spanish in new[]{true,false})
        {
            string text=GameText.AbilityDescription(ability,owner,spanish);
            foreach(string number in expectedNumbers)StringAssert.Contains(number,text,effect);
            Assert.IsFalse(text.Contains("{")||text.Contains("retirado")||text.Contains("Retired"),text);
            if(BonusEffects.Contains(effect))
            {
                owner.Damage+=19;
                Assert.AreEqual(text,GameText.AbilityDescription(ability,owner,spanish),"Editing basic damage must not recalculate a stored bonus.");
            }
        }
        Assert.AreEqual(before,EditorJsonUtility.ToJson(source));
    }
}
