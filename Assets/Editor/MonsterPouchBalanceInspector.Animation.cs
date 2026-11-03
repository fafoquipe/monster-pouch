using System;
using System.Globalization;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;

public sealed partial class MonsterPouchBalanceInspector
{
    public static int[] CharacterDisplayOrder(SerializedProperty units)
        =>Enumerable.Range(0,units.arraySize)
            .OrderByDescending(i=>units.GetArrayElementAtIndex(i).FindPropertyRelative("IsMonster").boolValue)
            .ThenBy(i=>units.GetArrayElementAtIndex(i).FindPropertyRelative("DisplayName").stringValue,StringComparer.Create(CultureInfo.GetCultureInfo("es"),true))
            .ThenBy(i=>units.GetArrayElementAtIndex(i).FindPropertyRelative("Id").stringValue,StringComparer.Ordinal).ToArray();
    public static string CharacterMenuLabel(SerializedProperty unit)
        =>CharacterCatalogLabels.Group(unit.FindPropertyRelative("IsMonster").boolValue,BalanceText.Spanish)+"/"+
            CharacterCatalogLabels.Kind(unit.FindPropertyRelative("Id").stringValue,unit.FindPropertyRelative("IsMonster").boolValue,BalanceText.Spanish)+"/"+
            unit.FindPropertyRelative("DisplayName").stringValue;

    public static bool IsAnimationParameter(string effect,string key)
        =>key=="castDuration"||key=="holdDuration"||key=="recoveryDuration"||key=="animationDuration"||
            key=="travelDuration"||key=="pullDuration"||(effect=="aky-opening-flight"&&key=="duration");

    public static bool HasOwnAnimation(TrickDefinition ability)
        =>ability!=null&&!AbilityTiming.UsesBasicAttackAnimation(ability.EffectId)&&
            (ability.EffectId=="anuik-revive"||AbilityBalance.Resolve(ability).Any(p=>IsAnimationParameter(ability.EffectId,p.Key)));

    void DrawAbilityAnimation(SerializedProperty ability,SerializedProperty unit)
    {
        if(!HasOwnAnimation(ReadDescriptionEffect(ability)))return;
        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("Animación visual de esta habilidad",EditorStyles.boldLabel);
        var effect=ReadDescriptionEffect(ability);var owner=ReadDescriptionOwner(unit);
        var parameters=AbilityBalance.Resolve(effect).Where(p=>IsAnimationParameter(effect.EffectId,p.Key)).ToArray();
        foreach(var parameter in parameters)DrawAnimationParameter(ability,effect.EffectId,parameter);
        if(effect.EffectId=="anuik-revive")Draw(unit,"ReviveDelay","Duración de resurrección (s)");
        if(presentationCatalog==null)presentationCatalog=Resources.Load<UnitArtCatalog>("MonsterPouch/UnitArt");
        float fallback=presentationCatalog?.Get(owner.Id)?.GetAbilityAnimationDuration(UnitFacing.South)??.65f;
        // Re-read pending values so the total updates immediately, before ApplyModifiedProperties.
        EditorGUILayout.HelpBox(AnimationSummary(ReadDescriptionEffect(ability),ReadDescriptionOwner(unit),fallback),MessageType.None);
        if(parameters.Any(p=>p.Key=="castDuration"))
            EditorGUILayout.LabelField("Preparación controla la animación y cuándo se suelta la habilidad.",EditorStyles.wordWrappedMiniLabel);
        if(parameters.Any(p=>p.Key=="animationDuration"))
            EditorGUILayout.LabelField("0 conserva el clip de respaldo; un valor mayor cambia solo esta animación.",EditorStyles.wordWrappedMiniLabel);
    }

    void DrawAnimationParameter(SerializedProperty ability,string effect,AbilityParameter fallback)
    {
        var parameters=ability.FindPropertyRelative("Parameters");SerializedProperty stored=null;
        for(int i=0;i<parameters.arraySize;i++)
        {
            var candidate=parameters.GetArrayElementAtIndex(i);
            if(candidate.FindPropertyRelative("Key").stringValue==fallback.Key){stored=candidate.FindPropertyRelative("Value");break;}
        }
        string label=fallback.Key=="castDuration"?"Preparación hasta el golpe (s)":
            fallback.Key=="holdDuration"?"Mantener pose de impacto (s)":
            fallback.Key=="recoveryDuration"?"Regreso al estado normal (s)":
            fallback.Key=="animationDuration"?"Duración visual (s)":
            effect=="popow-dash"&&fallback.Key=="travelDuration"?"Recorrido de embestida (s)":AbilityParameterLabel(effect,fallback.Key);
        string tooltip=fallback.Key=="castDuration"?"0 sin preparación; -1 usa la preparación del ataque básico.":
            fallback.Key=="animationDuration"?"0 conserva el clip de respaldo. El valor solo afecta esta habilidad.":"Tiempo en segundos. Los recorridos se sincronizan con el combate.";
        EditorGUI.BeginChangeCheck();
        float value=EditorGUILayout.FloatField(new GUIContent(label,tooltip),stored?.floatValue??fallback.Value);
        if(!EditorGUI.EndChangeCheck()||float.IsNaN(value)||float.IsInfinity(value))return;
        if(stored==null)
        {
            int index=parameters.arraySize;parameters.InsertArrayElementAtIndex(index);
            var added=parameters.GetArrayElementAtIndex(index);added.FindPropertyRelative("Key").stringValue=fallback.Key;
            stored=added.FindPropertyRelative("Value");
        }
        stored.floatValue=Mathf.Max(fallback.Key=="castDuration"?-1:0,value);
    }

    public static string AnimationSummary(TrickDefinition effect,UnitDefinition owner,float fallback)
    {
        if(effect==null)return "Sin animación propia.";
        var parameters=AbilityBalance.Resolve(effect);
        float V(string key,float missing=0)=>parameters.FirstOrDefault(p=>p.Key==key)?.Value??missing;
        string S(float seconds)=>seconds.ToString("0.##",CultureInfo.GetCultureInfo("es"))+" s";
        float Tick(float value)=>AbilityTiming.Ticks(value)*CombatSimulation.TickDuration;
        string id=effect.EffectId??"";
        bool phased=parameters.Any(p=>p.Key=="castDuration");
        float cast=Tick(V("castDuration")<0?Mathf.Max(.1f,owner?.AttackWindup??.3f):V("castDuration"));
        float hold=V("holdDuration"),recovery=V("recoveryDuration");
        if(id=="anuik-revive"||id=="anuik-second-revive")
            return "Duración visual de resurrección: "+S(Tick(Mathf.Max(.1f,owner?.ReviveDelay??.8f)))+
                (id=="anuik-second-revive"?". Comparte el tiempo de Habilidad propia.":". La unidad revive al terminar.");
        if(id=="popow-dash"||id=="aky-opening-flight")
            return "Preparación: "+S(cast)+". Recorrido visual: "+S(V(id=="popow-dash"?"travelDuration":"duration"))+
                ". Total hasta llegar: "+S(cast+V(id=="popow-dash"?"travelDuration":"duration"))+". La pose de movimiento sigue el recorrido.";
        if(id=="tokoro-ice-cream")return "Recorrido visual de los helados: "+S(Tick(V("travelDuration")))+". Se recogen al llegar a sus casillas.";
        if(id=="hymay-backstep")return "Duración visual de la voltereta: "+S(V("travelDuration"))+". Coincide con el desplazamiento.";
        if(phased)
        {
            // Atong's contact pose uses the normal attack recovery if both custom phases are zero.
            if(id=="atong-critical"&&hold==0&&recovery==0)
                recovery=Mathf.Max(.18f,Mathf.Clamp((owner?.AttackInterval??1)-cast-.02f,.01f,.18f));
            string summary="Duración visual total: "+S(cast+hold+recovery)+". Preparación "+S(cast)+" + pose "+S(hold)+" + regreso "+S(recovery)+".";
            if(id=="hymay-pull")summary+=" El objetivo tarda "+S(V("pullDuration"))+" en completar el arrastre después del impacto.";
            if(id=="atong-critical"&&hold==0&&V("recoveryDuration")==0)summary+=" El regreso usa el ataque básico.";
            return summary;
        }
        if(parameters.Any(p=>p.Key=="animationDuration"))
            return "Duración visual actual: "+S(V("animationDuration")>0?V("animationDuration"):fallback)+(V("animationDuration")==0?" (clip de respaldo).":".");
        if(owner?.BaseAbility!=null&&owner.BaseAbility.EffectId!=id&&SharesBaseAnimation(id))
            return "Comparte la animación de Habilidad propia.\n"+AnimationSummary(owner.BaseAbility,owner,fallback);
        return "Pasiva o modificación de otro efecto: no añade una animación propia. Conserva la animación del ataque o habilidad que la activa.";
    }

    static bool SharesBaseAnimation(string effect)
    {
        switch(effect)
        {
            case "bugaloo-protection":case "bugaloo-revenge":case "popow-dash-stun":case "popow-dash-charge":
            case "sepora-multishot":case "tauris-sweep":case "tauris-hunger":case "blotan-fast-income":case "blotan-health":
            case "flo-flower-heal":case "flo-flower-invulnerability":case "gochan-burn":case "jazar-intense-radiation":
            case "kayon-double-summon":case "stein-long-stun":case "stein-triple":case "trimol-infinite-boulder":
            case "trimol-brutal-boulder":case "tsu-death-trap":case "tsu-chocolate":case "tsu-colored-gum":
            case "atong-critical-damage":case "atong-always-critical":case "hymay-focus":case "hymay-weakness":return true;
            default:return false;
        }
    }
}
