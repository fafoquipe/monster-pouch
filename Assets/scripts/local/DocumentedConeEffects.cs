using System.Collections;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class DocumentedBattleEffects
    {
        void ConesLaunched(BattleUnit source,BoardCell origin,BoardCell[] cells,float duration)
        {
            if(!isActiveAndEnabled||layer==null||source==null)return;
            StartCoroutine(ThrowCones(source,origin,cells,duration));
        }
        IEnumerator ThrowCones(BattleUnit source,BoardCell origin,BoardCell[] cells,float duration)
        {
            var simulation=match.Simulation;
            var root=EffectRoot("tokoro-flying-cones");
            Vector2 from=Point(match.Mapper.GetWorldPosition(origin))+Vector2.up*24;
            var cones=new Image[cells.Length];var ends=new Vector2[cells.Length];
            for(int i=0;i<cells.Length;i++)
            {
                cones[i]=Box("flying-cone",root,new Vector2(24,24),Color.white);
                cones[i].sprite=PixelSprite("cone");cones[i].rectTransform.anchoredPosition=from;
                ends[i]=Point(match.Mapper.GetWorldPosition(cells[i]))+Vector2.up*9;
            }
            float started=simulation.Elapsed;float elapsed=0;
            while(root!=null&&Current(simulation)&&simulation.Elapsed-started<duration-.00001f)
            {
                // Follow the combat clock: pause, fast simulation and low FPS agree
                // with the exact tick on which the cone becomes collectible.
                elapsed+=EffectDelta;float t=Mathf.Clamp01(elapsed/duration);
                for(int i=0;i<cones.Length;i++)
                {
                    cones[i].rectTransform.anchoredPosition=Round(Vector2.Lerp(from,ends[i],t)+Vector2.up*Mathf.Sin(t*Mathf.PI)*22);
                    var pose=ProjectileFlightRendering.Cone(cones[i].sprite,t,i);pose.Apply(cones[i]);
                    SizeProjectile(cones[i], source, pose.Size, ProjectileVisualSource.Upgrade2);
                }
                yield return null;
            }
            ReleaseEffect(root);
        }
    }
}
