using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using UnityEditor;
using UnityEngine;

// Shared by the nested Character ability/trick cards and the legacy balance inspector.
public static class MonsterPouchAbilityAreaEditor
{
    sealed class Preview { public int X=2,Y=4; public bool Red,Wide; }
    static readonly Dictionary<string,Preview> Previews = new Dictionary<string,Preview>();
    static string L(string en,string es) => BalanceText.L(en,es);
    public static bool HidesOriginalParameter(SerializedProperty ability,string key) =>
        AbilityAreaCatalog.IsOriginalGeometryParameter(ability.FindPropertyRelative("EffectId")?.stringValue,key) &&
        (ability.FindPropertyRelative("Area")?.FindPropertyRelative("Shape")?.enumValueIndex??0)!=0;
    public static void Draw(SerializedProperty ability, TrickDefinition definition, UnitDefinition owner)
    {
        string effect = ability.FindPropertyRelative("EffectId")?.stringValue;
        if (!AbilityAreaCatalog.Supports(effect)) return;
        var area = ability.FindPropertyRelative("Area");
        if (area == null) return;
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(L("Effect area","Área del efecto"),EditorStyles.boldLabel);
        var mode=area.FindPropertyRelative("Shape");
        EditorGUI.BeginChangeCheck();
        int selected=EditorGUILayout.Popup(L("Shape","Forma"),mode.enumValueIndex,new[]{
            L("Original (preserve saved area)","Original (conservar área guardada)"),
            L("Square N × N","Cuadrado N × N"),L("Cross · four rectangles","Cruz · cuatro rectángulos")});
        if(EditorGUI.EndChangeCheck())mode.enumValueIndex=selected;
        var shape=(AbilityAreaShape)mode.enumValueIndex;
        if(shape==AbilityAreaShape.Square)Integer(area,"SquareSize",L("Side N (tiles)","Lado N (casillas)"),false);
        if(shape==AbilityAreaShape.Cross)
        {
            Arm(area,"Up",L("Up","Arriba"));Arm(area,"Right",L("Right","Derecha"));
            Arm(area,"Down",L("Down","Abajo"));Arm(area,"Left",L("Left","Izquierda"));
            if(!AbilityAreaCatalog.ExcludesCenter(effect))
                EditorGUILayout.PropertyField(area.FindPropertyRelative("IncludeCenter"),new GUIContent(L("Include center tile","Incluir casilla central")));
            EditorGUILayout.HelpBox(L("Width: odd number, centered at the origin. Depth: any whole number; 0 disables that arm. Overlaps count once.",
                "Anchura: impar, centrada en el origen. Profundidad: cualquier entero; 0 desactiva ese brazo. Los solapes cuentan una sola vez."),MessageType.None);
        }
        if(shape!=AbilityAreaShape.Original)
            EditorGUILayout.HelpBox(L("Fixed board directions: up = +Y, right = +X. No rotation with attacks or teams. Square 0 disables the area; even squares extend one extra tile up and right.",
                "Direcciones fijas del tablero: arriba = +Y, derecha = +X. No gira con ataques ni equipos. Cuadrado 0 desactiva el área; los cuadrados pares se extienden una casilla más arriba y a la derecha."),MessageType.None);
        else EditorGUILayout.HelpBox(L("Keeps the current saved rules, including radius, rounding and purchased upgrades. Choosing a new shape starts empty until you enter its sizes.",
            "Conserva las reglas guardadas, incluidos radio, redondeo y mejoras compradas. Al elegir una forma nueva empieza vacía hasta introducir sus tamaños."),MessageType.None);
        string origin = effect=="flo-flower" || effect=="kayon-dummy-legacy" ? L("Fallen summon","Invocación caída") :
            AbilityAreaCatalog.ImpactOrigin(effect)?L("Impact / selected target tile","Casilla de impacto / objetivo elegido"):L("Caster tile","Casilla del personaje");
        EditorGUILayout.LabelField(L("Origin","Origen"),origin);
        if(AbilityAreaCatalog.SeparatePrimary(effect))EditorGUILayout.HelpBox(L("The separate direct hit or self effect is preserved. This area edits the surrounding effect only.",
            "Se conserva el golpe directo o efecto propio separado. Esta área edita solo el efecto alrededor."),MessageType.None);
        if(AbilityAreaCatalog.ExcludesCenter(effect))EditorGUILayout.LabelField(L("Center excluded by this effect","Este efecto excluye la casilla central"),EditorStyles.miniLabel);
        if(effect=="pokrak-wide-bubbles")EditorGUILayout.HelpBox(L("When purchased, this edited area replaces the complete bubble footprint, including its base area.",
            "Al comprarla, esta área editada sustituye la huella completa de burbujas, incluida el área base."),MessageType.None);

        string previewKey=ability.serializedObject.targetObject.GetInstanceID()+":"+ability.propertyPath;
        if(!Previews.TryGetValue(previewKey,out var preview))Previews[previewKey]=preview=new Preview();
        EditorGUILayout.LabelField(L("Preview · no asset changes","Vista previa · no modifica el recurso"),EditorStyles.miniBoldLabel);
        preview.X=EditorGUILayout.IntSlider(L("Origin column","Columna de origen"),preview.X,0,BoardManager.Width-1);
        preview.Y=EditorGUILayout.IntSlider(L("Origin row","Fila de origen"),preview.Y,0,BoardManager.Height-1);
        if(shape==AbilityAreaShape.Original && effect=="hymay-focus")preview.Red=EditorGUILayout.Toggle(L("Red team preview","Vista del equipo rojo"),preview.Red);
        if(shape==AbilityAreaShape.Original && effect=="pokrak-bubbles")preview.Wide=EditorGUILayout.Toggle(L("With area upgrade","Con mejora de área"),preview.Wide);
        var settings=Read(area);
        if(effect=="pokrak-bubbles" && preview.Wide)
        {
            var wide=owner?.Tricks?.FirstOrDefault(t=>t?.EffectId=="pokrak-wide-bubbles")?.Area;
            if(wide?.IsEdited==true)settings=wide.Copy();
        }
        else if(effect=="pokrak-wide-bubbles" && !settings.IsEdited && owner?.BaseAbility?.Area?.IsEdited==true)
            settings=owner.BaseAbility.Area.Copy();
        var coordinates=settings.IsEdited ? AbilityAreaResolver.Resolve(settings,preview.X,preview.Y,BoardManager.Width,BoardManager.Height) :
            AbilityAreaCatalog.OriginalPreview(effect,(id,key,fallback)=>Value(ability,definition,owner,id,key,fallback),
                id=>id=="pokrak-wide-bubbles"&&preview.Wide,preview.X,preview.Y,preview.Red);
        if(AbilityAreaCatalog.ExcludesCenter(effect))coordinates=coordinates.Where(c=>c.X!=preview.X||c.Y!=preview.Y).ToArray();
        var cells=new HashSet<AbilityAreaCell>(coordinates);
        const float tile=22f;
        Rect bounds=GUILayoutUtility.GetRect(BoardManager.Width*tile,BoardManager.Height*tile,GUILayout.ExpandWidth(false));
        for(int y=0;y<BoardManager.Height;y++)for(int x=0;x<BoardManager.Width;x++)
        {
            var rect=new Rect(bounds.x+x*tile,bounds.y+(BoardManager.Height-1-y)*tile,tile-2,tile-2);
            bool selectedCell=cells.Contains(new AbilityAreaCell(x,y));
            EditorGUI.DrawRect(rect,selectedCell?new Color(.16f,.68f,.86f):new Color(.18f,.19f,.22f));
            if(x==preview.X&&y==preview.Y)GUI.Label(rect,"◎",EditorStyles.centeredGreyMiniLabel);
        }
        EditorGUILayout.LabelField(coordinates.Length+L(" tiles · ◎ origin · edges are clipped"," casillas · ◎ origen · recorte en bordes"),EditorStyles.miniLabel);
        if(settings.IsEdited&&coordinates.Length==0)EditorGUILayout.HelpBox(L("Empty area. No surrounding targets will be affected.","Área vacía. No afectará a objetivos alrededor."),MessageType.Info);
    }
    static void Arm(SerializedProperty area,string key,string label)
    {
        EditorGUILayout.LabelField(label,EditorStyles.miniBoldLabel);
        Integer(area,key+"Width",L("Width (odd)","Anchura (impar)"),true);
        Integer(area,key+"Depth",L("Depth (0 = off)","Profundidad (0 = apagado)"),false);
    }
    static void Integer(SerializedProperty area,string key,string label,bool odd)
    {
        var p=area.FindPropertyRelative(key);EditorGUI.BeginChangeCheck();
        int next=EditorGUILayout.IntField(label,p.intValue);
        if(EditorGUI.EndChangeCheck())p.intValue=odd?(Math.Max(1,next)|1):Math.Max(0,next);
    }
    public static AbilityAreaDefinition Read(SerializedProperty p) => new AbilityAreaDefinition {
        Shape=(AbilityAreaShape)p.FindPropertyRelative("Shape").enumValueIndex,
        SquareSize=p.FindPropertyRelative("SquareSize").intValue,IncludeCenter=p.FindPropertyRelative("IncludeCenter").boolValue,
        UpWidth=p.FindPropertyRelative("UpWidth").intValue,UpDepth=p.FindPropertyRelative("UpDepth").intValue,
        RightWidth=p.FindPropertyRelative("RightWidth").intValue,RightDepth=p.FindPropertyRelative("RightDepth").intValue,
        DownWidth=p.FindPropertyRelative("DownWidth").intValue,DownDepth=p.FindPropertyRelative("DownDepth").intValue,
        LeftWidth=p.FindPropertyRelative("LeftWidth").intValue,LeftDepth=p.FindPropertyRelative("LeftDepth").intValue
    };
    static float Value(SerializedProperty current,TrickDefinition definition,UnitDefinition owner,string effect,string key,float fallback)
    {
        if(current.FindPropertyRelative("EffectId").stringValue==effect)
        {
            var parameters=current.FindPropertyRelative("Parameters");
            for(int i=0;i<parameters.arraySize;i++)
            {
                var entry=parameters.GetArrayElementAtIndex(i);
                if(entry.FindPropertyRelative("Key").stringValue==key)return entry.FindPropertyRelative("Value").floatValue;
            }
        }
        var candidates=new[]{definition,owner?.BaseAbility,owner?.PassiveAbility}.Concat(owner?.Tricks??Array.Empty<TrickDefinition>());
        var found=candidates.FirstOrDefault(a=>a!=null&&a.EffectId==effect);
        var parameter=AbilityBalance.Resolve(found).FirstOrDefault(p=>p.Key==key);
        return parameter?.Value??fallback;
    }
}
