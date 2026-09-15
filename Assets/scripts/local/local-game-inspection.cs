using System.Linq;
using MonsterPouch.Gameplay.Match;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        void ShowDocumentedInspection(OwnedUnit owned,bool enemy)
        {
            CloseModal();inspected=owned;inspectedEnemy=enemy;
            modal=Panel(CanvasRoot,"Inspection shade",new Rect(0,0,540,960),new Color(0,0,0,.73f));
            var box=BluePanel(modal,"Unit sheet",new Rect(22,90,496,780),ink);
            Portrait(box,owned.Definition.Id,new Rect(18,16,95,101));
            Label(box,owned.Definition.DisplayName+(enemy?" · RIVAL":""),new Rect(129,20,344,40),25,gold,FontStyle.Bold,TextAnchor.MiddleLeft);
            inspectStats=Label(box,StatsText(owned),new Rect(129,65,344,58),14,Color.white,FontStyle.Normal,TextAnchor.MiddleLeft);
            Label(box,owned.Definition.BasicAttackName+" · "+owned.Definition.BaseAbility.Name,new Rect(22,137,452,34),16,cyan,FontStyle.Bold,TextAnchor.MiddleLeft);
            Label(box,owned.Definition.BaseAbility.Description,new Rect(22,179,452,126),16,Color.white,FontStyle.Normal,TextAnchor.UpperLeft);
            for(int i=0;i<3;i++)
            {
                int index=i;var trick=owned.Definition.Tricks[i];if(trick==null)continue;float y=324+i*124;
                Label(box,(owned.Tricks[i]?"★ ":"☆ ")+trick.Name,new Rect(22,y,335,26),16,owned.Tricks[i]?gold:Color.white,FontStyle.Bold,TextAnchor.MiddleLeft);
                Label(box,trick.Description,new Rect(22,y+34,452,78),15,new Color(.81f,.9f,1),FontStyle.Normal,TextAnchor.UpperLeft);
                if(!enemy&&!owned.Tricks[i]&&Match.Phase==MatchPhase.Preparation)
                {
                    if(owned.Definition.IsMonster)
                        Button(box,trick.Cost+" MT",new Rect(376,y,97,29),()=>{Match.UpgradeMonster(index);ShowInspection(owned,false);},gold,ink,13);
                    else if(owned.Copies>0&&Match.Player.Offers.Any(o=>o!=null&&o.UnitId==owned.Definition.Id))
                    {
                        int slot=Enumerable.Range(0,Match.Player.Offers.Count).First(s=>Match.Player.Offers[s]?.UnitId==owned.Definition.Id);
                        long token=Match.Player.Offers[slot].Token;
                        Button(box,trick.Cost+" MT",new Rect(376,y,97,29),()=>{Match.BuyTrick(owned.Definition.Id,index,slot,token);ShowInspection(owned,false);},new Color(.14f,.3f,.39f),gold,12);
                    }
                }
            }
            GameButton(box,"CERRAR",new Rect(24,717,448,43),CloseModal,false,17);
        }
    }
}
