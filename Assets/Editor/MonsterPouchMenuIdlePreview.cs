using System;
using System.Linq;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Uses the same body/shadow mesh and context-size policy as menu figures.
public sealed class MonsterPouchMenuIdlePreview:IDisposable
{
    GameObject root;
    LobbyFrameGraphic body;
    LobbyContactShadow shadow;
    public void Dispose(){if(root!=null)Object.DestroyImmediate(root);root=null;}
    void Ensure()
    {
        if(root!=null)return;
        root=new GameObject("Editor menu idle",typeof(RectTransform)){hideFlags=HideFlags.HideAndDontSave};
        ((RectTransform)root.transform).pivot=new Vector2(.5f,0);
        shadow=Child<LobbyContactShadow>("Contact shadow");
        body=Child<LobbyFrameGraphic>("Original idle");
    }
    T Child<T>(string name) where T:Graphic
    {
        var child=new GameObject(name,typeof(RectTransform)){hideFlags=HideFlags.HideAndDontSave};
        var rect=(RectTransform)child.transform;rect.SetParent(root.transform,false);
        rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        return child.AddComponent<T>();
    }
    public void Draw(CharacterDefinition character,Rect canvas,bool lobby)
    {
        if(Event.current.type!=EventType.Repaint||character==null)return;
        Ensure();
        bool hero=character.General.IsMonster;
        var context=lobby?LobbyArtworkContext.BattleHome:LobbyArtworkContext.Standard;
        string surface=lobby?"Choose featured Monster":hero?"deck-monster":"deck-slot";
        var baseline=lobby?new Rect(0,0,200,230):hero?new Rect(0,0,134,105):new Rect(0,0,90,104);
        var frame=CharacterMenuFigureLayout.Frame(baseline,character.Id,character.Visuals,true,surface);
        ((RectTransform)root.transform).sizeDelta=frame.size;
        bool configured=body.Configure(character.Id,false,context,useLobbyView:true);
        float viewScale=Mathf.Min(canvas.width/320f,(canvas.height-48)/270f);
        var feet=new Vector2(canvas.width*.5f,canvas.height-44);
        GUI.BeginGroup(canvas);
        try
        {
            DrawPedestal(feet,viewScale,lobby,hero);
            if(!configured){GUI.Label(new Rect(0,0,canvas.width,canvas.height),BalanceText.L("No full idle assigned","Sin cuerpo completo en reposo"),EditorStyles.centeredGreyMiniLabel);return;}
            body.SetAllDirty();body.Rebuild(CanvasUpdate.PreRender);
            shadow.SetAllDirty();shadow.Rebuild(CanvasUpdate.PreRender);
            DrawShadow(shadow.canvasRenderer.GetMesh(),feet,viewScale);
            DrawBody(body.canvasRenderer.GetMesh(),feet,viewScale);
        }
        finally {GUI.EndGroup();}
    }
    Vector2 Position(Transform owner,Vector3 point,Vector2 feet,float scale)
    {
        Vector3 local=root.transform.InverseTransformPoint(owner.TransformPoint(point));
        return feet+new Vector2(local.x,-local.y)*scale;
    }
    void DrawBody(Mesh mesh,Vector2 feet,float scale)
    {
        if(mesh==null||mesh.vertexCount!=4||body.CurrentSprite==null)return;
        var vertices=mesh.vertices.Select(v=>Position(body.transform,v,feet,scale)).ToArray();var uv=mesh.uv;
        Rect target=Rect.MinMaxRect(vertices.Min(v=>v.x),vertices.Min(v=>v.y),vertices.Max(v=>v.x),vertices.Max(v=>v.y));
        Rect texture=Rect.MinMaxRect(uv.Min(v=>v.x),uv.Min(v=>v.y),uv.Max(v=>v.x),uv.Max(v=>v.y));
        Color previous=GUI.color;var colors=mesh.colors32;if(colors.Length>0)GUI.color=colors[0];
        GUI.DrawTextureWithTexCoords(target,body.CurrentSprite.texture,texture,true);GUI.color=previous;
    }
    void DrawShadow(Mesh mesh,Vector2 feet,float scale)
    {
        if(mesh==null||mesh.vertexCount==0)return;
        var vertices=mesh.vertices;var triangles=mesh.triangles;var colors=mesh.colors32;
        Color previous=Handles.color;
        for(int i=0;i<triangles.Length;i+=3)
        {
            int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
            Handles.color=colors.Length==vertices.Length?((Color)colors[a]+colors[b]+colors[c])/3: new Color(0,0,0,.2f);
            Handles.DrawAAConvexPolygon(Position(shadow.transform,vertices[a],feet,scale),Position(shadow.transform,vertices[b],feet,scale),Position(shadow.transform,vertices[c],feet,scale));
        }
        Handles.color=previous;
    }
    static void DrawPedestal(Vector2 feet,float scale,bool lobby,bool hero)
    {
        Sprite sprite;Rect target;
        if(lobby)
        {
            var artwork=LobbyArtwork.Find(LobbyThemeSelection.Id);sprite=artwork?.Pedestal;
            target=artwork!=null?artwork.PedestalFrame(Vector2.zero):default;
        }
        else
        {
            var skin=Resources.Load<LobbyFurnitureDefinition>("MonsterPouch/Styles/Furniture/"+(CardTableTheme.LobbyDefinition?.Id??"classic"))
                ??Resources.Load<LobbyFurnitureDefinition>("MonsterPouch/Styles/LobbyFurniture");
            sprite=skin?.Pedestal;float width=hero?(skin?.FeaturedPedestalWidth??286):110;
            float height=sprite!=null?width*sprite.rect.height/sprite.rect.width:0;
            target=new Rect(-width*.5f,-height*(skin?.PedestalFootHeight??.24f),width,height);
        }
        if(sprite==null)return;
        target=new Rect(feet+target.position*scale,target.size*scale);
        var uv=UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
        GUI.DrawTextureWithTexCoords(target,sprite.texture,Rect.MinMaxRect(uv.x,uv.y,uv.z,uv.w),true);
    }
}
