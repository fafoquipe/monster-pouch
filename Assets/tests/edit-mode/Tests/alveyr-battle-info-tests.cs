using System.Linq;
using System.Text.RegularExpressions;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class AlveyrBattleInfoTests
{
    [TestCase(true,"Vida: 191/200\nDaño: 10/7\nAtaques por segundo: 0.52/0.5\nEnergía: 3/20")]
    [TestCase(false,"Health: 191/200\nDamage: 10/7\nAttacks per second: 0.52/0.5\nEnergy: 3/20")]
    public void FourRowsKeepCurrentValuesAndRoundBaselines(bool spanish,string expected)
        =>Assert.AreEqual(expected,BattleInfoText.Rows(191m,200,10m,7,.52m,.5f,3,20,spanish));

    [Test] public void FractionalValuesAreReadableAndEmptyEnergyCapacityIsSafe()
        =>Assert.AreEqual("Vida: 0.25/1\nDaño: 2.75/2\nAtaques por segundo: 1.33/1\nEnergía: 0/0",
            BattleInfoText.Rows(.25m,1,2.75m,2,4m/3m,1,0,0,true));

    [Test] public void AlveyrCatalogUsesCurrentParametersWithoutChangingHumanAssets()
    {
        var source=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/alveyr/Character");
        Assert.IsNotNull(source);Assert.IsNotNull(source.Ability);
        string characterBefore=EditorJsonUtility.ToJson(source),abilityBefore=EditorJsonUtility.ToJson(source.Ability);
        // JSON copies are detached from every ScriptableObject, including unsaved human edits.
        var owner=JsonUtility.FromJson<UnitDefinition>(JsonUtility.ToJson(source.CreateRuntimeDefinition()));
        var ability=owner.BaseAbility;
        ability.Parameters=new[]{P("duration",7),P("reduction",.25f),P("receivedDamageThreshold",80),P("manaPerThreshold",12),P("manaPerCastCap",18)};
        owner.EnergyMax=37;
        foreach(bool spanish in new[]{true,false})
        {
            string copy=GameText.AbilityDescription(ability,owner,spanish);
            foreach(string number in new[]{"7","25%","80","12","18"})StringAssert.Contains(number,copy);
            Assert.IsFalse(copy.Contains("{"),copy);
            Assert.IsFalse(Regex.IsMatch(copy,@"(?:Energía|Energy):\s*37"),copy);
            StringAssert.Contains(spanish?"lo evitado":"amount prevented",copy);
        }
        Assert.AreEqual(characterBefore,EditorJsonUtility.ToJson(source));
        Assert.AreEqual(abilityBefore,EditorJsonUtility.ToJson(source.Ability));
    }
    static AbilityParameter P(string key,float value)=>new AbilityParameter{Key=key,Value=value};

    [TestCase(true,"Tiempo bajo tierra (s)")]
    [TestCase(false,"Time underground (s)")]
    public void ConsumedUndergroundDurationHasAnExplicitLabel(bool spanish,string expected)
    {
        var labels=System.Type.GetType("EronyaEditorParameterLabels, Assembly-CSharp-Editor");
        Assert.AreEqual(expected,labels.GetMethod("Label").Invoke(null,new object[]{"la-minera-tunel-subterraneo","duration",spanish}));
    }

    [TestCase("zephalontropo-camara-de-precision","armorPenetration")]
    [TestCase("la-minera-fractura","armorReduction")]
    [TestCase("la-minera-fractura","duration")]
    public void RetiredArmorFieldsDoNotReturnAsEditableControls(string effect,string key)
    {
        Assert.IsTrue(AbilityBalance.IsRetiredArmorParameter(effect,key));
        Assert.IsFalse(AbilityBalance.Resolve(new TrickDefinition{EffectId=effect}).Any(p=>p.Key==key));
    }
}
