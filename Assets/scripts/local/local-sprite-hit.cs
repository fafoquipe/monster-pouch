using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        readonly Dictionary<Sprite,Vector2[][]> spriteHitShapes = new Dictionary<Sprite,Vector2[][]>();

        // Own figures stand in front of revealed enemy figures. Only their actual
        // sprite shape wins this test; the old proximity radius must not steal a
        // long press on an exposed part of the enemy behind them.
        OwnedUnit HitVisibleOwnSprite(Vector2 screen)
        {
            OwnedUnit best=null;
            int order=int.MinValue;
            foreach(var pair in Match.Actors)
            {
                var actor=pair.Value;
                if(actor==null || actor.Side!=Match.LocalSide || !actor.IsAlive) continue;
                foreach(var renderer in actor.GetComponentsInChildren<SpriteRenderer>())
                {
                    if(renderer.name=="ground-shadow" || !renderer.enabled || renderer.forceRenderingOff
                        || !renderer.gameObject.activeInHierarchy || renderer.sprite==null || renderer.color.a<=.01f
                        || renderer.sortingOrder<order) continue;
                    if(!SpriteContainsScreenPoint(renderer,screen)) continue;
                    best=pair.Key; order=renderer.sortingOrder;
                }
            }
            return best;
        }

        bool SpriteContainsScreenPoint(SpriteRenderer renderer,Vector2 screen)
        {
            float depth=GameCamera.WorldToScreenPoint(renderer.transform.position).z;
            Vector3 world=GameCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,depth));
            Vector2 point=renderer.transform.InverseTransformPoint(world);
            if(renderer.flipX)point.x=-point.x;
            if(renderer.flipY)point.y=-point.y;
            var sprite=renderer.sprite;
            var bounds=sprite.bounds;
            if(point.x<bounds.min.x || point.x>bounds.max.x || point.y<bounds.min.y || point.y>bounds.max.y) return false;
            if(!spriteHitShapes.TryGetValue(sprite,out var shapes))
            {
                shapes=new Vector2[sprite.GetPhysicsShapeCount()][];
                var points=new List<Vector2>();
                for(int i=0;i<shapes.Length;i++)
                { points.Clear(); sprite.GetPhysicsShape(i,points); shapes[i]=points.ToArray(); }
                spriteHitShapes.Add(sprite,shapes);
            }
            if(shapes.Length==0) return true;
            foreach(var shape in shapes) if(PolygonContains(shape,point)) return true;
            return false;
        }

        static bool PolygonContains(Vector2[] polygon,Vector2 point)
        {
            bool inside=false;
            for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
            {
                Vector2 a=polygon[i],b=polygon[j];
                if((a.y>point.y)!=(b.y>point.y)
                    && point.x<(b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x) inside=!inside;
            }
            return inside;
        }
    }
}
