using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using UnityEditor;
using UnityEngine;

/// <summary>Balance editing preserves the identity and exclusive mechanics of each character.</summary>
[CustomEditor(typeof(MatchConfig))]
public sealed class MonsterPouchBalanceInspector : Editor
{
    int selected;
    bool matchSettings;
    Dictionary<string, UnitDefinition> references;
    bool referenceMode;

    static readonly string[] Stats = { "MaxHealth", "Damage", "AttackRange", "AttackInterval", "AttackWindup", "ProjectileSpeed", "MoveInterval", "IQSpeed", "BaseCost", "TargetPolicy", "Formation" };
    static readonly string[] Energy = { "EnergyMax", "EnergyPerAttack", "EnergyOnDamage", "EnergyPerSecond" };
    static readonly string[] Revival = { "RevivesPerCombat", "ReviveHealthFraction", "ReviveDelay" };
    static readonly string[] Summoning = { "MaxLivingSummons", "SummonHealth", "SummonDamage", "SummonAttackInterval", "SummonMoveInterval" };
    static readonly string[] EffectBalance = { "HealthBonus", "DamageBonus", "AttackIntervalMultiplier", "RangeBonus", "Armor", "HealOnHit", "BonusEveryHits", "BonusDamage" };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var units = serializedObject.FindProperty("Units");
        if (units == null || units.arraySize == 0)
        {
            EditorGUILayout.HelpBox("El catálogo no contiene personajes.", MessageType.Info);
            return;
        }
        selected = Mathf.Clamp(selected, 0, units.arraySize - 1);
        var names = new string[units.arraySize];
        for (int i = 0; i < names.Length; i++)
            names[i] = units.GetArrayElementAtIndex(i).FindPropertyRelative("DisplayName").stringValue;
        selected = EditorGUILayout.Popup("Personaje", selected, names);
        var unit = units.GetArrayElementAtIndex(selected);
        string id = unit.FindPropertyRelative("Id").stringValue;
        bool documented = unit.FindPropertyRelative("UsesDocumentedRules")?.boolValue ?? false;
        UnitDefinition reference = Reference(id, documented);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(true))
        {
            Draw(unit, "Id", "Identidad del personaje");
            Draw(unit, "IsMonster", "Monster");
            Draw(unit, "AttackKind", "Tipo de ataque");
        }
        EditorGUILayout.HelpBox("Las habilidades pertenecen a este personaje. Aquí puedes ajustar su balance; la identidad de los ataques y habilidades permanece fija.", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        Draw(unit, "DisplayName", "Nombre");
        EditorGUILayout.LabelField("Estadísticas", EditorStyles.boldLabel);
        foreach (string field in Stats) Draw(unit, field);

        if (reference != null && Number(reference, "EnergyMax") > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Energía de su habilidad", EditorStyles.boldLabel);
            foreach (string field in Energy) Draw(unit, field);
        }
        if (id == "anuik")
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Resurrección de Anuik", EditorStyles.boldLabel);
            foreach (string field in Revival) Draw(unit, field);
        }
        else if (id == "tauris" && !documented)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mordisco de Tauris", EditorStyles.boldLabel);
            Draw(unit, "LethalEveryHits", "Impactos por mordisco letal");
        }
        else if (id == "kayon")
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Dummies de Kayon", EditorStyles.boldLabel);
            if (!documented) Draw(unit, "SummonInterval", "Intervalo de invocación");
            foreach (string field in Summoning) Draw(unit, field);
        }

        DrawAbility(unit.FindPropertyRelative("BaseAbility"), reference?.BaseAbility, "Habilidad propia", false, unit);
        var tricks = unit.FindPropertyRelative("Tricks");
        if (documented || !unit.FindPropertyRelative("IsMonster").boolValue)
        {
            for (int i = 0; tricks != null && i < Math.Min(3, tricks.arraySize); i++)
                DrawAbility(tricks.GetArrayElementAtIndex(i), reference?.Tricks != null && i < reference.Tricks.Length ? reference.Tricks[i] : null, "Mejora " + (i + 1), true, unit);
        }
        else DrawAbility(unit.FindPropertyRelative("MonsterUpgrade"), reference?.MonsterUpgrade, "Mejora propia", true, unit);

        if (EditorGUI.EndChangeCheck())
        {
            // A balance edit cannot activate another unit's hidden legacy mechanic.
            ClearForeignEnablers(unit, id);
            serializedObject.ApplyModifiedProperties();
        }

        EditorGUILayout.Space();
        matchSettings = EditorGUILayout.Foldout(matchSettings, "Reglas de la partida", true);
        if (matchSettings)
        {
            foreach (string field in new[] { "RoundIncome", "ReturnSoldCopies", "TeamWhelpLimit", "BlueDeployment", "RedDeployment" })
            {
                var property = serializedObject.FindProperty(field);
                if (property != null) EditorGUILayout.PropertyField(property, true);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }

    void DrawAbility(SerializedProperty property, TrickDefinition reference, string heading, bool hasCost, SerializedProperty unit)
    {
        if (property == null || reference == null) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(heading + " · " + reference.Name, EditorStyles.boldLabel);
        var identity = property.FindPropertyRelative("Id");
        var effectId = property.FindPropertyRelative("EffectId");
        string expectedEffect = typeof(TrickDefinition).GetField("EffectId")?.GetValue(reference) as string;
        bool wrongOwner = identity != null && identity.stringValue != reference.Id ||
                          effectId != null && effectId.stringValue != (expectedEffect ?? "");
        if (wrongOwner)
        {
            EditorGUILayout.HelpBox("Esta entrada contiene una habilidad ajena. Restaura la habilidad propia antes de ajustar su balance.", MessageType.Error);
            if (GUILayout.Button("Restaurar " + reference.Name)) CopyAbility(property, reference);
            return;
        }
        using (new EditorGUI.DisabledScope(true))
        {
            Draw(property, "Name", "Habilidad");
            Draw(property, "Trigger", "Activación");
        }
        if (hasCost) Draw(property, "Cost", "Coste");
        var parameters=property.FindPropertyRelative("Parameters");
        if(parameters!=null)for(int i=0;i<parameters.arraySize;i++)
        {
            var parameter=parameters.GetArrayElementAtIndex(i);
            string key=parameter.FindPropertyRelative("Key").stringValue;
            EditorGUILayout.PropertyField(parameter.FindPropertyRelative("Value"),new GUIContent(effectId.stringValue=="aky-opening-flight"&&key=="duration"?"Tiempo de vuelo (s)":key));
        }
        // New balance parameters remain editable in existing assets, without running a
        // content migration that could overwrite the designer's other balance values.
        if(parameters!=null)foreach(var fallback in AbilityBalance.Resolve(reference))
        {
            bool exists=false;
            for(int i=0;i<parameters.arraySize;i++)
                if(parameters.GetArrayElementAtIndex(i).FindPropertyRelative("Key").stringValue==fallback.Key){exists=true;break;}
            if(exists)continue;
            EditorGUI.BeginChangeCheck();
            float value=EditorGUILayout.FloatField(effectId.stringValue=="aky-opening-flight"&&fallback.Key=="duration"?"Tiempo de vuelo (s)":fallback.Key,fallback.Value);
            if(EditorGUI.EndChangeCheck())
            {
                int index=parameters.arraySize;parameters.InsertArrayElementAtIndex(index);
                var added=parameters.GetArrayElementAtIndex(index);
                added.FindPropertyRelative("Key").stringValue=fallback.Key;
                added.FindPropertyRelative("Value").floatValue=Mathf.Max(0,value);
            }
        }
        foreach (string field in EffectBalance)
        {
            double value = Number(reference, field);
            bool active = field == "AttackIntervalMultiplier" ? Math.Abs(value - 1) > .00001 : Math.Abs(value) > .00001;
            if (active) Draw(property, field);
        }
        EditorGUILayout.LabelField("Efecto · se actualiza con las estadísticas", EditorStyles.miniLabel);
        EditorGUILayout.HelpBox(DescriptionPreview(property,unit),MessageType.None);
    }

    // Read pending serialized values too: the preview changes in the same GUI pass as an edit,
    // without saving text into the asset or interfering with Undo/Redo.
    public static string DescriptionPreview(SerializedProperty property, SerializedProperty unit)
    {
        var effect=new TrickDefinition
        {
            Id=property.FindPropertyRelative("Id").stringValue,
            EffectId=property.FindPropertyRelative("EffectId").stringValue,
            Description=property.FindPropertyRelative("Description").stringValue,
            EveryAttacks=property.FindPropertyRelative("EveryAttacks").intValue,
            HealthBonus=property.FindPropertyRelative("HealthBonus").intValue
        };
        var parameters=property.FindPropertyRelative("Parameters");
        effect.Parameters=new AbilityParameter[parameters.arraySize];
        for(int i=0;i<parameters.arraySize;i++)
        {
            var parameter=parameters.GetArrayElementAtIndex(i);
            effect.Parameters[i]=new AbilityParameter
            {
                Key=parameter.FindPropertyRelative("Key").stringValue,
                Value=parameter.FindPropertyRelative("Value").floatValue
            };
        }
        var owner=new UnitDefinition
        {
            RevivesPerCombat=unit.FindPropertyRelative("RevivesPerCombat").intValue,
            ReviveHealthFraction=unit.FindPropertyRelative("ReviveHealthFraction").floatValue
        };
        return AbilityBalance.Description(effect,owner);
    }

    static void ClearForeignEnablers(SerializedProperty unit, string id)
    {
        if (id != "anuik") SetZero(unit, "RevivesPerCombat");
        if (id != "tauris") SetZero(unit, "LethalEveryHits");
        if (id != "kayon") { SetZero(unit, "SummonInterval"); SetZero(unit, "MaxLivingSummons"); }
    }

    static void SetZero(SerializedProperty parent, string name)
    {
        var p = parent.FindPropertyRelative(name);
        if (p == null) return;
        if (p.propertyType == SerializedPropertyType.Float) p.floatValue = 0;
        else p.intValue = 0;
    }

    static void Draw(SerializedProperty parent, string name, string label = null)
    {
        var p = parent.FindPropertyRelative(name);
        if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label ?? ObjectNames.NicifyVariableName(name)), true);
    }

    static double Number(object value, string field)
    {
        object number = value?.GetType().GetField(field)?.GetValue(value);
        return number == null ? 0 : Convert.ToDouble(number);
    }

    static void CopyAbility(SerializedProperty property, TrickDefinition reference)
    {
        foreach (var field in typeof(TrickDefinition).GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            var p = property.FindPropertyRelative(field.Name); if (p == null) continue;
            object value = field.GetValue(reference);
            if (field.FieldType == typeof(string)) p.stringValue = value as string ?? "";
            else if (field.FieldType == typeof(int)) p.intValue = (int)value;
            else if (field.FieldType == typeof(float)) p.floatValue = (float)value;
            else if (field.FieldType.IsEnum) p.intValue = Convert.ToInt32(value);
        }
        property.serializedObject.ApplyModifiedProperties();
    }

    UnitDefinition Reference(string id, bool documented)
    {
        if (references == null || referenceMode != documented)
        {
            referenceMode = documented;
            references = new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);
            // Optional until the parallel PDF integration lands; no dependency on its in-progress files.
            var documentedType = typeof(MatchConfig).Assembly.GetType("MonsterPouch.Gameplay.Match.DocumentedRoster");
            var create = documentedType?.GetMethod("CreateAll", BindingFlags.Public | BindingFlags.Static);
            if (documented && create != null)
            {
                foreach (var unit in (UnitDefinition[])create.Invoke(null, null)) references[unit.Id] = unit;
            }
            else
            {
                var defaults = MatchConfig.CreateDefault();
                foreach (var unit in defaults.Units) references[unit.Id] = unit;
                DestroyImmediate(defaults);
                foreach (var unit in ExpandedRoster.CreateAll()) references[unit.Id] = unit;
                var atori = MonsterPouchAtoriSetup.CreateDefinition(); references[atori.Id] = atori;
            }
        }
        references.TryGetValue(id, out var result); return result;
    }
}
