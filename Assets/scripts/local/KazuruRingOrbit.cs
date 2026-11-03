using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Local
{
    // A presentation attachment: the combat clock owns the halo's immunity and expiry.
    [DefaultExecutionOrder(30)]
    public sealed class KazuruRingOrbit : MonoBehaviour
    {
        BattleUnit unit, target;
        MatchController match;
        UnitPresentation view;
        readonly SpriteRenderer[] rings = new SpriteRenderer[3];
        Sprite tilted, halo;
        float time, strikeTime = -1;
        int attackVersion;
        public void Configure(BattleUnit actor, MatchController owner)
        {
            unit=actor;match=owner;view=actor.GetComponent<UnitPresentation>();
            var sprites=Resources.LoadAll<Sprite>("MonsterPouch/Kazuru/kazuru-rings");
            tilted=sprites.FirstOrDefault(s=>s.name=="tilted");halo=sprites.FirstOrDefault(s=>s.name=="halo");
            for(int i=0;i<3;i++)if(rings[i]==null)
            {
                var go=new GameObject("kazuru-orbit-"+i);go.transform.SetParent(transform,false);
                rings[i]=go.AddComponent<SpriteRenderer>();
                rings[i].sharedMaterial=Resources.Load<Material>("MonsterPouch/UnitSpriteMaterial");
                rings[i].sprite=tilted;
            }
        }
        public void Strike(BattleUnit victim)
        {target=victim;strikeTime=0;attackVersion=unit.AttackResetVersion;}
        void LateUpdate()
        {
            var body=view!=null?view.Renderer:null;
            bool visible=body!=null&&body.enabled&&unit!=null&&unit.IsAlive&&!unit.IsReviving&&body.sprite!=null;
            bool paused=match!=null&&match.Paused;
            if(!paused)time+=Time.deltaTime;
            if(strikeTime>=0&&!paused)strikeTime+=Time.deltaTime;
            bool protectedNow=visible&&match!=null&&match.Simulation!=null&&match.Simulation.KazuruHaloActive(unit);
            for(int i=0;i<3;i++)
            {
                var ring=rings[i];if(ring==null)continue;ring.enabled=visible;if(!visible)continue;
                float width=body.bounds.size.x*.68f,height=body.bounds.size.y*.78f;
                Vector3 center=new Vector3(body.bounds.center.x,unit.transform.position.y+height*.48f,body.transform.position.z);
                float angle=time*2.6f+i*Mathf.PI*2/3;
                float depth=Mathf.Sin(angle);
                Vector3 position=center+new Vector3(Mathf.Cos(angle)*width*.65f,depth*height*.16f,0);
                float scale=width*.29f/Mathf.Max(.01f,tilted.bounds.size.x);
                ring.sprite=tilted;ring.sortingLayerID=body.sortingLayerID;
                ring.sortingOrder=body.sortingOrder+(depth<0?2:-1);
                ring.color=body.color;ring.transform.rotation=Quaternion.Euler(0,0,Mathf.Sin(angle)*22);
                if(protectedNow&&i==0)
                {
                    ring.sprite=halo;position=center+Vector3.up*(height*.59f+Mathf.Sin(time*4)*.015f);
                    scale=width*.62f/Mathf.Max(.01f,halo.bounds.size.x);
                    ring.transform.rotation=Quaternion.identity;ring.sortingOrder=body.sortingOrder+2;
                }
                else if(i==0&&strikeTime>=0&&target!=null&&unit.AttackResetVersion==attackVersion)
                {
                    float start=CombatSimulation.GetAttackWindup(unit)-.1f;
                    float t=(strikeTime-start)/.2f;
                    if(t>=0&&t<1)
                    {
                        var contact=target.transform.position+Vector3.up*height*.42f;
                        position=Vector3.Lerp(position,contact,Mathf.Sin(t*Mathf.PI));
                        ring.sortingOrder=body.sortingOrder+3;ring.transform.rotation=Quaternion.Euler(0,0,t*300);
                    }
                    if(t>=1)strikeTime=-1;
                }
                ring.transform.position=position;
                // World scale stays stable even if a parent sprite has breathing/stretching motion.
                ring.transform.localScale=Vector3.one*scale;
            }
        }
        void OnDisable(){foreach(var ring in rings)if(ring!=null)ring.enabled=false;}
    }
}
