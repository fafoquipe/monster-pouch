// Place in an Editor test assembly during the integration turn. Not executed yet.
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class EronyaBalanceOwnerUndoTests
{
    [Test]
    public void UndoBaseHealRebuildsNestedOwnerBeforeEditingDelta()
    {
        var character=ScriptableObject.CreateInstance<CharacterDefinition>();
        var ability=ScriptableObject.CreateInstance<CharacterAbility>();
        var upgrade=ScriptableObject.CreateInstance<CharacterAbility>();
        Editor editor=null;
        var editorType=System.AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MonsterPouchAbilityEditor")).First(t=>t!=null);
        try
        {
            character.InitializeIdentity("flo");character.Ability=ability;
            ability.Definition=new TrickDefinition {EffectId="flo-flower",Parameters=new[]{new AbilityParameter {Key="heal",Value=20}}};
            upgrade.Definition=new TrickDefinition {EffectId="flo-flower-heal",Parameters=new[]{new AbilityParameter {Key="heal",Value=35}}};
            editor=Editor.CreateEditor(upgrade);editorType.GetField("OwnerSource").SetValue(editor,character);
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
            Undo.RecordObject(ability,"Edit base healing");ability.Definition.Parameters[0].Value=12;
            Undo.FlushUndoRecordObjects();editorType.GetMethod("RefreshOwner").Invoke(editor,null);
            Assert.AreEqual(23,AbilityBalance.ParameterEditorValue("flo-flower-heal","heal",35,(UnitDefinition)editorType.GetField("Owner").GetValue(editor)));
            Undo.RevertAllDownToGroup(group);editorType.GetMethod("RefreshOwner").Invoke(editor,null);
            Assert.AreEqual(20,ability.Definition.Parameters[0].Value);
            Assert.AreEqual(15,AbilityBalance.ParameterEditorValue("flo-flower-heal","heal",35,(UnitDefinition)editorType.GetField("Owner").GetValue(editor)));
            Assert.AreEqual(35,AbilityBalance.ParameterStoredValue("flo-flower-heal","heal",15,(UnitDefinition)editorType.GetField("Owner").GetValue(editor)));
        }
        finally
        {
            if(editor!=null)Object.DestroyImmediate(editor);
            Object.DestroyImmediate(upgrade);Object.DestroyImmediate(ability);Object.DestroyImmediate(character);
        }
    }
}
