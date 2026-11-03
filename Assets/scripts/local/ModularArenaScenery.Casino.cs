using UnityEngine;
using System.Collections.Generic;
namespace MonsterPouch.Local
{
    public sealed partial class ModularArenaScenery
    {
        void ProjectOriginalCasinoShadows()
        {
            var sprites=FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var authoredShadows=new HashSet<string>(System.StringComparer.Ordinal);
            foreach(var sprite in sprites)
                if(sprite.gameObject.scene==gameObject.scene&&sprite.sprite!=null&&sprite.gameObject.activeInHierarchy&&
                    sprite.name.EndsWith("-shadow",System.StringComparison.Ordinal))authoredShadows.Add(sprite.name);
            foreach(var draw in sprites)
            {
                if(draw.gameObject.scene!=gameObject.scene||draw.sprite==null)continue;
                // Dice never contribute geometry, even while switching back from another map.
                bool chip=draw.name.StartsWith("chip-",System.StringComparison.Ordinal)&&!draw.name.EndsWith("-shadow",System.StringComparison.Ordinal);
                if(draw.name!="clown-tower"&&draw.name!="shuffler-cart"&&!chip)continue;
                if(!draw.gameObject.activeInHierarchy)continue;
                // SetTheme restores the authored renderer after scenery is configured.
                // Do not add a second silhouette, even if that renderer is still hidden
                // from the previously selected map. Projection is only a missing-art fallback.
                if(authoredShadows.Contains(draw.name+"-shadow"))continue;
                var sprite=draw.sprite;var texture=sprite.texture;
                if(!shadowBatches.TryGetValue(texture,out var batch))shadowBatches.Add(texture,batch=new ShadowBatch());
                var bounds=arena.VisibleWorldBounds(draw);int start=batch.Vertices.Count;
                var points=sprite.vertices;var uv=sprite.uv;
                for(int i=0;i<points.Length;i++)
                {
                    var world=draw.transform.TransformPoint(new Vector3(points[i].x*(draw.flipX?-1:1),points[i].y*(draw.flipY?-1:1),0));
                    float height=world.y-bounds.yMin;
                    var ground=new Vector3(world.x+height*definition.ShadowDirection.x*definition.ShadowLength,
                        bounds.yMin-height*definition.ShadowDirection.y*definition.ShadowLength,0);
                    batch.Vertices.Add(root.transform.InverseTransformPoint(ground));batch.UV.Add(uv[i]);
                    batch.Colors.Add(new Color(0,0,0,.40f));
                }
                foreach(int i in sprite.triangles)batch.Triangles.Add(start+i);
            }
        }
    }
}
