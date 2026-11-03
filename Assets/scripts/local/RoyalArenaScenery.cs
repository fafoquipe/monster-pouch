using UnityEngine;

namespace MonsterPouch.Local
{
    // Cosmetic scenery only: board cells, camera projection and hit targets remain shared.
    public sealed class RoyalArenaScenery : MonoBehaviour
    {
        GameObject scenery;
        CardTableArena arena;
        Camera arenaCamera;
        Sprite wood,solid;
        SpriteRenderer surroundings;
        Material roomPixelMaterial;
        Rect lastViewport;
        Vector2Int lastScreen;
        Material spriteMaterial;
        public bool IsVisible=>scenery!=null&&scenery.activeSelf;
        public Rect TilePortraitBounds { get; private set; }
        public Rect FramePortraitBounds { get; private set; }

        public void Configure(CardTableArena owner,Camera camera)
        {
            if(scenery!=null)return;
            arena=owner;arenaCamera=camera;
            wood=Resources.Load<Sprite>("MonsterPouch/UI/TableSkins/royal-wood");
            var tileWood=Resources.Load<Sprite>("MonsterPouch/UI/TableSkins/royal-wood-soft");
            var room=Resources.Load<Sprite>("MonsterPouch/UI/TableSkins/royal-arena-pixel");
            if(wood==null||tileWood==null||room==null){Debug.LogError("Campo Real artwork is missing.");return;}
            spriteMaterial=PixelScenery.CreateMaterial();
            scenery=new GameObject("Campo Real scenery");
            scenery.transform.SetParent(transform,true);
            solid=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),new Vector2(.5f,.5f),100);
            // Cover the extra screen area on tall phones and tablets with real wood.
            if(MonsterPouch.Gameplay.Presentation.EnvironmentStyle.Find("royal")?.Scenery==null)
            {
                Draw("Royal floor extension",wood,new Rect(-1600,-2200,3740,5300),new Color(.83f,.80f,.74f),0);
                surroundings=Draw("Royal court surroundings",room,new Rect(0,0,540,960),Color.white,1);
            }

            Rect grid=arena.BoardPortraitBounds;TilePortraitBounds=grid;
            // Match the original hotel's full rail width around the same playable grid.
            // Keep Campo Real's own wood, inlays and corner fittings; only widen the rails.
            Rect frame=arena.FramePortraitBounds;FramePortraitBounds=frame;
            Draw("Royal table shadow",solid,new Rect(frame.x+5,frame.y+9,frame.width,frame.height),new Color(.19f,.09f,.05f,.4f),8);
            Draw("Royal carved frame",wood,frame,new Color(.50f,.40f,.33f),9);
            Draw("Royal outer gold inlay",solid,RailBounds(grid,frame,20f/24),new Color(.88f,.65f,.28f),10);
            Draw("Royal oak rail",wood,RailBounds(grid,frame,17f/24),new Color(.89f,.77f,.58f),11);
            Draw("Royal ivory inlay",solid,RailBounds(grid,frame,4f/24),new Color(1,.85f,.51f),12);
            Draw("Royal board backing",solid,RailBounds(grid,frame,1.5f/24),new Color(.30f,.18f,.10f),13);
            const int columns=MonsterPouch.Gameplay.Board.BoardManager.Width;
            const int rows=MonsterPouch.Gameplay.Board.BoardManager.Height;
            float width=grid.width/columns,height=grid.height/rows;
            for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
            {
                bool light=(x+y)%2==0;
                var tile=Draw("Royal tile "+x+" "+y,tileWood,new Rect(grid.x+x*width,grid.y+y*height,width,height),
                    light?new Color(1,.98f,.94f):new Color(.62f,.55f,.48f),18);
                tile.flipX=x%2==0;tile.flipY=y%3==0;
            }
            Rect corners=RailBounds(grid,frame,15f/24);
            float fitting=Mathf.Min(grid.xMin-frame.xMin,frame.xMax-grid.xMax,grid.yMin-frame.yMin,frame.yMax-grid.yMax)/24f;
            foreach(float x in new[]{corners.xMin,corners.xMax})foreach(float y in new[]{corners.yMin,corners.yMax})
            {
                Draw("Royal brass corner",solid,new Rect(x-4*fitting,y-4*fitting,8*fitting,8*fitting),new Color(.94f,.73f,.37f),14);
                Draw("Royal corner shine",solid,new Rect(x-3*fitting,y-3*fitting,3*fitting,2*fitting),new Color(1,.94f,.72f),15);
            }
            scenery.SetActive(false);
            UpdateSurroundings();
        }

        void LateUpdate()=>UpdateSurroundings();
        void UpdateSurroundings()
        {
            if(surroundings==null||arenaCamera==null)return;
            Rect view=arenaCamera.pixelRect;
            var screen=new Vector2Int(Screen.width,Screen.height);
            if(view.width<=0||view.height<=0||view==lastViewport&&screen==lastScreen)return;
            lastViewport=view;lastScreen=screen;
            Rect bounds=new Rect(0,0,540,960);
            if(roomPixelMaterial==null)roomPixelMaterial=PixelScenery.CreateMaterial();
            surroundings.sharedMaterial=roomPixelMaterial;
            PixelScenery.Configure(roomPixelMaterial,surroundings.sprite);
            Place(surroundings.transform,surroundings.sprite,bounds);
        }

        public void SetVisible(bool visible)
        {
            if(scenery==null)return;
            scenery.SetActive(visible);
        }

        static Rect Expand(Rect rect,float amount)=>new Rect(rect.x-amount,rect.y-amount,rect.width+2*amount,rect.height+2*amount);
        static Rect RailBounds(Rect grid,Rect frame,float outward)=>Rect.MinMaxRect(
            Mathf.Lerp(grid.xMin,frame.xMin,outward),Mathf.Lerp(grid.yMin,frame.yMin,outward),
            Mathf.Lerp(grid.xMax,frame.xMax,outward),Mathf.Lerp(grid.yMax,frame.yMax,outward));
        SpriteRenderer Draw(string name,Sprite art,Rect rect,Color tint,int order)
        {
            var go=new GameObject(name,typeof(SpriteRenderer));go.transform.SetParent(scenery.transform,false);
            Place(go.transform,art,rect);
            var renderer=go.GetComponent<SpriteRenderer>();renderer.sprite=art;renderer.color=tint;renderer.sortingOrder=order;
            if(spriteMaterial!=null)renderer.sharedMaterial=spriteMaterial;
            return renderer;
        }
        void Place(Transform item,Sprite art,Rect rect)
        {
            float unit=arenaCamera.orthographicSize*2/CardTableArena.PortraitHeight;
            item.position=new Vector3(arenaCamera.transform.position.x+(rect.center.x-270)*unit,
                arenaCamera.transform.position.y+(480-rect.center.y)*unit,0);
            item.localScale=new Vector3(rect.width*unit/art.bounds.size.x,rect.height*unit/art.bounds.size.y,1);
        }
        void OnDestroy()
        {
            if(scenery!=null)Destroy(scenery);
            if(solid!=null)Destroy(solid);
            if(roomPixelMaterial!=null)Destroy(roomPixelMaterial);
            if(spriteMaterial!=null)Destroy(spriteMaterial);
        }
    }
}
