using System;
using System.Linq;
using System.Text.RegularExpressions;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class EditableEnergyCopyTests
{
    static Type Labels=>Type.GetType("EronyaEditorParameterLabels, Assembly-CSharp-Editor");
    static object Call(string method,params object[] args)=>Labels.GetMethod(method).Invoke(null,args);
    static readonly Regex Unwanted=new Regex(@"\b(?:maná|mana|physical|magical|magic|físico|mágico|físicos|mágicos|armor|armadura|penetración|penetration|ARM)\b",RegexOptions.IgnoreCase);

    [Test] public void EveryEronyaParameterHasExplicitLabelsInBothLanguages()
    {
        Assert.IsNotNull(Labels);
        foreach(string effect in EronyaAbilityParameters.EffectIds)
        foreach(var parameter in AbilityBalance.Resolve(new TrickDefinition{EffectId=effect}))
        foreach(bool spanish in new[]{false,true})
        {
            string label=(string)Call("Label",effect,parameter.Key,spanish);
            Assert.IsNotEmpty(label,effect+":"+parameter.Key);
            Assert.IsFalse(Unwanted.IsMatch(label),label);
        }
        Assert.AreEqual("Límite de energía por uso",Call("Label","alveyr-escudo-de-los-vientos","manaPerCastCap",true));
    }

    [Test] public void NestedVisualFieldsHaveLabelsInBothLanguages()
    {
        var type=Type.GetType("CharacterEditorVisualLabels, Assembly-CSharp-Editor");
        Assert.IsNotNull(type);
        foreach(var source in new[]{typeof(UnitArt),typeof(UnitDirectionalAnimation),typeof(UnitSpriteRig)})
        foreach(var field in source.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))
        foreach(bool spanish in new[]{true,false})
            Assert.IsNotEmpty((string)type.GetMethod("Label").Invoke(null,new object[]{field.Name,spanish}),source.Name+"."+field.Name);
    }

    [TestCase("alveyr-escudo-de-los-vientos","reduction",.4f,40f)]
    [TestCase("alveyr-viento-disciplinado","healthThreshold",.2f,20f)]
    [TestCase("alboguera-tempo-vivo","primaryAttackSpeed",.3f,30f)]
    [TestCase("serya-combustion","attackSpeed",15f,15f)]
    [TestCase("dagata-caza-de-brasas","healthThreshold",50f,50f)]
    public void EditorPercentagesRoundTripWithoutChangingCombatUnits(string effect,string key,float stored,float shown)
    {
        Assert.That((float)Call("EditorValue",effect,key,stored,null),Is.EqualTo(shown).Within(.0001f));
        Assert.That((float)Call("StoredValue",effect,key,shown,null),Is.EqualTo(stored).Within(.0001f));
        Assert.That((float)Call("StoredValue",effect,key,0f,null),Is.EqualTo(0));
    }

    [Test] public void SerializedEnergyEditsRoundTripRefreshDescriptionAndUndoWithoutChangingSource()
    {
        var source=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/alveyr/Character");
        Assert.IsNotNull(source);
        string sourceBefore=EditorJsonUtility.ToJson(source);
        var copy=Object.Instantiate(source);int group=-1;
        float capacity=copy.General.EnergyMax,perAttack=copy.General.EnergyPerAttack,perSecond=copy.General.EnergyPerSecond,onDamage=copy.General.EnergyOnDamage;
        string[] originalDescriptions=new[]{GameText.AbilityDescription(copy.Ability.Definition,copy.CreateRuntimeDefinition(),true),GameText.AbilityDescription(copy.Ability.Definition,copy.CreateRuntimeDefinition(),false)};
        try
        {
            Undo.IncrementCurrentGroup();group=Undo.GetCurrentGroup();
            using(var serialized=new SerializedObject(copy))
            {
                serialized.FindProperty("General.EnergyMax").floatValue=37;
                serialized.FindProperty("General.EnergyPerAttack").floatValue=2.5f;
                serialized.FindProperty("General.EnergyPerSecond").floatValue=0;
                serialized.FindProperty("General.EnergyOnDamage").floatValue=4.5f;
                Assert.IsTrue(serialized.ApplyModifiedProperties());
            }
            Undo.FlushUndoRecordObjects();
            var runtime=copy.CreateRuntimeDefinition();
            var restored=JsonUtility.FromJson<UnitDefinition>(JsonUtility.ToJson(runtime));
            Assert.AreEqual(37,restored.EnergyMax);Assert.AreEqual(2.5f,restored.EnergyPerAttack);Assert.AreEqual(0,restored.EnergyPerSecond);Assert.AreEqual(4.5f,restored.EnergyOnDamage);
            foreach(bool spanish in new[]{true,false})
            {
                string text=GameText.AbilityDescription(copy.Ability.Definition,runtime,spanish);
                Assert.AreEqual(originalDescriptions[spanish?0:1],text,"General capacity belongs on the character card, not in the ability copy.");
                Assert.IsFalse(Regex.IsMatch(text,@"(?:Energía|Energy):\s*37"));
                Assert.IsFalse(Unwanted.IsMatch(text),text);
            }
            Undo.RevertAllDownToGroup(group);group=-1;
            Assert.AreEqual(capacity,copy.General.EnergyMax);Assert.AreEqual(perAttack,copy.General.EnergyPerAttack);Assert.AreEqual(perSecond,copy.General.EnergyPerSecond);Assert.AreEqual(onDamage,copy.General.EnergyOnDamage);
            Assert.AreEqual(sourceBefore,EditorJsonUtility.ToJson(source));
        }
        finally
        {
            if(group>=0)Undo.RevertAllDownToGroup(group);
            Object.DestroyImmediate(copy);
        }
    }

    [Test] public void PlayerCopyUsesEnergyAndGeneralDamageWithResolvedNumbers()
    {
        foreach(var unit in EronyaRoster.CreateAll())
        {
            foreach(var ability in new[]{unit.BaseAbility,unit.PassiveAbility}.Concat(unit.Tricks))
            foreach(bool spanish in new[]{true,false})
            {
                string text=GameText.AbilityDescription(ability,unit,spanish);
                Assert.IsFalse(Unwanted.IsMatch(text),ability.EffectId+": "+text);
                Assert.IsFalse(text.Contains("{"),ability.EffectId+": unresolved template");
            }
            string basic=LocalGameUI.BasicAttackDetailText(new OwnedUnit(unit));
            Assert.IsFalse(Unwanted.IsMatch(basic),unit.Id);
        }
    }

    [TestCase(0f)]
    [TestCase(2f)]
    [TestCase(3.5f)]
    public void MineraTauntCopyUsesEditedDurationWithoutChangingTheSource(float seconds)
    {
        var source=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/la-minera/Character");
        Assert.IsNotNull(source);
        string before=EditorJsonUtility.ToJson(source);
        var clone=JsonUtility.FromJson<UnitDefinition>(JsonUtility.ToJson(source.CreateRuntimeDefinition()));
        var ability=clone.PassiveAbility;
        ability.Parameters=AbilityBalance.Resolve(ability).Select(p=>new AbilityParameter{Key=p.Key,Value=p.Key=="tauntDuration"?seconds:p.Value}).ToArray();
        Assert.IsTrue(ability.Parameters.Any(p=>p.Key=="tauntDuration"),"Core must register the editable taunt clock.");
        string value=seconds.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);
        foreach(bool spanish in new[]{true,false})
        {
            string description=GameText.AbilityDescription(ability,clone,spanish);
            StringAssert.Contains(spanish?"Provoca durante "+value+" s":"for "+value+" s",description);
            StringAssert.DoesNotContain("{",description);
            StringAssert.DoesNotContain(spanish?"cambia su objetivo":"switches its target",description);
        }
        Assert.AreEqual(seconds,ability.Parameters.Single(p=>p.Key=="tauntDuration").Value);
        Assert.AreEqual(before,EditorJsonUtility.ToJson(source));
    }
}
