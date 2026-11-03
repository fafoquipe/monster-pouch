using System.Collections;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        // This leg is already released by an impact. It has no new attack windup
        // and survives an attack reset on Anuik, like any other released projectile.
        private IEnumerator ReboundProjectile(BattleUnit actor, BattleUnit origin, BattleUnit target, float duration)
        {
            var style=actor!=null?projectileArt?.Get(actor.BaseStats.DefinitionId):null;
            if(style?.Flight==null||style.Flight.Length==0||origin==null||target==null)yield break;
            var simulation=Match.Simulation;
            Vector3 from=origin.transform.position+Vector3.up*.5f;
            var image=NewEffect("projectile-anuik-bounce");
            float elapsed=0,travel=Mathf.Max(.1f,duration);
            DrawEffect(image,style,style.Flight[0],from,1);
            SizeProjectile(image, actor, "flight");
            while(elapsed<travel&&Match.Phase==MatchPhase.Combat&&Match.Simulation==simulation&&target!=null)
            {
                yield return null;
                if(Match.Phase!=MatchPhase.Combat||Match.Simulation!=simulation||target==null)break;
                elapsed+=Time.deltaTime;
                float progress=Mathf.Clamp01(elapsed/travel);
                Vector3 end=target.transform.position+Vector3.up*.5f;
                Vector3 position=Vector3.Lerp(from,end,progress)+Vector3.up*(Mathf.Sin(progress*Mathf.PI)*.18f);
                DrawEffect(image,style,style.Flight[Mathf.FloorToInt(elapsed*16)%style.Flight.Length],position,1);
                SizeProjectile(image, actor, "flight");
            }
            if(image!=null)Destroy(image.gameObject);
        }
    }
}
