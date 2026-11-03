using UnityEngine;
using System.Collections.Generic;

namespace MonsterPouch.Local
{
    // The illustrated play park surrounds the same physical board used by all maps.
    public sealed class InflatableArenaScenery : MonoBehaviour
    {
        GameObject scenery;
        Camera arenaCamera;
        Sprite solid;
        const float BorderWidth=21;
        readonly List<Mesh> borderMeshes=new List<Mesh>();
        Material material,shadowMaterial;
        Mesh contactShadow;
        SpriteRenderer room;
        Material roomPixelMaterial;
        Rect lastViewport;
        Vector2Int lastScreen;
        public bool IsVisible=>scenery!=null&&scenery.activeSelf;
        public Rect TilePortraitBounds {get;private set;}
        public Rect FramePortraitBounds {get;private set;}

        public void Configure(CardTableArena arena,Camera camera)
        {
            if(scenery!=null)return;
            var background=Resources.Load<Sprite>("MonsterPouch/UI/TableSkins/toy-inflatable-room");
            if(background==null){Debug.LogError("Play park background artwork is missing.");return;}
            arenaCamera=camera;material=PixelScenery.CreateMaterial();
            scenery=new GameObject("Inflatable toy park scenery");scenery.transform.SetParent(transform,true);
            solid=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),new Vector2(.5f,.5f),100);
            if(MonsterPouch.Gameplay.Presentation.EnvironmentStyle.Find("classic")?.Scenery==null)
            {
                Draw("Toy foam floor extension",solid,new Rect(-1600,-2200,3740,5300),new Color(.11f,.43f,.49f),0);
                room=Draw("Toy park surroundings",background,new Rect(0,0,540,960),Color.white,1);
            }
            Rect grid=arena.BoardPortraitBounds;
            Rect outer=Rect.MinMaxRect(grid.xMin-BorderWidth,grid.yMin-BorderWidth,grid.xMax+BorderWidth,grid.yMax+BorderWidth);
            TilePortraitBounds=grid;FramePortraitBounds=outer;
            DrawRoundedShadow(new Rect(outer.x,outer.y+2,outer.width,outer.height));
            DrawFrame(grid);
            // Alternating foam tiles keep the playing area legible inside the ball pit.
            const int columns=MonsterPouch.Gameplay.Board.BoardManager.Width;
            const int rows=MonsterPouch.Gameplay.Board.BoardManager.Height;
            float width=grid.width/columns,height=grid.height/rows;
            for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
            {
                bool light=(x+y)%2==0;
                Rect tile=new Rect(grid.x+x*width,grid.y+y*height,width,height);
                Draw("Toy tile "+x+" "+y,solid,tile,light?new Color(.74f,.86f,.70f):new Color(.40f,.68f,.66f),18);
                Draw("Toy tile stitch "+x+" "+y,solid,new Rect(tile.x+2,tile.y+1,tile.width-4,1),new Color(1,1,.87f,.15f),19);
            }
            scenery.SetActive(false);UpdateSurroundings();
        }
        public void SetVisible(bool visible){if(scenery!=null)scenery.SetActive(visible);}
        void DrawRoundedShadow(Rect rect)
        {
            // A feathered rounded contact shadow, without a rectangular backing behind the corners.
            const int steps=6,count=steps*4;
            var vertices=new Vector3[count*2+1];var colors=new Color[vertices.Length];
            var uv=new Vector2[vertices.Length];var triangles=new List<int>();
            float unit=arenaCamera.orthographicSize*2/CardTableArena.PortraitHeight;
            Vector3 World(Vector2 p)=>new Vector3(arenaCamera.transform.position.x+(p.x-270)*unit,
                arenaCamera.transform.position.y+(480-p.y)*unit,0);
            vertices[0]=scenery.transform.InverseTransformPoint(World(rect.center));colors[0]=new Color(.08f,.18f,.18f,.08f);
            for(int ring=0;ring<2;ring++)
            {
                float spread=ring*2,radius=BorderWidth+spread;
                Rect bounds=Rect.MinMaxRect(rect.xMin-spread,rect.yMin-spread,rect.xMax+spread,rect.yMax+spread);
                for(int corner=0;corner<4;corner++)
                {
                    Vector2 center=new Vector2(corner==0||corner==3?bounds.xMax-radius:bounds.xMin+radius,
                        corner<2?bounds.yMax-radius:bounds.yMin+radius);
                    for(int step=0;step<steps;step++)
                    {
                        float angle=(corner*90+step*90f/(steps-1))*Mathf.Deg2Rad;
                        int index=1+ring*count+corner*steps+step;
                        vertices[index]=scenery.transform.InverseTransformPoint(World(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius));
                        colors[index]=new Color(.08f,.18f,.18f,ring==0?.08f:0);
                    }
                }
            }
            for(int i=0;i<count;i++)
            {
                int a=1+i,b=1+(i+1)%count,c=a+count,d=b+count;
                triangles.AddRange(new[]{0,a,b,a,c,d,a,d,b});
            }
            var go=new GameObject("Park mat contact shadow",typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(scenery.transform,false);
            contactShadow=new Mesh{name="Soft park mat contact shadow"};
            contactShadow.vertices=vertices;contactShadow.colors=colors;contactShadow.uv=uv;
            contactShadow.SetTriangles(triangles,0);contactShadow.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh=contactShadow;
            shadowMaterial=new Material(material);shadowMaterial.mainTexture=Texture2D.whiteTexture;
            var draw=go.GetComponent<MeshRenderer>();draw.sharedMaterial=shadowMaterial;draw.sortingOrder=7;
        }
        void DrawFrame(Rect grid)
        {
            // Seven solid, parallel rainbow bands wrap the board and turn around rounded corners.
            DrawRainbowEdge("top",new Vector2(grid.xMin,grid.yMin),new Vector2(grid.xMax,grid.yMin),Vector2.down,-90);
            DrawRainbowEdge("right",new Vector2(grid.xMax,grid.yMin),new Vector2(grid.xMax,grid.yMax),Vector2.right,0);
            DrawRainbowEdge("bottom",new Vector2(grid.xMax,grid.yMax),new Vector2(grid.xMin,grid.yMax),Vector2.up,90);
            DrawRainbowEdge("left",new Vector2(grid.xMin,grid.yMax),new Vector2(grid.xMin,grid.yMin),Vector2.left,180);
        }
        void DrawRainbowEdge(string side,Vector2 start,Vector2 end,Vector2 outward,float cornerAngle)
        {
            var palette=new[]{new Color(.90f,.18f,.24f),new Color(.97f,.40f,.12f),new Color(1,.79f,.18f),
                new Color(.24f,.69f,.34f),new Color(.13f,.52f,.90f),new Color(.26f,.30f,.67f),new Color(.56f,.25f,.73f)};
            const int cornerSegments=12;
            var vertices=new List<Vector3>();var colors=new List<Color>();
            var uv=new List<Vector2>();var triangles=new List<int>();
            float unit=arenaCamera.orthographicSize*2/CardTableArena.PortraitHeight;
            Vector3 Local(Vector2 point)=>scenery.transform.InverseTransformPoint(new Vector3(
                arenaCamera.transform.position.x+(point.x-270)*unit,arenaCamera.transform.position.y+(480-point.y)*unit,0));
            void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
            {
                int v=vertices.Count;
                vertices.Add(Local(a));vertices.Add(Local(b));vertices.Add(Local(c));vertices.Add(Local(d));
                for(int i=0;i<4;i++){colors.Add(color);uv.Add(Vector2.one*.5f);}
                triangles.AddRange(new[]{v,v+1,v+2,v+2,v+1,v+3});
            }
            for(int band=0;band<palette.Length;band++)
            {
                float outside=BorderWidth*(palette.Length-band)/palette.Length;
                float inside=BorderWidth*(palette.Length-band-1)/palette.Length;
                Color color=QualitySettings.activeColorSpace==ColorSpace.Linear?palette[band].linear:palette[band];
                Quad(start+outward*outside,end+outward*outside,start+outward*inside,end+outward*inside,color);
                for(int segment=0;segment<cornerSegments;segment++)
                {
                    float a=(cornerAngle+segment*90f/cornerSegments)*Mathf.Deg2Rad;
                    float b=(cornerAngle+(segment+1)*90f/cornerSegments)*Mathf.Deg2Rad;
                    var from=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var to=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                    Quad(end+from*outside,end+to*outside,end+from*inside,end+to*inside,color);
                }
            }
            var mesh=new Mesh{name="Park rainbow edge "+side};
            mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();borderMeshes.Add(mesh);
            var go=new GameObject("Park mat edge "+side,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(scenery.transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=shadowMaterial;renderer.sortingOrder=17;
        }
        void LateUpdate()=>UpdateSurroundings();
        void UpdateSurroundings()
        {
            if(room==null||arenaCamera==null)return;
            Rect view=arenaCamera.pixelRect;var size=new Vector2Int(Screen.width,Screen.height);
            if(view.width<=0||view.height<=0||view==lastViewport&&size==lastScreen)return;
            lastViewport=view;lastScreen=size;
            Rect full=new Rect(0,0,540,960);
            if(roomPixelMaterial==null)roomPixelMaterial=PixelScenery.CreateMaterial();
            room.sharedMaterial=roomPixelMaterial;
            PixelScenery.Configure(roomPixelMaterial,room.sprite);
            Place(room.transform,room.sprite,full);
        }
        SpriteRenderer Draw(string name,Sprite sprite,Rect rect,Color color,int order)
        {
            var go=new GameObject(name,typeof(SpriteRenderer));go.transform.SetParent(scenery.transform,false);
            Place(go.transform,sprite,rect);
            var renderer=go.GetComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.color=color;renderer.sortingOrder=order;
            if(material!=null)renderer.sharedMaterial=material;
            return renderer;
        }
        void Place(Transform item,Sprite sprite,Rect rect)
        {
            float unit=arenaCamera.orthographicSize*2/CardTableArena.PortraitHeight;
            item.position=new Vector3(arenaCamera.transform.position.x+(rect.center.x-270)*unit,
                arenaCamera.transform.position.y+(480-rect.center.y)*unit,0);
            item.localScale=new Vector3(rect.width*unit/sprite.bounds.size.x,rect.height*unit/sprite.bounds.size.y,1);
        }
        void OnDestroy(){foreach(var mesh in borderMeshes)if(mesh!=null)Destroy(mesh);if(contactShadow!=null)Destroy(contactShadow);if(shadowMaterial!=null)Destroy(shadowMaterial);if(material!=null)Destroy(material);if(roomPixelMaterial!=null)Destroy(roomPixelMaterial);if(scenery!=null)Destroy(scenery);if(solid!=null)Destroy(solid);}
    }
}
