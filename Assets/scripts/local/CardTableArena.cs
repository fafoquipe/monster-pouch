using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using UnityEngine;

namespace MonsterPouch.Local
{
    // Runtime framing preserves the scene's source art and logical board coordinates.
    // Applying it repeatedly never compounds the mapper or sprite scale.
    public sealed partial class CardTableArena : MonoBehaviour
    {
        public const float HorizontalExpansion=1.10f;
        public const float PortraitWidth=540,PortraitHeight=960,BriefTop=744;
        Camera arenaCamera;
        BoardManager board;
        BoardWorldMapper mapper;
        bool applied;
        SpriteRenderer felt,surface;
        Transform hotelRoot;
        readonly Dictionary<string,SpriteRenderer> sceneSprites=new Dictionary<string,SpriteRenderer>(StringComparer.Ordinal);
        readonly Dictionary<SpriteRenderer,bool> hotelVisibility=new Dictionary<SpriteRenderer,bool>();
        readonly Dictionary<SpriteRenderer,Rect> visiblePixels=new Dictionary<SpriteRenderer,Rect>();
        readonly Dictionary<SpriteRenderer,float> referenceWidths=new Dictionary<SpriteRenderer,float>();
        public bool IsApplied=>applied;
        public bool LoungeEnabled { get; private set; }
        public Vector2 OriginalCellSize { get; private set; }
        public Rect OriginalBoardPortraitBounds { get; private set; }
        public Rect BoardPortraitBounds=>ToPortrait(BoardWorldBounds);
        public Rect FramePortraitBounds=>ToPortrait(VisibleWorldBounds(FindSprite("table-frame")));
        public Rect BoardWorldBounds
        {
            get
            {
                if(board==null||mapper==null)return default;
                Vector3 first=mapper.GetWorldPosition(board.GetCell(0,0));
                Vector3 last=mapper.GetWorldPosition(board.GetCell(BoardManager.Width-1,BoardManager.Height-1));
                Vector2 half=new Vector2(Mathf.Abs(mapper.CellSize.x),Mathf.Abs(mapper.CellSize.y))*.5f;
                return Rect.MinMaxRect(Mathf.Min(first.x,last.x)-half.x,Mathf.Min(first.y,last.y)-half.y,
                    Mathf.Max(first.x,last.x)+half.x,Mathf.Max(first.y,last.y)+half.y);
            }
        }

        public static CardTableArena Apply(BoardManager board,BoardWorldMapper mapper,Camera camera)
        {
            if(board==null||mapper==null||camera==null)return null;
            var arena=mapper.GetComponent<CardTableArena>();
            if(arena==null)arena=mapper.gameObject.AddComponent<CardTableArena>();
            if(!arena.applied)arena.Configure(board,mapper,camera);
            return arena;
        }

        void Configure(BoardManager manager,BoardWorldMapper worldMapper,Camera camera)
        {
            board=manager;mapper=worldMapper;arenaCamera=camera;
            if(!board.IsInitialized)board.BuildBoard();
            OriginalCellSize=mapper.CellSize;
            OriginalBoardPortraitBounds=ToPortrait(BoardWorldBounds);
            Vector3 center=(mapper.GetWorldPosition(board.GetCell(0,0))+
                mapper.GetWorldPosition(board.GetCell(BoardManager.Width-1,BoardManager.Height-1)))*.5f;
            camera.orthographic=true;camera.orthographicSize=12.2f;
            camera.transform.position=new Vector3(center.x,.55f,camera.transform.position.z);
            boardCenter=center;
            var anchor=new GameObject("Expanded board origin").transform;boardAnchor=anchor;
            anchor.SetParent(transform,false);anchor.position=center;
            Vector2 expanded=new Vector2(OriginalCellSize.x*HorizontalExpansion,OriginalCellSize.y);
            mapper.Configure(board,anchor,expanded,new Vector2(-(BoardManager.Width-1)*expanded.x*.5f,
                -(BoardManager.Height-1)*expanded.y*.5f));
            // Preserve the authored casino objects. Only its obsolete composite is unused.
            foreach(var item in FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(item.gameObject.scene!=gameObject.scene)continue;
                if(item.name=="casino-board")item.gameObject.SetActive(false);
                if(item.name=="table-surface"||item.name=="table-frame"||item.name=="table-frame-shadow")
                    tablePoses.Add(item,new TablePose(item));
            }
            surface=FindSprite("table-surface");felt=FindSprite("casino-table-path");
            // Alpha bounds were measured read-only from the original 1254² PNGs.
            // They are used only for validation; the scene transforms stay intact.
            RegisterVisibleBounds("table-frame",new Rect(129,60,829,1331),1086);
            RegisterVisibleBounds("clown-tower",new Rect(320,162,617,943));
            RegisterVisibleBounds("shuffler-cart",new Rect(186,193,909,909));
            RegisterVisibleBounds("die-white",new Rect(374,318,505,645));
            RegisterVisibleBounds("die-red",new Rect(360,299,573,691));
            foreach(var item in FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(item.gameObject.scene==gameObject.scene&&item.name=="grand-cas-hotel")
                {
                    hotelRoot=item;
                    foreach(var renderer in item.GetComponentsInChildren<SpriteRenderer>(true))hotelVisibility[renderer]=renderer.enabled;
                }
            applied=true;SetTheme(CardTableTheme.Current);
        }

        public void SetLounge(bool enabled)=>SetTheme(enabled?TableStyle.GrandCasaHotel:TableStyle.Default);

        public void SetTheme(TableStyle style)
        {
            // A renderer cannot become visible while its authored environment root is inactive.
            // Keep that root available; renderer visibility selects the map without disabling the board.
            if(hotelRoot!=null&&!hotelRoot.gameObject.activeSelf)hotelRoot.gameObject.SetActive(true);
            ApplyCasinoBoardWidth(style);
            var artwork=CardTableTheme.NationArtwork(style);
            bool painted=artwork!=null&&artwork.CombatPanorama!=null;
            var room=GetComponent<SquareArenaRoom>()??gameObject.AddComponent<SquareArenaRoom>();
            room.Configure(arenaCamera,style);
            var definition=MonsterPouch.Gameplay.Presentation.EnvironmentStyle.Find(CardTableTheme.MapId(style));
            var modular=GetComponent<ModularArenaScenery>()??gameObject.AddComponent<ModularArenaScenery>();
            modular.Configure(this,arenaCamera,!painted&&definition!=null?definition.Scenery:null);
            MonsterPouch.Gameplay.Presentation.SceneCharacterLight.SetTheme((int)style);
            bool enabled=style==TableStyle.GrandCasaHotel&&!painted;
            LoungeEnabled=style==TableStyle.GrandCasaHotel;
            if(felt!=null)felt.color=enabled?new Color(.52f,.76f,.76f):Color.white;
            if(surface!=null)surface.color=enabled?new Color(.9f,1,.98f):Color.white;
            var royal=GetComponent<RoyalArenaScenery>();
            if(style==TableStyle.CampoReal&&!painted&&royal==null)
            {
                royal=gameObject.AddComponent<RoyalArenaScenery>();
                royal.Configure(this,arenaCamera);
            }
            var inflatable=GetComponent<InflatableArenaScenery>();
            if(style==TableStyle.Default&&!painted&&inflatable==null)
            {
                inflatable=gameObject.AddComponent<InflatableArenaScenery>();
                inflatable.Configure(this,arenaCamera);
            }
            if(royal!=null)royal.SetVisible(style==TableStyle.CampoReal&&!painted);
            if(inflatable!=null)inflatable.SetVisible(style==TableStyle.Default&&!painted);
            var nation=GetComponent<NationArenaScenery>();
            if(CardTableTheme.IsNation(style)||painted)
            {
                if(nation==null)nation=gameObject.AddComponent<NationArenaScenery>();
                if(artwork!=null){nation.Configure(this,arenaCamera,artwork);nation.SetVisible(true);}
                else{nation.SetVisible(false);Debug.LogError("Missing nation artwork: "+CardTableTheme.MapId(style));}
            }
            else if(nation!=null)nation.SetVisible(false);
            foreach(var entry in hotelVisibility)
            {
                if(entry.Key==null)continue;
                bool replaced=definition!=null&&definition.Scenery!=null&&
                    (entry.Key==felt||entry.Key.name=="board-shadow"||entry.Key.name.EndsWith("-shadow",StringComparison.Ordinal)&&
                        entry.Key.name!="table-frame-shadow"&&!IsAuthoredCasinoPropShadow(entry.Key.name));
                bool removed=entry.Key.name=="die-red"||entry.Key.name=="die-white"||
                    entry.Key.name=="die-red-shadow"||entry.Key.name=="die-white-shadow";
                entry.Key.enabled=enabled&&entry.Value&&!replaced&&!removed;
            }
        }

        // The original composition includes individually positioned silhouettes.
        // Keep their authored transform, opacity and order; only their map visibility changes.
        static bool IsAuthoredCasinoPropShadow(string name)=>name=="clown-tower-shadow"||name=="shuffler-cart-shadow"||
            name.StartsWith("chip-",StringComparison.Ordinal)&&name.EndsWith("-shadow",StringComparison.Ordinal);

        void OnDestroy(){foreach(var entry in hotelVisibility)if(entry.Key!=null)entry.Key.enabled=entry.Value;}

        float WorldPerPixel=>arenaCamera.orthographicSize*2/PortraitHeight;
        public Vector2 PortraitPoint(Vector3 point)=>new Vector2(270+(point.x-arenaCamera.transform.position.x)/WorldPerPixel,
            480-(point.y-arenaCamera.transform.position.y)/WorldPerPixel);
        Rect ToPortrait(Rect world)
        {
            var leftTop=PortraitPoint(new Vector3(world.xMin,world.yMax));
            var rightBottom=PortraitPoint(new Vector3(world.xMax,world.yMin));
            return Rect.MinMaxRect(leftTop.x,leftTop.y,rightBottom.x,rightBottom.y);
        }
        SpriteRenderer FindSprite(string name)
        {
            if(sceneSprites.Count==0)
                foreach(var sprite in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                    if(sprite.gameObject.scene==gameObject.scene&&!sceneSprites.ContainsKey(sprite.name))sceneSprites.Add(sprite.name,sprite);
            return sceneSprites.TryGetValue(name,out var found)?found:null;
        }
        void RegisterVisibleBounds(string name,Rect pixels,float referenceWidth=1254)
        {
            var sprite=FindSprite(name);if(sprite==null||sprite.sprite==null)return;
            visiblePixels[sprite]=pixels;referenceWidths[sprite]=referenceWidth;
        }
        public Rect VisibleWorldBounds(SpriteRenderer sprite)
        {
            if(sprite==null||sprite.sprite==null)return default;
            if(!visiblePixels.TryGetValue(sprite,out var pixels))return new Rect(sprite.bounds.min,sprite.bounds.size);
            var source=sprite.sprite;float scale=source.rect.width/(referenceWidths.TryGetValue(sprite,out float referenceWidth)?referenceWidth:1254f);
            Vector2 min=(pixels.min*scale-source.pivot)/source.pixelsPerUnit;
            Vector2 max=(pixels.max*scale-source.pivot)/source.pixelsPerUnit;
            Vector3 a=sprite.transform.TransformPoint(min),b=sprite.transform.TransformPoint(max);
            return Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));
        }
    }
}
