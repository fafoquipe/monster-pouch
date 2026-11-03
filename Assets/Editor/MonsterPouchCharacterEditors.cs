using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;

public static class BalanceText
{
    public static bool Spanish=>EditorPrefs.GetBool("MonsterPouch.Balance.Spanish",false);
    public static string L(string en,string es)=>Spanish?es:en;
    public static void Language()
    {
        int choice=EditorGUILayout.Popup("Language / Idioma",Spanish?1:0,new[]{"English","Español"});
        if((choice==1)!=Spanish){EditorPrefs.SetBool("MonsterPouch.Balance.Spanish",choice==1);UnityEditorInternal.InternalEditorUtility.RepaintAllViews();}
    }
    // Explicit units and first-hit timing replace serialized variable names.
    static readonly Dictionary<string,string[]> labels=new Dictionary<string,string[]> {
        {"DisplayName",new[]{"Display name","Nombre"}},{"IsMonster",new[]{"Hero slot","Puesto de héroe"}},
        {"MaxHealth",new[]{"Health","Vida"}},{"MoveInterval",new[]{"Movement interval (s/tile)","Intervalo de movimiento (s/casilla)"}},
        {"IQSpeed",new[]{"IQ / reaction","IQ / reacción"}},{"BaseCost",new[]{"Cost (Moonkens)","Coste (Moonkens)"}},
        {"TargetPolicy",new[]{"Target selection","Selección de objetivo"}},{"Formation",new[]{"Formation","Formación"}},
        {"EnergyMax",new[]{"Energy bar capacity","Capacidad de energía"}},{"EnergyPerAttack",new[]{"Energy per basic attack","Energía por ataque básico"}},
        {"EnergyOnDamage",new[]{"Energy on damage taken","Energía al recibir daño"}},{"EnergyPerSecond",new[]{"Energy per second","Energía por segundo"}},
        {"RevivesPerCombat",new[]{"Revivals per combat","Resurrecciones por combate"}},{"ReviveHealthFraction",new[]{"Revival health (0–1)","Vida al resucitar (0–1)"}},
        {"ReviveDelay",new[]{"Revival delay (s)","Demora de resurrección (s)"}},{"MaxLivingSummons",new[]{"Living Dummy limit","Límite de Dummys vivos"}},
        {"SummonHealth",new[]{"Dummy health","Vida del Dummy"}},{"SummonDamage",new[]{"Dummy damage","Daño del Dummy"}},
        {"SummonAttackInterval",new[]{"Dummy attack interval (s)","Intervalo de ataque del Dummy (s)"}},{"SummonMoveInterval",new[]{"Dummy movement interval (s)","Intervalo de movimiento del Dummy (s)"}},
        {"Kind",new[]{"Attack type","Tipo de ataque"}},{"Damage",new[]{"Damage","Daño"}},{"Range",new[]{"Range (tiles)","Alcance (casillas)"}},
        {"Interval",new[]{"Attack interval (s)","Intervalo de ataque (s)"}},{"Windup",new[]{"First-hit preparation (s)","Preparación del primer golpe (s)"}},
        {"Hold",new[]{"Impact pose (s)","Pose de impacto (s)"}},{"Recovery",new[]{"Return to idle (s)","Regreso a reposo (s)"}},
        {"ProjectileSpeed",new[]{"Projectile speed (tiles/s)","Velocidad del proyectil (casillas/s)"}},
        {"Cost",new[]{"Cost (Moonkens)","Coste (Moonkens)"}},{"HealthBonus",new[]{"Bonus health","Vida adicional"}},
        {"DamageBonus",new[]{"Bonus damage","Daño adicional"}},{"AttackIntervalMultiplier",new[]{"Attack interval multiplier","Multiplicador del intervalo"}},
        {"RangeBonus",new[]{"Bonus range (tiles)","Alcance adicional (casillas)"}},{"Armor",new[]{"Armor","Armadura"}},
        {"HealOnHit",new[]{"Healing per hit","Cura por golpe"}},{"BonusEveryHits",new[]{"Bonus every N hits","Bonus cada N golpes"}},
        {"BonusDamage",new[]{"Bonus hit damage","Daño del golpe adicional"}},{"Icon",new[]{"Icon","Icono"}},
        {"Effect",new[]{"Effect prefab","Prefab del efecto"}},{"Sound",new[]{"Sound","Sonido"}},{"Portrait",new[]{"Portrait","Retrato"}},
        {"Card",new[]{"Card artwork","Arte de la ficha"}},{"Prefab",new[]{"Character prefab","Prefab del personaje"}},
        {"Visuals",new[]{"Animations and visuals","Animaciones y aspecto"}}
    };
    public static string Field(string key)=>labels.TryGetValue(key,out var p)?L(p[0],p[1]):ObjectNames.NicifyVariableName(key);
    public static void Property(SerializedProperty p,string key)
    {
        if(p==null)return;
        if(key=="Visuals"){CharacterEditorVisualLabels.Draw(p);return;}
        if(p.propertyType==SerializedPropertyType.Enum)
            p.enumValueIndex=EditorGUILayout.Popup(Field(key),p.enumValueIndex,p.enumNames.Select(EnumName).ToArray());
        else EditorGUILayout.PropertyField(p,new GUIContent(Field(key)),true);
    }
    static string EnumName(string v)=>!Spanish?ObjectNames.NicifyVariableName(v):v=="Melee"?"Cuerpo a cuerpo":v=="Ranged"?"A distancia":
        v=="NearestReachable"?"Más cercano alcanzable":v=="LowestHealth"?"Menor vida":v=="Front"?"Frente":v=="Middle"?"Centro":v=="Back"?"Fondo":v;
    static readonly Dictionary<string,string[]> parameters=new Dictionary<string,string[]> {
        {"duration",new[]{"Duration (s)","Duración (s)"}},{"damage",new[]{"Damage","Daño"}},{"damageOverride",new[]{"Ability damage","Daño de habilidad"}},
        {"multiplier",new[]{"Damage (× basic attack)","Daño (× ataque básico)"}},{"bounceDamageMultiplier",new[]{"Bounce damage (× hit)","Daño de rebote (× golpe)"}},
        {"damagePercent",new[]{"Bonus damage (%)","Daño adicional (%)"}},{"bonusPercent",new[]{"Bonus (%)","Bonus (%)"}},
        {"speedPercent",new[]{"Bonus speed (%)","Velocidad adicional (%)"}},{"weaknessPercent",new[]{"Additional damage taken (%)","Daño recibido adicional (%)"}},
        {"extraDuration",new[]{"Additional duration (s)","Duración adicional (s)"}},{"every",new[]{"Every N attacks","Cada N ataques"}},
        {"health",new[]{"Health","Vida"}},{"heal",new[]{"Healing","Cura"}},{"healthPercent",new[]{"Health (%)","Vida (%)"}},
        {"healPercent",new[]{"Healing (%)","Cura (%)"}},{"reduction",new[]{"Reduction (%)","Reducción (%)"}},{"energy",new[]{"Energy","Energía"}},
        {"coins",new[]{"Moonkens","Moonkens"}},{"interval",new[]{"Interval (s)","Intervalo (s)"}},{"stacks",new[]{"Maximum stacks","Acumulaciones máximas"}},
        {"count",new[]{"Summons","Invocaciones"}},{"targets",new[]{"Targets","Objetivos"}},{"superTargets",new[]{"Super targets","Objetivos de súper"}},
        {"radius",new[]{"Radius (tiles)","Radio (casillas)"}},{"radiusX",new[]{"Horizontal range (tiles each side)","Alcance horizontal (casillas por lado)"}},
        {"radiusY",new[]{"Vertical range (tiles each side)","Alcance vertical (casillas por lado)"}},{"width",new[]{"Width (tiles)","Anchura (casillas)"}},
        {"height",new[]{"Height (tiles)","Altura (casillas)"}},{"projectileSpeed",new[]{"Projectile speed (tiles/s)","Velocidad del proyectil (casillas/s)"}},
        {"distance",new[]{"Distance (tiles)","Distancia (casillas)"}},{"bounceRange",new[]{"Bounce range (tiles)","Alcance de rebote (casillas)"}},
        {"threshold",new[]{"Health threshold (%)","Umbral de vida (%)"}},{"shieldPercent",new[]{"Damage reduction (%)","Reducción de daño (%)"}},
        {"tauntDuration",new[]{"Taunt duration (s)","Duración de provocación (s)"}},{"travelDuration",new[]{"Travel time (s)","Tiempo de recorrido (s)"}},
        {"castDuration",new[]{"Preparation / release (s)","Preparación / lanzamiento (s)"}},{"holdDuration",new[]{"Impact pose (s)","Pose de impacto (s)"}},
        {"recoveryDuration",new[]{"Recovery (s)","Recuperación (s)"}},{"animationDuration",new[]{"Animation duration (s)","Duración de animación (s)"}},
        {"pullDuration",new[]{"Pull time (s)","Tiempo de arrastre (s)"}},{"postArrivalStun",new[]{"Stun after arrival (s)","Aturdimiento al llegar (s)"}},
        {"maxPerRound",new[]{"Maximum per round","Máximo por ronda"}},
        {"firstMoonDamage",new[]{"First moon damage","Daño de la primera luna"}},
        {"secondMoonDamage",new[]{"Second moon damage","Daño de la segunda luna"}},
        {"marsDamage",new[]{"Mars damage","Daño de Marte"}},
        {"travelRange",new[]{"Travel range (tiles)","Recorrido (casillas)"}},
        {"lateralTravelRange",new[]{"Side group travel (tiles)","Recorrido lateral (casillas)"}},
        {"fanAngleDegrees",new[]{"Fan angle per side (degrees)","Ángulo por lado (grados)"}},
        {"antiHealPercent",new[]{"Healing reduction (%)","Reducción de cura (%)"}},
        {"blockActions",new[]{"Block actions (0=no, 1=yes)","Bloquear acciones (0=no, 1=sí)"}},
        {"blockEnergy",new[]{"Block energy (0=no, 1=yes)","Bloquear energía (0=no, 1=sí)"}},
        {"burstInterval",new[]{"Burst interval (s)","Intervalo de ráfaga (s)"}},
        {"extension",new[]{"Area extension (tiles)","Ampliación del área (casillas)"}},
        {"healthPerKill",new[]{"Health per defeat","Vida por baja"}},
        {"range",new[]{"Range (tiles)","Alcance (casillas)"}},
        {"secondRingPercent",new[]{"Second ring damage (%)","Daño del segundo aro (%)"}},
        {"stunDuration",new[]{"Stun duration (s)","Duración de aturdimiento (s)"}}
    };
    public static string Parameter(string effect,string key)
    {
        string eronya=EronyaEditorParameterLabels.Label(effect,key,Spanish);
        if(eronya!=null)return eronya;
        if(effect=="rosie-haste" && key=="speedPercent")return L("Attack speed bonus (%)","Velocidad de ataque adicional (%)");
        if(key=="shield")return L("Shield health","Vida del escudo");
        if(key=="shieldBonus")return L("Additional shield (+)","Escudo adicional (+)");
        if(key=="energyPerShieldDamage")return L("Energy per allied shield damage (+)","Energía por daño al escudo aliado (+)");
        if(key=="shieldHealthPercent")return L("Shield (% of maximum health)","Escudo (% de vida máxima)");
        if(AbilityBalance.IsFactorParameter(effect,key))return key=="reflectPercent"?L("Reflected damage (× received)","Daño reflejado (× recibido)"):L("Critical damage (× attack)","Daño crítico (× ataque)");
        if(Spanish&&AbilityBalance.IsRelativeParameter(effect,key))return AbilityBalance.ParameterLabel(effect,key);
        string name=parameters.TryGetValue(key,out var pair)?L(pair[0],pair[1]):ObjectNames.NicifyVariableName(key);
        if(!Spanish&&AbilityBalance.IsRelativeParameter(effect,key))return key=="interval"?"Payout interval change (s; negative = sooner)":"Additional "+name.ToLowerInvariant();
        return name;
    }
    public static bool Timing(string key)=>key=="castDuration"||key=="holdDuration"||key=="recoveryDuration"||key=="animationDuration"||key=="travelDuration"||key=="pullDuration";
}
[CustomEditor(typeof(CharacterAttack))]
public sealed class MonsterPouchAttackEditor:Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        foreach(string key in new[]{"Kind","Damage","Range","Interval","Windup","Hold","Recovery","ProjectileSpeed"}) {
            if(key=="ProjectileSpeed"&&serializedObject.FindProperty("Kind").enumValueIndex!=(int)AttackKind.Ranged)continue;
            var p=serializedObject.FindProperty(key);
            if(key=="Hold"||key=="Recovery") {
                bool automatic=p.floatValue<0;
                bool value=EditorGUILayout.Toggle(BalanceText.Field(key)+" · "+BalanceText.L("character default","valor del personaje"),automatic);
                if(value!=automatic)p.floatValue=value?-1:0;if(value)continue;
            }
            BalanceText.Property(p,key);
        }
        MonsterPouchProjectileVisualControls.Draw(serializedObject);
        EditorGUILayout.HelpBox(BalanceText.L("First-hit preparation starts after the Super finishes. Basic attacks cannot overlap an energy Super.",
            "La preparación del primer golpe empieza al terminar la súper. Los ataques básicos no se superponen a una súper de energía."),MessageType.Info);
        serializedObject.ApplyModifiedProperties();
    }
}
[CustomEditor(typeof(CharacterAbility))]
public sealed class MonsterPouchAbilityEditor:Editor
{
    public UnitDefinition Owner;
    public CharacterDefinition OwnerSource;
    bool timing=true,assets,bonuses;
    public void RefreshOwner()
    {
        var character=OwnerSource;
        if(character==null)
        {
            var path=System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(target))+"/Character.asset";
            character=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
        }
        Owner=character==null?null:character.CreateRuntimeDefinition();
    }
    void OnEnable(){Undo.undoRedoPerformed+=DependencyChanged;EditorApplication.projectChanged+=DependencyChanged;}
    void OnDisable(){Undo.undoRedoPerformed-=DependencyChanged;EditorApplication.projectChanged-=DependencyChanged;}
    void DependencyChanged(){Owner=null;Repaint();}
    public override void OnInspectorGUI()
    {
        RefreshOwner();
        serializedObject.Update();var ability=(CharacterAbility)target;var d=serializedObject.FindProperty("Definition");
        if(AbilityBalance.IsRetiredArmorEffect(ability.Definition.EffectId??ability.Definition.Id)) {
            EditorGUILayout.HelpBox(BalanceText.L("This effect is retired.","Este efecto está retirado."),MessageType.None);return;
        }
        EditorGUILayout.LabelField(GameText.AbilityName(ability.Definition,BalanceText.Spanish),EditorStyles.boldLabel);
        using(new EditorGUI.DisabledScope(true))EditorGUILayout.TextField("ID",d.FindPropertyRelative("EffectId").stringValue);
        EditorGUILayout.LabelField(BalanceText.L("Activation","Activación"),ability.Definition.Trigger==AbilityTrigger.Energy?
            BalanceText.L("Super · energy bar","Súper · barra de energía"):ability.Definition.Trigger==AbilityTrigger.EveryAttacks?
            BalanceText.L("Every N basic attacks","Cada N ataques básicos"):BalanceText.L("Passive","Pasiva"));
        DrawParameters(d,false);
        timing=EditorGUILayout.Foldout(timing,BalanceText.L("Timing and animation","Tiempos y animación"),true);
        if(timing)DrawParameters(d,true);
        BalanceText.Property(d.FindPropertyRelative("Cost"),"Cost");
        // These generic bonuses are consumed by legacy rules, including zero
        // values edited into nonzero bonuses. Eronya uses its effect parameters.
        if(Owner?.Ruleset!=CombatRuleset.Eronya) {
            bonuses=EditorGUILayout.Foldout(bonuses,BalanceText.L("Stat bonuses","Bonificaciones de estadísticas"),true);
            if(bonuses)foreach(string field in new[]{"HealthBonus","DamageBonus","AttackIntervalMultiplier","RangeBonus","HealOnHit","BonusEveryHits","BonusDamage"})
                BalanceText.Property(d.FindPropertyRelative(field),field);
        }
        MonsterPouchProjectileVisualControls.Draw(serializedObject);
        assets=EditorGUILayout.Foldout(assets,BalanceText.L("Artwork and sound","Arte y sonido"),true);
        if(assets)foreach(string field in new[]{"Icon","Effect","Sound"})BalanceText.Property(serializedObject.FindProperty(field),field);
        serializedObject.ApplyModifiedProperties();
        RefreshOwner();
        MaestroWaveFieldPreview.Draw(ability);
        EditorGUILayout.HelpBox(GameText.AbilityDescription(ability.Definition,Owner,BalanceText.Spanish),MessageType.None);
    }
    void DrawParameters(SerializedProperty d,bool animation) => DrawParametersFor(((CharacterAbility)target).Definition,Owner,d,animation);
    public static void DrawParametersFor(TrickDefinition definition,UnitDefinition owner,SerializedProperty d,bool animation)
    {
        if (!animation) MonsterPouchAbilityAreaEditor.Draw(d, definition, owner);
        var stored=d.FindPropertyRelative("Parameters");
        foreach(var fallback in AbilityBalance.Resolve(definition)) {
            if(MonsterPouchAbilityAreaEditor.HidesOriginalParameter(d,fallback.Key))continue;
            if(BalanceText.Timing(fallback.Key)!=animation||AbilityBalance.IsRetiredArmorParameter(definition.EffectId??definition.Id,fallback.Key))continue;
            SerializedProperty value=null;
            for(int i=0;i<stored.arraySize;i++) {
                var entry=stored.GetArrayElementAtIndex(i);
                if(entry.FindPropertyRelative("Key").stringValue==fallback.Key){value=entry.FindPropertyRelative("Value");break;}
            }
            string effect=d.FindPropertyRelative("EffectId").stringValue,key=fallback.Key;
            float current=value?.floatValue??fallback.Value;
            if(EronyaAbilityParameters.Knows(effect))current=EronyaAbilityParameters.Normalize(effect,key,current);
            float next=current;string label=BalanceText.Parameter(effect,key);
            if(AbilityBalance.IsReadOnlyParameter(effect,key))
            {
                using(new EditorGUI.DisabledScope(true))EditorGUILayout.FloatField(label,fallback.Value);
                continue;
            }
            EditorGUI.BeginChangeCheck();
            if(key=="damageOverride"||(key=="castDuration"&&effect!="dagata-entrada-entre-humo")) {
                bool selected=EditorGUILayout.Toggle(label+" · "+BalanceText.L("automatic","automático"),current<0);
                if(selected){next=-1;if(key=="damageOverride")EditorGUILayout.LabelField(BalanceText.L("Formula","Fórmula"),AbilityBalance.DamageFormula(definition,BalanceText.Spanish));}
                else next=Mathf.Max(0,EditorGUILayout.FloatField(label,Mathf.Max(0,current)));
            } else next=EronyaEditorParameterLabels.StoredValue(effect,key,EditorGUILayout.FloatField(label,EronyaEditorParameterLabels.EditorValue(effect,key,current,owner)),owner);
            if(!EditorGUI.EndChangeCheck()||float.IsNaN(next)||float.IsInfinity(next))continue;
            if(value==null) {
                int index=stored.arraySize;stored.InsertArrayElementAtIndex(index);var entry=stored.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("Key").stringValue=key;value=entry.FindPropertyRelative("Value");
            }
            value.floatValue=EronyaAbilityParameters.Knows(effect)?EronyaAbilityParameters.Normalize(effect,key,next):next;
        }
    }
}
[CustomEditor(typeof(CharacterDefinition))]
public sealed class MonsterPouchCharacterEditor:Editor
{
    Editor nested;int tab,trick;
    public override void OnInspectorGUI()
    {
        var c=(CharacterDefinition)target;serializedObject.Update();
        bool eronya=EronyaRoster.IsId(c.Id);
        string ability=c.Ability!=null&&c.Ability.Definition.Trigger==AbilityTrigger.Energy?BalanceText.L("Super","Súper"):BalanceText.L("Ability","Habilidad");
        var tabs=new System.Collections.Generic.List<string>{BalanceText.L("Stats","Estadísticas"),BalanceText.L("Basic attack","Ataque básico"),ability,"Tricks",BalanceText.L("Assets","Recursos")};
        if(eronya&&c.General.PassiveAbility!=null&&!AbilityBalance.IsRetiredArmorEffect(c.General.PassiveAbility.EffectId??c.General.PassiveAbility.Id))tabs.Add(BalanceText.L("Passive","Pasiva"));
        tab=GUILayout.Toolbar(Mathf.Clamp(tab,0,tabs.Count-1),tabs.ToArray());
        EditorGUILayout.Space(10);
        if(tab==0) {
            DrawInGameSize();
            DrawMenuSizes();
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(BalanceText.L("Identity and movement","Identidad y movimiento"),EditorStyles.boldLabel);
            var g=serializedObject.FindProperty("General");
            foreach(string field in new[]{"DisplayName","IsMonster","MaxHealth","MoveInterval","IQSpeed","BaseCost","TargetPolicy","Formation"})BalanceText.Property(g.FindPropertyRelative(field),field);
            if(c.General.EnergyMax>0||c.Ability?.Definition.Trigger==AbilityTrigger.Energy) {
                EditorGUILayout.Space(8);EditorGUILayout.LabelField(BalanceText.L("Super · energy bar","Súper · barra de energía"),EditorStyles.boldLabel);
                foreach(string field in new[]{"EnergyMax","EnergyPerAttack","EnergyOnDamage","EnergyPerSecond"})
                    BalanceText.Property(g.FindPropertyRelative(field),field);
            }
            if(c.Id=="anuik")foreach(string field in new[]{"RevivesPerCombat","ReviveHealthFraction","ReviveDelay"})BalanceText.Property(g.FindPropertyRelative(field),field);
            if(c.Id=="kayon")foreach(string field in new[]{"MaxLivingSummons","SummonHealth","SummonDamage","SummonAttackInterval","SummonMoveInterval"})BalanceText.Property(g.FindPropertyRelative(field),field);
        } else if(tab==1)DrawNested(c.Attack,c);
        else if(tab==2)DrawNested(c.Ability,c);
        else if(tab==3) {
            var visible=Enumerable.Range(0,c.Upgrades?.Length??0).Where(i=>c.Upgrades[i]!=null&&!AbilityBalance.IsRetiredArmorEffect(c.Upgrades[i].Definition.EffectId??c.Upgrades[i].Definition.Id)).ToArray();
            if(visible.Length>0) {
                int choice=GUILayout.Toolbar(Mathf.Max(0,Array.IndexOf(visible,trick)),visible.Select(i=>"Trick "+(i+1)).ToArray());
                trick=visible[choice];EditorGUILayout.Space(8);DrawNested(c.Upgrades[trick],c);
            } else EditorGUILayout.HelpBox(BalanceText.L("No active Tricks.","Sin Tricks activos."),MessageType.None);
        } else if(tab==5&&eronya)
        {
            var passive=serializedObject.FindProperty("General").FindPropertyRelative("PassiveAbility");
            if(c.General.PassiveAbility!=null)
            {
                var owner=c.CreateRuntimeDefinition();
                MonsterPouchAbilityEditor.DrawParametersFor(c.General.PassiveAbility,owner,passive,false);
                if(c.General.PassiveAbility.EffectId=="dagata-entrada-entre-humo")
                {
                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField(BalanceText.L("Jump timing","Tiempos del salto"),EditorStyles.boldLabel);
                    MonsterPouchAbilityEditor.DrawParametersFor(c.General.PassiveAbility,owner,passive,true);
                    EditorGUILayout.HelpBox(BalanceText.L(
                        "Each phase uses simulation time in 0.1 s steps. Zero skips that phase. Travel does not grant invulnerability.",
                        "Cada fase usa tiempo de simulación en pasos de 0,1 s. Cero omite esa fase. El recorrido no otorga invulnerabilidad."),MessageType.Info);
                }
            }
        }
        else foreach(string field in new[]{"Portrait","Card","Icon","Prefab","Visuals"})BalanceText.Property(serializedObject.FindProperty(field),field);
        serializedObject.ApplyModifiedProperties();
        if(tab==5&&eronya&&c.General.PassiveAbility!=null)EditorGUILayout.HelpBox(GameText.AbilityDescription(c.General.PassiveAbility,c.CreateRuntimeDefinition(),BalanceText.Spanish),MessageType.None);
        if(c.Attack==null||c.Ability==null)EditorGUILayout.HelpBox(BalanceText.L("Assign an attack and an ability asset.","Asigna los recursos de ataque y habilidad."),MessageType.Error);
    }
    void DrawMenuSizes()
    {
        var character=(CharacterDefinition)target;
        var visuals=serializedObject.FindProperty("Visuals");
        if(visuals==null||character.Visuals==null)return;
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(BalanceText.L("Size on menu pedestals","Tamaño sobre los pedestales del menú"),EditorStyles.boldLabel);
        if(character.General.IsMonster)DrawMenuSize(visuals,"LobbySizePercent",BalanceText.L("Main lobby size (%)","Tamaño en lobby principal (%)"),character.Visuals.GetLobbyScale()*100);
        DrawMenuSize(visuals,"BriefSizePercent",BalanceText.L("Brief / selection size (%)","Tamaño en maletín / selección (%)"),character.Visuals.GetBriefScale()*100);
        EditorGUILayout.HelpBox(BalanceText.L(
            "100% keeps the current base size. Enter any positive percentage. Lobby, Brief and in-game size are independent; the pedestal preview shows the selected context.",
            "100 % conserva el tamaño base actual. Introduce cualquier porcentaje positivo. Lobby, maletín y partida son independientes; la vista del pedestal muestra el contexto seleccionado."),MessageType.None);
    }
    static void DrawMenuSize(SerializedProperty visuals,string key,string label,float displayed)
    {
        var property=visuals.FindPropertyRelative(key);if(property==null)return;
        using(new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();float chosen=EditorGUILayout.FloatField(label,displayed);
            if(EditorGUI.EndChangeCheck()&&chosen>0&&!float.IsNaN(chosen)&&!float.IsInfinity(chosen))property.floatValue=chosen;
            if(GUILayout.Button(BalanceText.L("Reset","Restablecer"),GUILayout.Width(88)))property.floatValue=100;
        }
    }
    void DrawInGameSize()
    {
        EditorGUILayout.LabelField(BalanceText.L("Character size in game","Tamaño del personaje en partida"),EditorStyles.boldLabel);
        var size=serializedObject.FindProperty("Visuals")?.FindPropertyRelative("InGameSizePercent");
        var art=((CharacterDefinition)target).Visuals;
        if(size==null||art==null) {
            EditorGUILayout.HelpBox(BalanceText.L("Assign the character's visuals in Assets first.","Asigna primero los recursos visuales del personaje en Recursos."),MessageType.Info);
            return;
        }
        float percent=art.GetInGameSizePercent(MonsterPouchBalanceIdlePreview.BoardTileSize.x);
        EditorGUI.BeginChangeCheck();
        float chosen=EditorGUILayout.Slider(BalanceText.L("Size (%)","Tamaño (%)"),percent,0,100);
        if(EditorGUI.EndChangeCheck())size.floatValue=chosen;
        using(new EditorGUILayout.HorizontalScope()) {
            GUILayout.Label(BalanceText.L("0% · minimum","0% · mínimo"),EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(BalanceText.L("100% · 2 tiles","100% · 2 baldosas"),EditorStyles.miniLabel);
        }
        if(GUILayout.Button(BalanceText.L("Original size","Tamaño original")))size.floatValue=-1;
        EditorGUILayout.HelpBox(BalanceText.L(
            "0% is a visible minimum (0.4 tiles wide); 100% is 2 tiles wide. Proportions are preserved. Save, then restart Play to see the change on the board.",
            "0% es un mínimo visible (0,4 baldosas de ancho); 100% son 2 baldosas de ancho. Conserva las proporciones. Guarda y vuelve a iniciar Play para verlo en el tablero."),MessageType.None);
    }
    void DrawNested(UnityEngine.Object asset,CharacterDefinition c)
    {
        if(asset==null){EditorGUILayout.HelpBox(BalanceText.L("Missing configuration.","Falta configuración."),MessageType.Error);return;}
        CreateCachedEditor(asset,null,ref nested);
        if(nested is MonsterPouchAbilityEditor a)a.OwnerSource=c;
        nested.OnInspectorGUI();
    }
    void OnDisable(){if(nested!=null)DestroyImmediate(nested);}
}
public sealed class MonsterPouchBalanceEditor:EditorWindow
{
    CharacterDefinition selected;CharacterDefinition[] characters=Array.Empty<CharacterDefinition>();Editor editor;
    readonly MonsterPouchBalanceIdlePreview idlePreview=new MonsterPouchBalanceIdlePreview();
    Vector2 listScroll,detailScroll;int filter;string search="";bool dirty=true;
    [MenuItem("Monster Pouch/Balance Editor")]
    public static void Open(){var w=GetWindow<MonsterPouchBalanceEditor>("Monster Pouch — Balance");w.minSize=new Vector2(1040,640);}
    void OnEnable(){EditorApplication.projectChanged+=Invalidate;Undo.undoRedoPerformed+=Invalidate;EditorApplication.update+=AnimatePreview;}
    void AnimatePreview(){if(idlePreview.NeedsRepaint())Repaint();}
    void Invalidate(){dirty=true;idlePreview.Invalidate();Repaint();}
    public static CharacterDefinition[] FindEditableCharacters(MatchConfig config)
    {
        const string root="Assets/Resources/MonsterPouch/Characters";
        var imported=AssetDatabase.IsValidFolder(root)
            ?AssetDatabase.FindAssets("t:CharacterDefinition",new[]{root})
                .Select(guid=>AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            :Enumerable.Empty<CharacterDefinition>();
        // Discover actual editable assets independently of runtime activation.
        // Keep configured assets from other folders, without creating or enrolling any.
        return (config?.Characters??Array.Empty<CharacterDefinition>()).Concat(imported)
            .Where(c=>c!=null&&c.General!=null).Distinct()
            .OrderByDescending(c=>c.General.IsMonster).ThenBy(c=>c.General.DisplayName,StringComparer.OrdinalIgnoreCase)
            .ThenBy(c=>c.Id,StringComparer.Ordinal).ToArray();
    }
    void Reload()
    {
        characters=FindEditableCharacters(Resources.Load<MatchConfig>("MonsterPouch/MatchConfig"));
        if(selected==null||!characters.Contains(selected))selected=characters.FirstOrDefault();dirty=false;
    }
    void OnGUI()
    {
        if(dirty)Reload();
        using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
            GUILayout.Label("MONSTER POUCH  /  BALANCE",EditorStyles.boldLabel);GUILayout.FlexibleSpace();
            if(GUILayout.Button(BalanceText.L("Save changes","Guardar cambios"),EditorStyles.toolbarButton))AssetDatabase.SaveAssets();
        }
        BalanceText.Language();
        using(new EditorGUILayout.HorizontalScope()) {
            using(new EditorGUILayout.VerticalScope(GUILayout.Width(208))) {
                filter=GUILayout.Toolbar(filter,new[]{BalanceText.L("All","Todos"),CharacterCatalogLabels.Group(true,BalanceText.Spanish),CharacterCatalogLabels.Group(false,BalanceText.Spanish)});
                search=EditorGUILayout.TextField(search,GUI.skin.FindStyle("ToolbarSearchTextField")??EditorStyles.textField);
                listScroll=EditorGUILayout.BeginScrollView(listScroll);
                foreach(var c in characters) {
                    if(filter>0&&c.General.IsMonster!=(filter==1))continue;
                    string kind=CharacterCatalogLabels.Kind(c.Id,c.General.IsMonster,BalanceText.Spanish);
                    if(!string.IsNullOrWhiteSpace(search)&&c.General.DisplayName.IndexOf(search,StringComparison.OrdinalIgnoreCase)<0&&c.Id.IndexOf(search,StringComparison.OrdinalIgnoreCase)<0&&kind.IndexOf(search,StringComparison.OrdinalIgnoreCase)<0)continue;
                    bool was=selected==c;
                    if(GUILayout.Toggle(was,c.General.DisplayName,GUI.skin.button,GUILayout.Height(34))&&!was){selected=c;detailScroll=Vector2.zero;}
                }
                EditorGUILayout.EndScrollView();EditorGUILayout.LabelField(characters.Length+" "+BalanceText.L("characters","personajes"),EditorStyles.miniLabel);
            }
            using(new EditorGUILayout.VerticalScope()) {
                if(selected==null){EditorGUILayout.HelpBox(BalanceText.L("No editable character assets found.","No se encontraron recursos de personaje editables."),MessageType.Warning);return;}
                EditorGUILayout.LabelField(selected.General.DisplayName,EditorStyles.largeLabel);
                EditorGUILayout.LabelField(CharacterCatalogLabels.Group(selected.General.IsMonster,BalanceText.Spanish)+" / "+CharacterCatalogLabels.Kind(selected.Id,selected.General.IsMonster,BalanceText.Spanish)+" · ID: "+selected.Id,EditorStyles.miniLabel);
                detailScroll=EditorGUILayout.BeginScrollView(detailScroll);
                float previousLabelWidth=EditorGUIUtility.labelWidth;
                try {
                    EditorGUIUtility.labelWidth=Mathf.Clamp((position.width-420)*.48f,180,320);
                    Editor.CreateCachedEditor(selected,null,ref editor);editor.OnInspectorGUI();
                } finally {EditorGUIUtility.labelWidth=previousLabelWidth;}
                EditorGUILayout.EndScrollView();
            }
            idlePreview.Draw(selected,position.height);
        }
    }
    void OnDisable(){EditorApplication.projectChanged-=Invalidate;Undo.undoRedoPerformed-=Invalidate;EditorApplication.update-=AnimatePreview;idlePreview.Dispose();if(editor!=null)DestroyImmediate(editor);}
}

