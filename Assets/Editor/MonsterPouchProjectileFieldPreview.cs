using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;

// Embedded next to the existing size control, with the same authored cell proportions as the idle view.
// It samples the real flight renderer and owns only hidden, temporary images/procedural textures.
[InitializeOnLoad]
public static class MonsterPouchProjectileFieldPreview
{
    sealed class View : IDisposable
    {
        public ProjectileVisualPlayback Playback;
        public string Error;
        public bool Playing=true,Visible=true;
        public double Started=EditorApplication.timeSinceStartup,LastDraw;
        public float Progress;
        public void Dispose(){Playback?.Dispose();}
    }
    static readonly Dictionary<string,View> views=new Dictionary<string,View>();
    static readonly Dictionary<UnityEngine.Object,CharacterDefinition> owners=new Dictionary<UnityEngine.Object,CharacterDefinition>();
    static double nextRepaint;
    static MonsterPouchProjectileFieldPreview()
    {
        AssemblyReloadEvents.beforeAssemblyReload+=Clear;
        EditorApplication.quitting+=Clear;
        EditorApplication.projectChanged+=Clear;
        EditorApplication.playModeStateChanged+=_=>Clear();
        EditorApplication.update+=Update;
    }
    static void Update()
    {
        double now=EditorApplication.timeSinceStartup;
        foreach(string key in views.Where(v=>now-v.Value.LastDraw>3).Select(v=>v.Key).ToArray())
        {views[key].Dispose();views.Remove(key);}
        if(now<nextRepaint||!views.Values.Any(v=>v.Visible&&v.Playing&&now-v.LastDraw<.5))return;
        nextRepaint=now+1.0/16;UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }
    static void Clear(){foreach(var view in views.Values)view.Dispose();views.Clear();owners.Clear();}
    public static CharacterDefinition Owner(UnityEngine.Object asset)
    {
        if(owners.Count==0)foreach(var c in Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").Characters) {
            if(c==null)continue;if(c.Attack!=null)owners[c.Attack]=c;if(c.Ability!=null)owners[c.Ability]=c;
            foreach(var a in c.Upgrades??Array.Empty<CharacterAbility>())if(a!=null)owners[a]=c;
        }
        return owners.TryGetValue(asset,out var owner)?owner:null;
    }
    public static ProjectileVisualSlot ReadSlot(SerializedProperty property)
    {
        var slot=new ProjectileVisualSlot {
            VariantId=property.FindPropertyRelative("VariantId").stringValue,
            RendererId=property.FindPropertyRelative("RendererId").stringValue,
            OverrideSize=property.FindPropertyRelative("OverrideSize").boolValue,
            SizePercent=property.FindPropertyRelative("SizePercent").floatValue
        };
        var source=property.FindPropertyRelative("Frames");slot.Frames=new ProjectileVisualFrame[source.arraySize];
        for(int i=0;i<source.arraySize;i++) {
            var frame=source.GetArrayElementAtIndex(i);
            slot.Frames[i]=new ProjectileVisualFrame{Sprite=frame.FindPropertyRelative("Sprite").objectReferenceValue as Sprite,VisibleRect=frame.FindPropertyRelative("VisibleRect").rectValue};
        }
        return slot;
    }
    public static void Draw(SerializedProperty property)
    {
        var slot=ReadSlot(property);var asset=property.serializedObject.targetObject;
        string key=asset.GetInstanceID()+"/"+slot.VariantId+"/"+slot.RendererId;
        if(!views.TryGetValue(key,out var view)) {
            view=new View();views.Add(key,view);
            try {
                var owner=Owner(asset);
                if(owner==null)throw new InvalidOperationException("No authoring character owns this asset.");
                view.Playback=new ProjectileVisualPlayback(owner,asset,slot);
            }catch(Exception e){view.Error=e.Message;}
        }
        view.LastDraw=EditorApplication.timeSinceStartup;
        view.Visible=EditorGUILayout.Foldout(view.Visible,BalanceText.L("See projectile on the field","Ver proyectil en el campo"),true);
        if(!view.Visible)return;
        if(view.Playback==null) {
            EditorGUILayout.HelpBox(BalanceText.L("Flight preview is not registered: ","Falta registrar esta vista de vuelo: ")+view.Error,MessageType.Error);return;
        }
        float duration=view.Playback.Duration;
        if(view.Playing)view.Progress=Mathf.Repeat((float)(EditorApplication.timeSinceStartup-view.Started)/duration,1);
        view.Playback.Sample(slot,view.Progress*duration,view.Progress,50);
        Rect area=GUILayoutUtility.GetRect(230,210,GUILayout.ExpandWidth(true));
        if(Event.current.type==EventType.Repaint)DrawField(area,view.Playback);
        using(new EditorGUILayout.HorizontalScope()) {
            if(GUILayout.Button(view.Playing?BalanceText.L("Pause","Pausar"):BalanceText.L("Play","Reproducir"),GUILayout.Width(90))) {
                view.Playing=!view.Playing;view.Started=EditorApplication.timeSinceStartup-view.Progress*duration;
            }
            EditorGUI.BeginChangeCheck();float progress=GUILayout.HorizontalSlider(view.Progress,0,1);
            if(EditorGUI.EndChangeCheck()){view.Progress=progress;view.Playing=false;}
        }
        EditorGUILayout.LabelField(BalanceText.L("One cell = one board tile · maximum 2 tiles","Una celda = una casilla del campo · máximo 2 casillas"),EditorStyles.miniLabel);
        if(slot.OverrideSize&&slot.ClampedPercent==0)
            EditorGUILayout.LabelField(BalanceText.L("Hidden flight · effects remain active","Vuelo oculto · los efectos siguen activos"),EditorStyles.miniLabel);
    }
    public static void DrawField(Rect area,ProjectileVisualPlayback playback)
    {
        EditorGUI.DrawRect(area,new Color(.065f,.085f,.11f));
        GUI.BeginGroup(area);
        try {
            Vector2 ratio=MonsterPouchBalanceIdlePreview.BoardTileSize;
            float width=Mathf.Min(area.width/5.4f,area.height/3.4f*ratio.x/ratio.y),height=width*ratio.y/ratio.x;
            Vector2 center=new Vector2(area.width*.5f,area.height*.5f);
            for(int y=-1;y<=1;y++)for(int x=-2;x<=2;x++) {
                var rect=new Rect(center.x+(x-.5f)*width,center.y+(y-.5f)*height,width-1,height-1);
                EditorGUI.DrawRect(rect,x==0&&y==0?new Color(.25f,.43f,.39f):(x+y)%2==0?new Color(.16f,.21f,.24f):new Color(.20f,.26f,.29f));
            }
            float scale=width/50;
            for(int i=0;i<playback.Layers.Length;i++)DrawImage(playback.Layers[i],center+new Vector2(playback.Offsets[i].x,-playback.Offsets[i].y)*scale,scale);
        } finally {GUI.EndGroup();}
    }
    static void DrawImage(UnityEngine.UI.Image image,Vector2 center,float scale)
    {
        var sprite=image.sprite;var rect=image.rectTransform;
        Vector2 size=ProjectileVisualGeometry.Fit(rect.sizeDelta,sprite!=null?sprite.rect.size:rect.sizeDelta,image.preserveAspect);
        size=Vector2.Scale(size,rect.localScale)*scale;
        if(size.x<=0||size.y<=0)return;
        Rect target=new Rect(center-size*.5f,size);
        Color previous=GUI.color;Matrix4x4 matrix=GUI.matrix;
        try {
            GUI.color=image.color;GUIUtility.RotateAroundPivot(-rect.localEulerAngles.z,center);
            if(sprite==null)GUI.DrawTexture(target,Texture2D.whiteTexture);
            else {
                Rect pixels=sprite.textureRect;Vector2 offset=sprite.textureRectOffset;
                var crop=new Rect(target.x+offset.x/sprite.rect.width*target.width,
                    target.y+(sprite.rect.height-offset.y-pixels.height)/sprite.rect.height*target.height,
                    pixels.width/sprite.rect.width*target.width,pixels.height/sprite.rect.height*target.height);
                var uv=new Rect(pixels.x/sprite.texture.width,pixels.y/sprite.texture.height,pixels.width/sprite.texture.width,pixels.height/sprite.texture.height);
                GUI.DrawTextureWithTexCoords(crop,sprite.texture,uv,true);
            }
        } finally {GUI.color=previous;GUI.matrix=matrix;}
    }
}
