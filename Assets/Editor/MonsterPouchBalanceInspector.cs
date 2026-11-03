using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;

/// <summary>Balance editing preserves the identity and exclusive mechanics of each character.</summary>
[CustomEditor(typeof(MatchConfig))]
public sealed partial class MonsterPouchBalanceInspector : Editor
{
    int selected;
    [SerializeField] string selectedId;
    bool matchSettings;
    bool roundPresentation;
    Dictionary<string, UnitDefinition> references;
    bool referenceMode;
    UnitArtCatalog presentationCatalog;

    static readonly string[] Stats = { "MaxHealth", "Damage", "AttackRange", "AttackInterval", "ProjectileSpeed", "MoveInterval", "IQSpeed", "BaseCost", "TargetPolicy", "Formation" };
    static readonly string[] Energy = { "EnergyMax", "EnergyPerAttack", "EnergyOnDamage", "EnergyPerSecond" };
    static readonly string[] Revival = { "RevivesPerCombat", "ReviveHealthFraction" };
    static readonly string[] Summoning = { "MaxLivingSummons", "SummonHealth", "SummonDamage", "SummonAttackInterval", "SummonMoveInterval" };
    static readonly string[] EffectBalance = { "HealthBonus", "DamageBonus", "AttackIntervalMultiplier", "RangeBonus", "Armor", "HealOnHit", "BonusEveryHits", "BonusDamage" };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        if(((MatchConfig)target).Characters?.Length>0)
        {
            BalanceText.Language();
            if(GUILayout.Button(BalanceText.L("Open Monster Pouch — Balance Editor","Abrir Monster Pouch — Editor de balance")))MonsterPouchBalanceEditor.Open();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Characters"),true);
            foreach(string field in new[]{"RoundUI","RoundIncome","ReturnSoldCopies","TeamWhelpLimit","BlueDeployment","RedDeployment"})EditorGUILayout.PropertyField(serializedObject.FindProperty(field),true);
            serializedObject.ApplyModifiedProperties();return;
        }
        var units = serializedObject.FindProperty("Units");
        if (units == null || units.arraySize == 0)
        {
            EditorGUILayout.HelpBox("El catálogo no contiene personajes.", MessageType.Info);
            return;
        }
        selected = Mathf.Clamp(selected, 0, units.arraySize - 1);
        var order=CharacterDisplayOrder(units);
        if(!string.IsNullOrEmpty(selectedId))
        {
            int current=Array.FindIndex(order,index=>units.GetArrayElementAtIndex(index).FindPropertyRelative("Id").stringValue==selectedId);
            if(current>=0)selected=order[current];
        }
        var names=order.Select(index=>CharacterMenuLabel(units.GetArrayElementAtIndex(index))).ToArray();
        int displaySelection=EditorGUILayout.Popup("Personaje",Array.IndexOf(order,selected),names);
        selected=order[Mathf.Clamp(displaySelection,0,order.Length-1)];
        var unit = units.GetArrayElementAtIndex(selected);
        string id = unit.FindPropertyRelative("Id").stringValue;
        selectedId=id;
        bool documented = unit.FindPropertyRelative("UsesDocumentedRules")?.boolValue ?? false;
        UnitDefinition reference = Reference(id, documented);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(true))
        {
            Draw(unit, "Id", "Identidad del personaje");
            Draw(unit, "IsMonster", BalanceText.L("Hero slot","Puesto de héroe"));
            Draw(unit, "AttackKind", "Tipo de ataque");
        }
        EditorGUILayout.HelpBox("Edita los bonus (+), multiplicadores (×) y tiempos de cada habilidad. Su descripción se actualiza automáticamente. ×2 significa el doble del daño de ataque; +2 añade dos puntos.", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        Draw(unit, "DisplayName", "Nombre");
        EditorGUILayout.LabelField("Estadísticas", EditorStyles.boldLabel);
        foreach (string field in Stats) Draw(unit, field);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animación del ataque normal",EditorStyles.boldLabel);
        Draw(unit,"AttackWindup","Carga del golpe (s)");
        var basicTail=BasicAttackTiming.DefaultTail(id);
        DrawAttackPhase(unit,"AttackHold","Mantener el golpe (s)",basicTail.x);
        DrawAttackPhase(unit,"AttackRecovery","Retraer el golpe (s)",basicTail.y);
        EditorGUILayout.HelpBox("Carga marca cuándo impacta o sale el proyectil. Golpe mantiene la pose; retraer vuelve al reposo. 0 elimina esa fase. Si la animación supera el intervalo, el siguiente ataque espera a que termine.",MessageType.None);

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

        DrawRoundPresentation();
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

    static void DrawAttackPhase(SerializedProperty unit,string field,string label,float fallback)
    {
        var prop=unit.FindPropertyRelative(field);
        EditorGUI.BeginChangeCheck();
        float value=EditorGUILayout.FloatField(label,BasicAttackTiming.Resolve(prop.floatValue,fallback));
        if(EditorGUI.EndChangeCheck()&&!float.IsNaN(value)&&!float.IsInfinity(value))prop.floatValue=Mathf.Max(0,value);
    }

    void DrawRoundPresentation()
    {
        EditorGUILayout.Space();
        roundPresentation = EditorGUILayout.Foldout(roundPresentation, "Rondas · banderitas y transición", true);
        if (!roundPresentation) return;
        var settings = serializedObject.FindProperty("RoundUI");
        if (settings == null) return;
        EditorGUILayout.HelpBox("El primero en ganar 3 rondas gana la partida. El avance es automático y no depende de la animación. Las posiciones se miden desde arriba a la izquierda en el lienzo de 540 × 960.", MessageType.Info);
        Draw(settings, "maxRoundsToWin", "Victorias para ganar");
        EditorGUILayout.LabelField("Tiempos (segundos)", EditorStyles.boldLabel);
        Draw(settings, "delayBeforeBetweenRoundsUI", "Pausa al terminar combate");
        Draw(settings, "timeoutBannerDuration", "Cartel de tiempo agotado");
        Draw(settings, "healthComparisonDuration", "Barras del desempate por vida");
        Draw(settings, "betweenRoundsPanelAnimationDuration", "Entrada de tarjetas");
        Draw(settings, "flagStampAnimationDuration", "Estampa de banderita");
        Draw(settings, "delayAfterFlagStamp", "Pausa tras estampa");
        Draw(settings, "nextRoundStartDelay", "Salida hacia siguiente ronda");
        EditorGUILayout.LabelField("Posiciones y separación", EditorStyles.boldLabel);
        Draw(settings, "enemyPanelPosition", "Panel enemigo (X, Y)");
        Draw(settings, "playerPanelPosition", "Panel jugador (X, Y)");
        Draw(settings, "roundLabelPositionOffset", "Desplazamiento de Round");
        Draw(settings, "timerPositionOffset", "Desplazamiento del reloj");
        Draw(settings, "spacingBetweenFlags", "Espacio entre banderitas");
        serializedObject.ApplyModifiedProperties();
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
        var owner=ReadDescriptionOwner(unit);
        MonsterPouchAbilityAreaEditor.Draw(property, reference, owner);
        var parameters=property.FindPropertyRelative("Parameters");
        if(parameters!=null)for(int i=0;i<parameters.arraySize;i++)
        {
            var parameter=parameters.GetArrayElementAtIndex(i);
            string key=parameter.FindPropertyRelative("Key").stringValue;
            if(MonsterPouchAbilityAreaEditor.HidesOriginalParameter(property,key))continue;
            if(IsAnimationParameter(effectId.stringValue,key))continue;
            // This old total is not read by combat; extraDuration is the editable bonus.
            if(effectId.stringValue=="stein-long-stun"&&key=="duration")continue;
            var stored=parameter.FindPropertyRelative("Value");
            EditorGUI.BeginChangeCheck();
            float value=EditorGUILayout.FloatField(AbilityParameterLabel(effectId.stringValue,key),
                AbilityBalance.ParameterEditorValue(effectId.stringValue,key,stored.floatValue,owner));
            if(EditorGUI.EndChangeCheck()&&!float.IsNaN(value)&&!float.IsInfinity(value))
                stored.floatValue=AbilityBalance.ParameterStoredValue(effectId.stringValue,key,value,owner);
        }
        // New balance parameters remain editable in existing assets, without running a
        // content migration that could overwrite the designer's other balance values.
        if(parameters!=null)foreach(var fallback in AbilityBalance.Resolve(reference))
        {
            if(MonsterPouchAbilityAreaEditor.HidesOriginalParameter(property,fallback.Key))continue;
            if(IsAnimationParameter(effectId.stringValue,fallback.Key))continue;
            if(effectId.stringValue=="stein-long-stun"&&fallback.Key=="duration")continue;
            bool exists=false;
            for(int i=0;i<parameters.arraySize;i++)
                if(parameters.GetArrayElementAtIndex(i).FindPropertyRelative("Key").stringValue==fallback.Key){exists=true;break;}
            if(exists)continue;
            // Existing Bugaloo assets inherit their customized reflect duration until
            // the designer explicitly sets an independent taunt duration.
            float initialValue=fallback.Value;
            if(effectId.stringValue=="bugaloo-reflect"&&fallback.Key=="tauntDuration")
                for(int i=0;i<parameters.arraySize;i++)
                {
                    var existing=parameters.GetArrayElementAtIndex(i);
                    if(existing.FindPropertyRelative("Key").stringValue=="duration")
                        initialValue=existing.FindPropertyRelative("Value").floatValue;
                }
            EditorGUI.BeginChangeCheck();
            float value=EditorGUILayout.FloatField(AbilityParameterLabel(effectId.stringValue,fallback.Key),
                AbilityBalance.ParameterEditorValue(effectId.stringValue,fallback.Key,initialValue,owner));
            if(EditorGUI.EndChangeCheck()&&!float.IsNaN(value)&&!float.IsInfinity(value))
            {
                int index=parameters.arraySize;parameters.InsertArrayElementAtIndex(index);
                var added=parameters.GetArrayElementAtIndex(index);
                added.FindPropertyRelative("Key").stringValue=fallback.Key;
                added.FindPropertyRelative("Value").floatValue=AbilityBalance.ParameterStoredValue(effectId.stringValue,fallback.Key,value,owner);
            }
        }
        foreach (string field in EffectBalance)
        {
            double value = Number(reference, field);
            bool active = field == "AttackIntervalMultiplier" ? Math.Abs(value - 1) > .00001 : Math.Abs(value) > .00001;
            if (active) Draw(property, field);
        }
        DrawAbilityAnimation(property,unit);
        EditorGUILayout.LabelField("Efecto · se actualiza con las estadísticas", EditorStyles.miniLabel);
        EditorGUILayout.HelpBox(DescriptionPreview(property,unit),MessageType.None);
    }

    static string AbilityParameterLabel(string effect,string key)
    {
        if(key=="castDuration")return "Preparación (s; 0 instantáneo; -1 ataque base)";
        if(key=="holdDuration")return "Mantener pose de impacto (s)";
        if(key=="recoveryDuration")return "Animación de regreso (s)";
        if(key=="animationDuration")return "Duración de esta animación (0 = original)";
        if(effect=="tokoro-ice-cream"&&key=="travelDuration")return "Recorrido de los helados (s)";
        if(effect=="hymay-backstep"&&key=="travelDuration")return "Tiempo de voltereta (s)";
        if(effect=="hymay-pull"&&key=="pullDuration")return "Tiempo de arrastre (s)";
        if(effect=="hymay-pull"&&key=="postArrivalStun")return "Stun tras llegar (s)";
        if(effect=="anuik-revive-impact"&&key=="stunDuration")return "Duración del stun (s)";
        return effect=="aky-opening-flight"&&key=="duration"?"Tiempo de vuelo (s)":AbilityBalance.ParameterLabel(effect,key);
    }

    // Read pending serialized values too: the preview changes in the same GUI pass as an edit,
    // without saving text into the asset or interfering with Undo/Redo.
    public static string DescriptionPreview(SerializedProperty property, SerializedProperty unit)
        =>GameText.AbilityDescription(ReadDescriptionEffect(property),ReadDescriptionOwner(unit),BalanceText.Spanish);

    static UnitDefinition ReadDescriptionOwner(SerializedProperty unit)=>new UnitDefinition
    {
        Id=unit.FindPropertyRelative("Id").stringValue,
        DisplayName=unit.FindPropertyRelative("DisplayName").stringValue,
        Ruleset=(CombatRuleset)unit.FindPropertyRelative("Ruleset").enumValueIndex,
        MaxHealth=unit.FindPropertyRelative("MaxHealth").intValue,
        Damage=unit.FindPropertyRelative("Damage").intValue,
        AttackRange=unit.FindPropertyRelative("AttackRange").intValue,
        EnergyMax=unit.FindPropertyRelative("EnergyMax").floatValue,
        EnergyPerAttack=unit.FindPropertyRelative("EnergyPerAttack").floatValue,
        EnergyPerSecond=unit.FindPropertyRelative("EnergyPerSecond").floatValue,
        EnergyOnDamage=unit.FindPropertyRelative("EnergyOnDamage").floatValue,
        PassiveAbility=ReadDescriptionEffect(unit.FindPropertyRelative("PassiveAbility")),
        Tricks=Enumerable.Range(0,unit.FindPropertyRelative("Tricks").arraySize)
            .Select(i=>ReadDescriptionEffect(unit.FindPropertyRelative("Tricks").GetArrayElementAtIndex(i))).ToArray(),
        AttackWindup=unit.FindPropertyRelative("AttackWindup").floatValue,
        AttackInterval=unit.FindPropertyRelative("AttackInterval").floatValue,
        RevivesPerCombat=unit.FindPropertyRelative("RevivesPerCombat").intValue,
        ReviveHealthFraction=unit.FindPropertyRelative("ReviveHealthFraction").floatValue,
        ReviveDelay=unit.FindPropertyRelative("ReviveDelay").floatValue,
        BaseAbility=ReadDescriptionEffect(unit.FindPropertyRelative("BaseAbility"))
    };
    static TrickDefinition ReadDescriptionEffect(SerializedProperty property)
    {
        if(property==null)return null;
        var effect=new TrickDefinition
        {
            Id=property.FindPropertyRelative("Id").stringValue,
            EffectId=property.FindPropertyRelative("EffectId").stringValue,
            Description=property.FindPropertyRelative("Description").stringValue,
            EveryAttacks=property.FindPropertyRelative("EveryAttacks").intValue,
            HealthBonus=property.FindPropertyRelative("HealthBonus").intValue,
            DamageBonus=property.FindPropertyRelative("DamageBonus").intValue,
            RangeBonus=property.FindPropertyRelative("RangeBonus").intValue
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
        return effect;
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
        if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label ?? ObjectNames.NicifyVariableName(name), p.tooltip), true);
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
