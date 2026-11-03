using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Uses real idle frames and the authored board dimensions without modifying scenes or assets.
public sealed class MonsterPouchBalanceIdlePreview
{
    UnitArtCatalog catalog;
    CharacterDefinition character;
    UnitArt art;
    UnitDirectionalAnimation clip;
    UnitFacing facing=UnitFacing.South;
    Sprite fallback;
    Vector2 tileSize;
    static Vector2 boardTileSize;
    public static Vector2 BoardTileSize { get { if(boardTileSize==Vector2.zero)ReadTileSize();return boardTileSize; } }
    double started,pausedAt,nextRepaint;
    bool playing=true;
    int menuContext;
    readonly MonsterPouchMenuIdlePreview menuPreview=new MonsterPouchMenuIdlePreview();
    public void Dispose()=>menuPreview.Dispose();

    public bool NeedsRepaint()
    {
        double now=EditorApplication.timeSinceStartup;
        if(!playing||character==null||now<nextRepaint)return false;
        nextRepaint=now+1.0/12;return true;
    }
    public void Invalidate(){character=null;}
    void Select(CharacterDefinition value)
    {
        character=value;
        tileSize=BoardTileSize;
        if(catalog==null)catalog=Resources.Load<UnitArtCatalog>("MonsterPouch/UnitArt");
        art=value?.Visuals??catalog?.Get(value?.Id);
        clip=art?.Animation(facing);
        fallback=art?.Idle(facing)??value?.Portrait;
        started=pausedAt=EditorApplication.timeSinceStartup;
    }
    static void ReadTileSize()
    {
        // Read the authored board without replacing the user's active scene.
        var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/main-scene.unity");
        try {
            var mapper=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<BoardWorldMapper>(true)).FirstOrDefault();
            boardTileSize=mapper!=null?new Vector2(Mathf.Abs(mapper.CellSize.x)*CardTableArena.HorizontalExpansion,Mathf.Abs(mapper.CellSize.y)):Vector2.one;
        } finally {EditorSceneManager.ClosePreviewScene(scene);}
    }
    public void Draw(CharacterDefinition value,float availableHeight=640)
    {
        if(character!=value||(value?.Visuals!=null&&art!=value.Visuals))Select(value);
        using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox,GUILayout.Width(300))) {
            bool hero=value?.General.IsMonster??false;
            if(!hero&&menuContext==1)menuContext=2;
            string[] views=hero?new[]{BalanceText.L("In game","Partida"),BalanceText.L("Lobby","Lobby"),BalanceText.L("Brief","Maletín")}:
                new[]{BalanceText.L("In game","Partida"),BalanceText.L("Brief","Maletín")};
            int selected=GUILayout.Toolbar(hero?menuContext:menuContext==2?1:0,views);
            menuContext=hero?selected:selected==1?2:0;
            if(menuContext!=0)
            {
                bool lobby=menuContext==1;
                EditorGUILayout.LabelField(lobby?BalanceText.L("MAIN LOBBY · PEDESTAL","LOBBY PRINCIPAL · PEDESTAL"):BalanceText.L("BRIEF · PEDESTAL","MALETÍN · PEDESTAL"),EditorStyles.boldLabel);
                var menuCanvas=GUILayoutUtility.GetRect(290,Mathf.Clamp(availableHeight-300,220,340),GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(menuCanvas,new Color(.065f,.085f,.11f));menuPreview.Draw(value,menuCanvas,lobby);
                float menuPercent=(lobby?value.Visuals?.GetLobbyScale()??1:value.Visuals?.GetBriefScale()??1)*100;
                EditorGUILayout.LabelField(menuPercent.ToString("0.##")+"% · "+BalanceText.L("base size","tamaño base"),EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.HelpBox(BalanceText.L("Full idle body with the same size and foot placement as this menu context. The pedestal keeps its own size.","Cuerpo completo en reposo con el mismo tamaño y apoyo de pies que en este contexto. El pedestal conserva su propio tamaño."),MessageType.None);
                return;
            }
            EditorGUILayout.LabelField(BalanceText.L("3 × 3 BOARD · GAME SCALE","TABLERO 3 × 3 · ESCALA DE PARTIDA"),EditorStyles.boldLabel);
            var canvas=GUILayoutUtility.GetRect(290,Mathf.Clamp(availableHeight-300,220,340),GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(canvas,new Color(.065f,.085f,.11f));
            if(Event.current.type==EventType.Repaint) {
                GUI.BeginGroup(canvas);
                try {DrawFrame(new Rect(0,0,canvas.width,canvas.height));}
                finally {GUI.EndGroup();}
            }
            string[] directions=BalanceText.Spanish?new[]{"Atrás","Atrás · derecha","Derecha","Frente · derecha","Frente","Frente · izquierda","Izquierda","Atrás · izquierda"}:
                new[]{"Back","Back · right","Right","Front · right","Front","Front · left","Left","Back · left"};
            var chosen=(UnitFacing)EditorGUILayout.Popup((int)facing,directions);
            if(chosen!=facing){facing=chosen;Select(value);}
            if(GUILayout.Button(playing?BalanceText.L("Pause preview","Pausar vista"):BalanceText.L("Play preview","Reproducir vista"))) {
                double now=EditorApplication.timeSinceStartup;
                if(playing)pausedAt=now;else started+=now-pausedAt;
                playing=!playing;
            }
            EditorGUILayout.LabelField(value?.General.DisplayName??"",EditorStyles.centeredGreyMiniLabel);
            float percent=art?.GetInGameSizePercent(tileSize.x)??0;
            EditorGUILayout.LabelField(percent.ToString("0.#")+"% · "+BalanceText.L("in game","en partida"),EditorStyles.centeredGreyMiniLabel);
            float width=Mathf.Lerp(UnitArt.MinimumWidthInTiles,UnitArt.MaximumWidthInTiles,percent/100);
            EditorGUILayout.LabelField(BalanceText.L("Width: ","Ancho: ")+width.ToString("0.00")+BalanceText.L(" tiles"," baldosas"),EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.HelpBox(BalanceText.L("Same character-to-tile proportion as in game. The highlighted tile is its position.","Misma proporción entre personaje y baldosa que en partida. La baldosa marcada es su posición."),MessageType.None);
        }
    }
    void DrawFrame(Rect canvas)
    {
        float elapsed=(float)((playing?EditorApplication.timeSinceStartup:pausedAt)-started);
        Sprite sprite=fallback;
        var frames=clip?.Idle;
        if(frames!=null&&frames.Length>0) {
            float fps=clip.IdleFramesPerSecond;
            if(float.IsNaN(fps)||float.IsInfinity(fps))fps=1;
            sprite=frames[Mathf.FloorToInt(elapsed*Mathf.Max(.1f,fps))%frames.Length]??fallback;
        }
        if(sprite==null){GUI.Label(canvas,BalanceText.L("No idle assigned","Sin idle asignado"),EditorStyles.centeredGreyMiniLabel);return;}
        var texture=sprite.texture;
        Rect pixels=sprite.textureRect;
        Vector2 offset=sprite.textureRectOffset;
        // One camera for all characters and directions. Never fit the board to
        // sprite/atlas bounds: only the character's actual in-game scale varies.
        // Keep 3 tiles of headroom above the feet and 1.5 tiles below them.
        float worldScale=Mathf.Min(canvas.width/(tileSize.x*3.4f),(canvas.height-24)/(tileSize.y*4.5f));
        var origin=new Vector2(canvas.center.x,canvas.yMax-12-tileSize.y*1.5f*worldScale);
        var tile=tileSize*worldScale;
        for(int row=-1;row<=1;row++)for(int column=-1;column<=1;column++) {
            var rect=new Rect(origin.x+(column-.5f)*tile.x,origin.y+(row-.5f)*tile.y,tile.x-1,tile.y-1);
            bool selected=row==0&&column==0;
            EditorGUI.DrawRect(rect,selected?new Color(.32f,.55f,.47f):(row+column)%2==0?new Color(.16f,.21f,.24f):new Color(.20f,.26f,.29f));
            if(selected) {
                Color edge=new Color(.68f,.87f,.65f);
                EditorGUI.DrawRect(new Rect(rect.x,rect.y,rect.width,1),edge);
                EditorGUI.DrawRect(new Rect(rect.x,rect.yMax-1,rect.width,1),edge);
                EditorGUI.DrawRect(new Rect(rect.x,rect.y,1,rect.height),edge);
                EditorGUI.DrawRect(new Rect(rect.xMax-1,rect.y,1,rect.height),edge);
            }
        }
        var shadow=UnitPresentation.ContactShadowSprite;
        Vector3 shadowScale=UnitPresentation.ContactShadowScale(art,tileSize.x);
        var shadowRect=new Rect(origin.x-shadowScale.x*worldScale*.5f,origin.y+(.035f-shadowScale.y*.5f)*worldScale,
            shadowScale.x*worldScale,shadowScale.y*worldScale);
        Color previous=GUI.color;GUI.color=new Color(0,0,0,.35f);
        GUI.DrawTexture(shadowRect,shadow.texture);GUI.color=previous;
        float pixelScale=worldScale*UnitPresentation.PresentationScale(art,tileSize.x)/sprite.pixelsPerUnit;
        Rect destination=new Rect(origin.x+(offset.x-sprite.pivot.x)*pixelScale,
            origin.y-(offset.y+pixels.height-sprite.pivot.y)*pixelScale,pixels.width*pixelScale,pixels.height*pixelScale);
        Rect uv=new Rect(pixels.x/texture.width,pixels.y/texture.height,pixels.width/texture.width,pixels.height/texture.height);
        if(clip?.FlipX==true){destination.x=2*origin.x-destination.xMax;uv.x+=uv.width;uv.width=-uv.width;}
        GUI.DrawTextureWithTexCoords(destination,texture,uv,true);
    }
}
