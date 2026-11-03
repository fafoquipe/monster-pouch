using System.Collections;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class DocumentedBattleEffects
    {
        // Jazzar's projectile uses the existing attack windup and projectile-speed timing.
        IEnumerator ToxicFlight(BattleUnit actor,BattleUnit target,float duration)
        {
            var simulation=match.Simulation;
            var root=EffectRoot("jazzar-toxic-flight");
            var drops=new Image[4];
            for(int i=drops.Length-1;i>=0;i--)
            {
                drops[i]=Box(i==0?"jazzar-toxic-glob":"jazzar-viscous-tail",root,Vector2.one*30,Color.white);
                drops[i].sprite=PixelSprite("toxic-glob");
            }
            Vector2 start=Point(actor.transform.position+Vector3.up*.5f);
            float elapsed=0;
            while(elapsed<duration&&Current(simulation)&&target!=null&&root!=null)
            {
                elapsed+=EffectDelta;float t=Mathf.Clamp01(elapsed/duration);
                Vector2 end=Point(target.transform.position+Vector3.up*.5f);
                Vector2 direction=(end-start).normalized;
                float angle=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;
                float sizeRatio = 1;
                for(int i=0;i<drops.Length;i++)
                {
                    float lag=Mathf.Max(0,t-i*.055f);
                    Vector2 point=Vector2.Lerp(start,end,lag)+Vector2.up*(Mathf.Sin(lag*Mathf.PI)*9-i*1.2f);
                    drops[i].rectTransform.anchoredPosition=Round(point);
                    var pose=ProjectileFlightRendering.Toxic(drops[i].sprite,i,elapsed,angle,i==0?1:sizeRatio);pose.Apply(drops[i]);
                    if (i == 0) sizeRatio = SizeProjectile(drops[i], actor, pose.Size);
                }
                yield return null;
            }
            ReleaseEffect(root);
        }

        IEnumerator ToxicSplash(Vector3 position)
        {
            var simulation=match.Simulation;
            var root=EffectRoot("jazzar-toxic-splash");root.anchoredPosition=Round(Point(position));
            root.localScale=Vector3.one*(ContactCellPixels(position)*MonsterPouch.Gameplay.Presentation.ImpactPresentationSettings.ContactCells(false,false)/88f);
            var pool=Box("toxic-contact-splat",root,new Vector2(14,6),Color.white);pool.sprite=PixelSprite("toxic-glob");
            var drops=new Image[7];
            for(int i=0;i<drops.Length;i++)
            {drops[i]=Box("toxic-splash-droplet",root,Vector2.one*(8+i%3*2),Color.white);drops[i].sprite=pool.sprite;}
            float elapsed=0;
            while(elapsed<.42f&&root!=null&&match.Simulation==simulation&&match.Phase!=MatchPhase.Menu&&match.Phase!=MatchPhase.Preparation)
            {
                elapsed+=EffectDelta;float t=Mathf.Clamp01(elapsed/.42f),fade=1-t;
                pool.rectTransform.sizeDelta=new Vector2(18+t*36,12-t*6);pool.color=Alpha(Color.white,fade);
                for(int i=0;i<drops.Length;i++)
                {
                    float angle=i*Mathf.PI*2/drops.Length;
                    drops[i].rectTransform.anchoredPosition=Round(new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(5+t*28)+Vector2.down*t*t*14);
                    drops[i].rectTransform.localRotation=Quaternion.Euler(0,0,angle*Mathf.Rad2Deg);
                    drops[i].rectTransform.localScale=new Vector3(1+t*.6f,1-t*.4f,1);
                    drops[i].color=Alpha(Color.white,fade);
                }
                yield return null;
            }
            ReleaseEffect(root);
        }
    }
}
