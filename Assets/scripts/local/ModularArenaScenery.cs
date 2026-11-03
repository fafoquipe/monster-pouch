using System.Collections.Generic;
using MonsterPouch.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
namespace MonsterPouch.Local
{
    // Authored positions share the board's coordinate system. Resizing only reveals more scenery.
    public sealed partial class ModularArenaScenery:MonoBehaviour
    {
        CardTableArena arena;
        Camera view;
        ArenaSceneryDefinition definition;
        GameObject root;
        Material material,geometryMaterial;
        Mesh backdrop;
        int revision;
        readonly Dictionary<Texture,Material> spriteMaterials=new Dictionary<Texture,Material>();
        readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Color> colors=new List<Color>();
        readonly List<int> indices=new List<int>();
        readonly List<Vector2> uvs=new List<Vector2>();
        public bool IsVisible=>root!=null&&root.activeSelf;
        public ArenaSceneryDefinition Definition=>definition;
        public void Configure(CardTableArena owner,Camera camera,ArenaSceneryDefinition style)
        {
            arena=owner;view=camera;
            if(style==definition&&root!=null){root.SetActive(true);return;}
            definition=style;Rebuild();
        }
        void LateUpdate(){if(definition!=null&&definition.Revision!=revision)Rebuild();}
        void Rebuild()
        {
            Clear();if(definition==null)return;revision=definition.Revision;
            root=new GameObject("Modular arena scenery");root.transform.SetParent(transform,false);
            material=PixelScenery.CreateMaterial();material.SetFloat("_Saturation",definition.Saturation);
            material.SetFloat("_WarmAccentBoost",.65f);
            material.SetFloat("_PixelSize",1);material.SetVector("_CropRect",new Vector4(0,0,1,1));
            geometryMaterial=new Material(material);geometryMaterial.mainTexture=Texture2D.whiteTexture;
            geometryMaterial.SetFloat("_Saturation",.58f);geometryMaterial.SetFloat("_Brightness",.86f);
            Rect board=arena.FramePortraitBounds;float horizon=board.yMin-definition.FloorStartsAboveBoard;
            // This square is deliberately larger than the visible area, including landscape previews.
            const float left=-1800,right=2340,top=-1590,bottom=2550;
            if(definition.FloorTexture!=null)DrawFloor(new Rect(left,top,right-left,bottom-top));
            else Quad(new Rect(left,top,right-left,bottom-top),definition.Floor);
            if(definition.FloorTexture==null)
            {
                Color seam=Color.Lerp(definition.Floor,Color.black,.05f);
                for(float x=left;x<right;x+=80)Quad(new Rect(x,horizon,1,bottom-horizon),seam);
                for(float y=horizon;y<bottom;y+=80)Quad(new Rect(left,y,right-left,1),seam);
            }
            if(definition.Carpets)
            {
                foreach(float x in new[]{board.xMin-72,board.xMax+12})
                {
                    Quad(new Rect(x,horizon,60,bottom-horizon),definition.Carpet);
                    Quad(new Rect(x+5,horizon,2,bottom-horizon),definition.Trim);
                    Quad(new Rect(x+53,horizon,2,bottom-horizon),definition.Trim);
                    for(float y=horizon+24;y<bottom;y+=56)
                    {
                        Quad(new Rect(x+10,y,4,4),definition.Trim);Quad(new Rect(x+46,y,4,4),definition.Trim);
                    }
                }
            }
            if(!definition.Tabletop)
            {
            Quad(new Rect(left,top,right-left,horizon-top),definition.Wall);
            if(definition.StoneWall)
            {
                int row=0;
                for(float y=horizon-44;y>top;y-=44,row++)
                {
                    Quad(new Rect(left,y,right-left,2),definition.WallDetail);
                    for(float x=left+(row%2)*48;x<right;x+=96)Quad(new Rect(x,y,2,44),definition.WallDetail);
                }
            }
            else
                for(float x=left;x<right;x+=60)
                { Quad(new Rect(x,top,2,horizon-top),definition.WallDetail);Quad(new Rect(x+2,top,1,horizon-top),Color.Lerp(definition.Wall,Color.white,.08f)); }
            Quad(new Rect(left,horizon-8,right-left,8),definition.Trim);
            Quad(new Rect(left,horizon,right-left,4),new Color(0,0,0,.16f));
            }
            else ProjectOriginalCasinoShadows();
            var props=new GameObject("Independent environment pieces",typeof(SortingGroup));
            props.transform.SetParent(root.transform,false);props.GetComponent<SortingGroup>().sortingOrder=5;
            foreach(var element in definition.Elements)
            {
                if(element==null||element.Sprite==null||element.Width<=0)continue;
                Vector2 foot=ResolveFoot(element,board);
                float height=element.Width*element.Sprite.rect.height/element.Sprite.rect.width;
                var go=new GameObject(element.Name);go.transform.SetParent(props.transform,false);
                var draw=go.AddComponent<SpriteRenderer>();draw.sprite=element.Sprite;draw.sharedMaterial=MaterialFor(element.Sprite);
                draw.flipX=element.FlipX;draw.sortingOrder=element.Order;
                float scale=element.Width*Unit/element.Sprite.bounds.size.x;
                go.transform.localScale=Vector3.one*scale;
                // Match the visible bottom center, independently of each source sprite's pivot.
                Vector3 localFoot=new Vector3(element.Sprite.bounds.center.x,element.Sprite.bounds.min.y+element.Sprite.bounds.size.y*element.BottomPadding,0);
                go.transform.localRotation=Quaternion.Euler(0,0,element.Rotation);
                go.transform.position=Point(foot)-go.transform.TransformVector(localFoot);
                if(element.LightStrength>0)
                {
                    var emitter=new GameObject(element.Name+" light");emitter.transform.SetParent(root.transform,false);
                    emitter.transform.position=Point(foot-new Vector2(0,height*.82f));
                    var lamp=emitter.AddComponent<SceneryLamp>();lamp.Color=element.LightColor;
                    lamp.Strength=element.LightStrength;lamp.Radius=element.LightRadius*Unit;
                }
                ProjectShadow(element,foot,scale);
                if(element.Shadow>0&&!definition.Tabletop)Shadow(foot+new Vector2(3,2),element.Width*.86f,Mathf.Min(14,height*.12f),element.Shadow);
            }
            BuildProjectedShadows();
            var geometry=new GameObject("Floor, wall, trim and contact shadows",typeof(MeshFilter),typeof(MeshRenderer));
            geometry.transform.SetParent(root.transform,false);
            backdrop=new Mesh{name="Modular environment geometry"};
            backdrop.SetVertices(vertices);backdrop.SetColors(colors);backdrop.SetUVs(0,uvs);backdrop.SetTriangles(indices,0);backdrop.RecalculateBounds();
            geometry.GetComponent<MeshFilter>().sharedMesh=backdrop;
            var renderer=geometry.GetComponent<MeshRenderer>();renderer.sharedMaterial=geometryMaterial;renderer.sortingOrder=2;
            vertices.Clear();colors.Clear();uvs.Clear();indices.Clear();
        }
        sealed class ShadowBatch
        {
            public readonly List<Vector3> Vertices=new List<Vector3>();
            public readonly List<Vector2> UV=new List<Vector2>();
            public readonly List<Color> Colors=new List<Color>();
            public readonly List<int> Triangles=new List<int>();
        }
        readonly Dictionary<Texture,ShadowBatch> shadowBatches=new Dictionary<Texture,ShadowBatch>();
        readonly List<Mesh> shadowMeshes=new List<Mesh>();
        readonly List<Material> shadowMaterials=new List<Material>();
        void ProjectShadow(SceneryElement element,Vector2 foot,float scale)
        {
            if(element.Shadow<=0)return;
            var sprite=element.Sprite;var texture=sprite.texture;
            if(!shadowBatches.TryGetValue(texture,out var batch))shadowBatches.Add(texture,batch=new ShadowBatch());
            int start=batch.Vertices.Count;var points=sprite.vertices;var uv=sprite.uv;
            Vector3 origin=Point(foot);
            for(int i=0;i<points.Length;i++)
            {
                Vector3 local=Quaternion.Euler(0,0,element.Rotation)*new Vector3((points[i].x-sprite.bounds.center.x)*scale*(element.FlipX?-1:1),(points[i].y-sprite.bounds.min.y-sprite.bounds.size.y*element.BottomPadding)*scale,0);
                float x=local.x,height=local.y;
                var point=origin+new Vector3(x+height*definition.ShadowDirection.x*definition.ShadowLength,
                    -height*definition.ShadowDirection.y*definition.ShadowLength,0);
                batch.Vertices.Add(root.transform.InverseTransformPoint(point));batch.UV.Add(uv[i]);
                batch.Colors.Add(new Color(.025f,.04f,.055f,element.Shadow));
            }
            foreach(int index in sprite.triangles)batch.Triangles.Add(start+index);
        }
        void BuildProjectedShadows()
        {
            foreach(var entry in shadowBatches)
            {
                var data=entry.Value;var mesh=new Mesh{name="Projected scenery shadows"};
                mesh.SetVertices(data.Vertices);mesh.SetUVs(0,data.UV);mesh.SetColors(data.Colors);
                mesh.SetTriangles(data.Triangles,0);mesh.RecalculateBounds();shadowMeshes.Add(mesh);
                var go=new GameObject("Projected prop shadows "+entry.Key.name,typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(root.transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
                var shadowMaterial=new Material(Resources.Load<Shader>("MonsterPouch/ScenerySilhouetteShadow")){mainTexture=entry.Key,name="Silhouette shadow "+entry.Key.name};
                var frame=arena.FramePortraitBounds;var a=Point(frame.min);var b=Point(frame.max);
                shadowMaterial.SetVector("_BoardExclusion",new Vector4(a.x,b.y,b.x,a.y));
                shadowMaterials.Add(shadowMaterial);
                var draw=go.GetComponent<MeshRenderer>();draw.sharedMaterial=shadowMaterial;draw.sortingOrder=3;
            }
            shadowBatches.Clear();
        }


        public static Vector2 ResolveFoot(SceneryElement element,Rect board)
        {
            Vector2 origin=element.Anchor switch
            {
                SceneryAnchor.BoardTop=>new Vector2(board.center.x,board.yMin),
                SceneryAnchor.BoardLeft=>new Vector2(board.xMin,board.center.y),
                SceneryAnchor.BoardRight=>new Vector2(board.xMax,board.center.y),
                SceneryAnchor.BoardBottom=>new Vector2(board.center.x,board.yMax),
                _=>new Vector2(270,480)
            };
            return origin+element.Foot;
        }
        void DrawFloor(Rect area)
        {
            var go=new GameObject("Repeating floor texture",typeof(SpriteRenderer));go.transform.SetParent(root.transform,false);
            var sprite=definition.FloorTexture;var draw=go.GetComponent<SpriteRenderer>();draw.sprite=sprite;draw.sharedMaterial=MaterialFor(sprite);
            draw.sharedMaterial.SetFloat("_Saturation",definition.Tabletop?.72f:.58f);
            draw.sharedMaterial.SetFloat("_Brightness",.86f);
            draw.color=definition.Floor;draw.sortingOrder=0;draw.drawMode=SpriteDrawMode.Tiled;
            float scale=definition.FloorTileWidth*Unit/sprite.bounds.size.x;
            draw.size=area.size*Unit/scale;go.transform.localScale=Vector3.one*scale;go.transform.position=Point(area.center);
        }
        Material MaterialFor(Sprite sprite)
        {
            if(!spriteMaterials.TryGetValue(sprite.texture,out var value))
            {value=new Material(material){name="Scenery atlas "+sprite.texture.name,mainTexture=sprite.texture};spriteMaterials.Add(sprite.texture,value);}
            return value;
        }
        float Unit=>view.orthographicSize*2/CardTableArena.PortraitHeight;
        Vector3 Point(Vector2 p)=>new Vector3(view.transform.position.x+(p.x-270)*Unit,view.transform.position.y+(480-p.y)*Unit,0);
        void Add(Vector2 p,Color color){vertices.Add(root.transform.InverseTransformPoint(Point(p)));colors.Add(QualitySettings.activeColorSpace==ColorSpace.Linear?color.linear:color);uvs.Add(Vector2.one*.5f);}
        void Quad(Rect r,Color c)
        {
            int i=vertices.Count;Add(new Vector2(r.xMin,r.yMin),c);Add(new Vector2(r.xMin,r.yMax),c);
            Add(new Vector2(r.xMax,r.yMax),c);Add(new Vector2(r.xMax,r.yMin),c);
            indices.Add(i);indices.Add(i+1);indices.Add(i+2);indices.Add(i);indices.Add(i+2);indices.Add(i+3);
        }
        void Shadow(Vector2 foot,float width,float height,float alpha)
        {
            int center=vertices.Count;Color c=new Color(.10f,.08f,.12f,alpha);Add(foot,c);
            for(int i=0;i<12;i++){float a=i*Mathf.PI/6;Add(foot+new Vector2(Mathf.Cos(a)*width*.5f,Mathf.Sin(a)*height*.5f),c);}
            for(int i=0;i<12;i++){indices.Add(center);indices.Add(center+1+i);indices.Add(center+1+(i+1)%12);}
        }
        void Clear()
        {
            if(root!=null){root.SetActive(false);Destroy(root);}if(backdrop!=null)Destroy(backdrop);if(material!=null)Destroy(material);if(geometryMaterial!=null)Destroy(geometryMaterial);
            foreach(var value in shadowMaterials)if(value!=null)Destroy(value);shadowMaterials.Clear();
            foreach(var mesh in shadowMeshes)if(mesh!=null)Destroy(mesh);shadowMeshes.Clear();shadowBatches.Clear();
            foreach(var value in spriteMaterials.Values)if(value!=null)Destroy(value);spriteMaterials.Clear();
            root=null;backdrop=null;material=null;geometryMaterial=null;vertices.Clear();colors.Clear();uvs.Clear();indices.Clear();
        }
        void OnDestroy()=>Clear();
    }
}
