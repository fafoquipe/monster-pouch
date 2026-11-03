using System.Collections;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Local
{
    // Presentation-only extension of the existing effect component and lifetime.
    public sealed partial class DocumentedBattleEffects
    {
        readonly Dictionary<string,Sprite[]> geotharClips=new Dictionary<string,Sprite[]>();
        bool GeotharOwns(BattleUnit actor)
        {
            string id=actor?.BaseStats.DefinitionId;
            return id=="korran"||id=="golem-tectonico"||id=="la-minera"||id=="protectora-del-mausoleo";
        }
        bool GeotharHasArea(string kind)=>kind=="korran-bastion-imperial"||kind=="korran-el-hogar-resiste"||
            kind=="golem-tectonico-guardia-de-piedra"||kind=="la-minera-fractura"||kind=="protectora-del-mausoleo-sello-ancestral";
        bool GeotharImpact(BattleUnit actor,BattleUnit target,int damage)
        {
            if(!GeotharOwns(actor)||target==null||damage<=0)return false;
            int start=actor.BaseStats.DefinitionId=="protectora-del-mausoleo"?4:0;
            bool heavy=actor.BaseStats.DefinitionId=="golem-tectonico";
            return GeotharBurst(actor.BaseStats.DefinitionId,start,ContactPosition(target)+Vector3.up*(heavy?0:.35f),ContactCells(actor,false));
        }
        bool GeotharArea(BattleUnit actor,string kind,BoardCell[] cells)
        {
            if(!GeotharOwns(actor)||(!GeotharHasArea(kind)&&kind!="korran-bastion-imperial:rupture")||cells==null||cells.Length==0||cells[0]==null)return false;
            string id=actor.BaseStats.DefinitionId;
            int start=id=="protectora-del-mausoleo"||kind=="korran-bastion-imperial:rupture"?8:4;
            return GeotharBurst(id,start,match.Mapper.GetWorldPosition(cells[0])+Vector3.up*.2f,ContactCells(actor,true));
        }
        void GeotharDisplaced(CombatDisplacementSnapshot value)
        {
            if(value.Unit==null||value.Unit.CombatResetVersion!=value.UnitResetVersion||
                value.Unit.BaseStats.DefinitionId!="la-minera"||value.Kind!=CombatDisplacementKind.Tunnel)return;
            if(value.From!=null)GeotharBurst("la-minera",8,match.Mapper.GetWorldPosition(value.From));
            if(value.To!=null)GeotharBurst("la-minera",8,match.Mapper.GetWorldPosition(value.To));
        }
        bool GeotharBurst(string id,int first,Vector3 position,float contactCells=0)
        {
            if(!isActiveAndEnabled||layer==null||match==null||match.Simulation==null)return false;
            string key=id+":"+first;
            if(!geotharClips.TryGetValue(key,out var frames))
            {
                frames=new Sprite[4];
                for(int i=0;i<4;i++)
                {
                    frames[i]=Resources.Load<Sprite>("MonsterPouch/Eronya/Geothar/"+id+"/vfx/frame-"+(first+i).ToString("00"));
                    if(frames[i]==null)return false;
                }
                geotharClips[key]=frames;
            }
            StartCoroutine(GeotharBurstFrames(frames,position,contactCells));return true;
        }
        IEnumerator GeotharBurstFrames(Sprite[] frames,Vector3 position,float contactCells)
        {
            var simulation=match.Simulation;var root=EffectRoot("geothar-impact");
            var image=Box("authored-effect",root,Vector2.one*(contactCells>0?ContactCellPixels(position)*contactCells:64),Color.white);
            root.anchoredPosition=Round(Point(position));float elapsed=0;
            while(root!=null&&Current(simulation)&&elapsed<.4f)
            {
                image.sprite=frames[Mathf.Min(3,Mathf.FloorToInt(elapsed*10))];
                elapsed+=EffectDelta;yield return null;
            }
            ReleaseEffect(root);
        }
    }
}
