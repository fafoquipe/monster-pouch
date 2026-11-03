using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Uses the existing BalanceText language preference and native serialized fields.
public static class CharacterEditorVisualLabels
{
    static readonly Dictionary<string,string[]> Names=new Dictionary<string,string[]> {
        {"Visuals",new[]{"Animations and visuals","Animaciones y aspecto"}},
        {"Body",new[]{"Body","Cuerpo"}},{"LeftFoot",new[]{"Left foot","Pie izquierdo"}},{"RightFoot",new[]{"Right foot","Pie derecho"}},
        {"BodyOffset",new[]{"Body offset","Desplazamiento del cuerpo"}},{"LeftFootOffset",new[]{"Left foot offset","Desplazamiento del pie izquierdo"}},{"RightFootOffset",new[]{"Right foot offset","Desplazamiento del pie derecho"}},
        {"Idle",new[]{"Idle frames","Fotogramas de reposo"}},{"Move",new[]{"Movement frames","Fotogramas de movimiento"}},
        {"HopLeft",new[]{"Left-foot hop","Salto con el pie izquierdo"}},{"HopRight",new[]{"Right-foot hop","Salto con el pie derecho"}},
        {"OpeningJump",new[]{"Opening jump","Salto inicial"}},{"Tunnel",new[]{"Tunnel frames","Fotogramas de túnel"}},
        {"Attack",new[]{"Attack frames","Fotogramas de ataque"}},{"Death",new[]{"Death frames","Fotogramas de muerte"}},
        {"Revive",new[]{"Revival frames","Fotogramas de resurrección"}},{"Special",new[]{"Super frames","Fotogramas de súper"}},
        {"SpecialFlipX",new[]{"Mirror Super horizontally","Reflejar la súper horizontalmente"}},{"SpecialDuration",new[]{"Super duration (s)","Duración de la súper (s)"}},
        {"IdleFramesPerSecond",new[]{"Idle frames per second","Fotogramas de reposo por segundo"}},{"MoveFramesPerSecond",new[]{"Movement frames per second","Fotogramas de movimiento por segundo"}},
        {"AttackContactFrame",new[]{"Attack contact frame (from 0)","Fotograma de impacto (desde 0)"}},
        {"AttackLungeDistance",new[]{"Attack visual advance","Avance visual del ataque"}},{"AttackImpactSquash",new[]{"Attack impact compression","Compresión visual del impacto"}},
        {"SpecialLungeDistance",new[]{"Super visual advance","Avance visual de la súper"}},{"SpecialLift",new[]{"Super visual lift","Elevación visual de la súper"}},
        {"SpecialPunchScale",new[]{"Super impact scale","Escala visual del impacto de la súper"}},{"SpecialReleasePoint",new[]{"Super release point (0–1)","Instante de lanzamiento de la súper (0–1)"}},
        {"FlipX",new[]{"Mirror horizontally","Reflejar horizontalmente"}},{"MoveFlipX",new[]{"Mirror movement only","Reflejar solo el movimiento"}},
        {"Id",new[]{"ID","ID"}},{"TravelMotion",new[]{"Travel motion","Movimiento de desplazamiento"}},
        {"WholeBodyHopUsesPoses",new[]{"Use authored hop poses","Usar poses dibujadas de salto"}},{"AuthoredBodyMotion",new[]{"Use full-body attack frames","Usar fotogramas de ataque de cuerpo completo"}},
        {"IdleDirections",new[]{"Idle views","Vistas de reposo"}},{"AttackDirections",new[]{"Attack views","Vistas de ataque"}},
        {"WalkRigs",new[]{"Walking parts by direction","Partes de marcha por dirección"}},{"DirectionalAnimations",new[]{"Animations by direction","Animaciones por dirección"}},
        {"Portrait",new[]{"Portrait","Retrato"}},{"WorldWidth",new[]{"World width","Ancho en el mundo"}},
        {"DisplayScale",new[]{"Display scale","Escala visual"}},{"InGameSizePercent",new[]{"In-game size (%)","Tamaño en partida (%)"}},
        {"ReferencePixelWidth",new[]{"Reference width (pixels)","Ancho de referencia (píxeles)"}},
        {"FootPivot",new[]{"Foot pivot","Pivote de los pies"}},{"VitalsHeadAnchor",new[]{"Health bar head anchor","Anclaje de la barra sobre la cabeza"}},
        {"ProvisionalDirectionalProjection",new[]{"Provisional directional projection","Proyección direccional provisional"}},
        {"AbilityAnimationDuration",new[]{"Ability animation duration (s)","Duración de animación de habilidad (s)"}},
        {"HitFlashDuration",new[]{"Hit flash duration (s)","Duración del destello de impacto (s)"}},
        {"AbilityPulseDuration",new[]{"Ability pulse duration (s)","Duración del pulso de habilidad (s)"}},{"AbilityPulseScale",new[]{"Ability pulse scale","Escala del pulso de habilidad"}},
        {"MoveEase",new[]{"Movement easing","Suavizado del movimiento"}},{"MoveArcHeight",new[]{"Movement arc height","Altura del arco de movimiento"}},
        {"WalkWeight",new[]{"Walking weight transfer","Transferencia de peso al caminar"}},{"SuperReadyPulseScale",new[]{"Super-ready pulse scale","Escala del pulso de súper lista"}},
        {"LobbySizePercent",new[]{"Main lobby size (%)","Tamaño en lobby principal (%)"}},
        {"BriefSizePercent",new[]{"Brief / selection size (%)","Tamaño en maletín / selección (%)"}},
        {"SourceNotes",new[]{"Source notes","Notas de origen"}}
    };
    static readonly Dictionary<string,string[]> Help=new Dictionary<string,string[]> {
        {"TravelMotion",new[]{"Visual travel only; flying grants no combat protection.","Solo desplazamiento visual; volar no concede protección en combate."}},
        {"WholeBodyHopUsesPoses",new[]{"Uses authored launch, air and landing poses for whole-body hops.","Usa las poses dibujadas de despegue, vuelo y aterrizaje del salto de cuerpo completo."}},
        {"DisplayScale",new[]{"Preserves body proportions and foot anchoring.","Conserva las proporciones del cuerpo y el anclaje de los pies."}},
        {"WorldWidth",new[]{"Visible width of the front idle pose in world units.","Ancho visible de la pose de reposo frontal en unidades del mundo."}},
        {"AbilityAnimationDuration",new[]{"Zero uses each clip's duration. Movement retains its combat timing.","Cero usa la duración de cada clip. El desplazamiento conserva sus tiempos de combate."}},
        {"MoveFlipX",new[]{"Mirrors movement relative to the main mirror setting; other clips keep their facing.","Refleja el movimiento respecto al reflejo principal; los demás clips conservan su orientación."}},
        {"SourceNotes",new[]{"Original author notes; editing this text does not alter combat.","Notas originales del autor; editar este texto no modifica el combate."}}
    };
    public static string Label(string key,bool spanish)=>Names.TryGetValue(key,out var value)?value[spanish?1:0]:null;
    static bool Directions(string key)=>key=="IdleDirections"||key=="AttackDirections"||key=="WalkRigs"||key=="DirectionalAnimations";
    static string Direction(int i)
    {
        string[] es={"Atrás","Atrás · derecha","Derecha","Frente · derecha","Frente","Frente · izquierda","Izquierda","Atrás · izquierda"};
        string[] en={"Back","Back · right","Right","Front · right","Front","Front · left","Left","Back · left"};
        return i<8?(BalanceText.Spanish?es:en)[i]:BalanceText.L("Direction ","Dirección ")+(i+1);
    }
    static string EnumLabel(string key)=>!BalanceText.Spanish?ObjectNames.NicifyVariableName(key):
        key=="Legacy"?"Heredado":key=="AlternatingHop"?"Saltos alternos":key=="Flying"?"Vuelo":key=="WholeBodyHop"?"Salto de cuerpo completo":key;
    public static void Draw(SerializedProperty property,string elementLabel=null)
    {
        // Context sizing has dedicated, role-aware controls in the Stats tab.
        if(property.name=="LobbySizePercent"||property.name=="BriefSizePercent")return;
        string name=elementLabel??Label(property.name,BalanceText.Spanish)??BalanceText.Field(property.name);
        string help=Help.TryGetValue(property.name,out var pair)?pair[BalanceText.Spanish?1:0]:
            BalanceText.Spanish?name:property.tooltip;
        var label=new GUIContent(name,help);
        if(property.isArray&&property.propertyType!=SerializedPropertyType.String)
        {
            property.isExpanded=EditorGUILayout.Foldout(property.isExpanded,label,true);
            if(!property.isExpanded)return;
            using(new EditorGUI.IndentLevelScope())
            {
                property.arraySize=Mathf.Max(0,EditorGUILayout.IntField(BalanceText.L("Count","Cantidad"),property.arraySize));
                for(int i=0;i<property.arraySize;i++)Draw(property.GetArrayElementAtIndex(i),Directions(property.name)?Direction(i):BalanceText.L("Frame ","Fotograma ")+(i+1));
            }
        }
        else if(property.propertyType==SerializedPropertyType.Generic)
        {
            property.isExpanded=EditorGUILayout.Foldout(property.isExpanded,label,true);
            if(!property.isExpanded)return;
            using(new EditorGUI.IndentLevelScope())
            {
                var child=property.Copy();var end=property.GetEndProperty();
                if(child.NextVisible(true))do {if(SerializedProperty.EqualContents(child,end))break;Draw(child);}while(child.NextVisible(false));
            }
        }
        else if(property.propertyType==SerializedPropertyType.Enum)
            property.enumValueIndex=EditorGUILayout.Popup(label,property.enumValueIndex,property.enumNames.Select(EnumLabel).ToArray());
        // The native Header attribute on this field is English; draw its existing
        // non-negative float directly with the translated label and help instead.
        else if(property.name=="AbilityAnimationDuration")property.floatValue=Mathf.Max(0,EditorGUILayout.FloatField(label,property.floatValue));
        else EditorGUILayout.PropertyField(property,label,false);
    }
}
