using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    // One compact, non-interactive display owns both gauges so they cannot overlap.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UnitVitalsUI : MaskableGraphic
    {
        float hp=1,trail=1,energy,shield;int divisions=5,stars;bool ally,hasEnergy,monster,monsterUpgraded;
        static readonly Color Border=new Color(.06f,.10f,.16f),Empty=new Color(.15f,.21f,.28f);
        public float ShieldFraction=>shield;
        public float HealthFraction=>hp;
        public float EnergyFraction=>energy;
        public bool ShowsEnergy=>hasEnergy;
        // Lowest drawn pixel, including the left-hand badge. Keep the whole HUD above the head.
        public float BottomEdge => shield>0 ? -1 : hasEnergy ? 6 : monster ? 12 : stars==2 ? 9.9f : stars==3 ? 11.9f : 14;
        bool initialized;
        UnitPositionMarkUI positionMark;
        Image leaderBadge;
        bool usesFactionBadge;
        bool RefreshLeaderBadge(string characterId,bool show,bool upgraded)
        {
            var sprite=show?LeaderBadgeArtwork.Get(characterId,upgraded):null;
            bool next=sprite!=null,changed=next!=usesFactionBadge;
            if(next&&leaderBadge==null)
            {
                var go=new GameObject("Faction leader emblem",typeof(RectTransform),typeof(Image));
                go.transform.SetParent(transform,false);leaderBadge=go.GetComponent<Image>();
                leaderBadge.raycastTarget=false;leaderBadge.preserveAspect=true;
                var rt=leaderBadge.rectTransform;rt.anchorMin=rt.anchorMax=Vector2.zero;rt.pivot=Vector2.zero;
                rt.anchoredPosition=new Vector2(-23,12);rt.sizeDelta=new Vector2(22,18);
            }
            if(leaderBadge!=null)
            {
                if(leaderBadge.sprite!=sprite){leaderBadge.sprite=sprite;changed=true;}
                leaderBadge.enabled=next;
            }
            usesFactionBadge=next;return changed;
        }
        public void Refresh(BattleUnit unit,OwnedUnit owned,float delta,BoardSide localSide)
        {
            raycastTarget=false;
            if(positionMark==null&&owned.MarkedPositionRound>0)
            {
                var mark=new GameObject("Tauruk position mark",typeof(RectTransform),typeof(UnitPositionMarkUI));
                mark.transform.SetParent(transform,false);
                positionMark=mark.GetComponent<UnitPositionMarkUI>();
            }
            if(positionMark!=null)positionMark.Refresh(unit,owned);
            float nextHp=Mathf.Clamp01((float)unit.CurrentHealth/Mathf.Max(1,unit.BaseStats.MaxHealth));
            float nextTrail=nextHp>trail?nextHp:Mathf.MoveTowards(trail,nextHp,Mathf.Max(0,delta)*1.7f);
            float nextShield=unit.IsAlive?Mathf.Clamp01((float)unit.Shield/Mathf.Max(1,unit.BaseStats.MaxHealth)):0;
            bool nextHasEnergy=unit.BaseStats.EnergyMax>0;
            float nextEnergy=nextHasEnergy?Mathf.Clamp01(unit.Energy/unit.BaseStats.EnergyMax):0;
            int nextDivisions=Mathf.Clamp(Mathf.CeilToInt(unit.BaseStats.MaxHealth/10f),3,10);
            bool nextAlly=unit.Side==localSide;
            bool nextMonster=owned.Definition.IsMonster;
            bool nextMonsterUpgraded=nextMonster&&owned.HasMonsterUpgrade;
            int nextStars=nextMonster?0:Mathf.Clamp(owned.TrickCount,0,3);
            bool badgeChanged=RefreshLeaderBadge(owned.Definition.Id,nextMonster,nextMonsterUpgraded);
            // Movement changes the RectTransform, not the bar mesh. Keep static
            // gauges out of Canvas rebuilds while still drawing every damage-trail step.
            if(!badgeChanged&&initialized&&hp==nextHp&&trail==nextTrail&&energy==nextEnergy&&shield==nextShield&&
                hasEnergy==nextHasEnergy&&divisions==nextDivisions&&ally==nextAlly&&
                monster==nextMonster&&monsterUpgraded==nextMonsterUpgraded&&stars==nextStars)return;
            hp=nextHp;trail=nextTrail;energy=nextEnergy;shield=nextShield;hasEnergy=nextHasEnergy;
            divisions=nextDivisions;ally=nextAlly;monster=nextMonster;
            monsterUpgraded=nextMonsterUpgraded;stars=nextStars;initialized=true;
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var origin=rectTransform.rect.min;
            void Box(float x,float y,float w,float h,Color c)
            {
                if(w<=0||h<=0)return;int n=vh.currentVertCount;
                vh.AddVert(origin+new Vector2(x,y),c,Vector2.zero);vh.AddVert(origin+new Vector2(x,y+h),c,Vector2.zero);
                vh.AddVert(origin+new Vector2(x+w,y+h),c,Vector2.zero);vh.AddVert(origin+new Vector2(x+w,y),c,Vector2.zero);
                vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
            Box(0,14,64,12,Border);Box(1,15,62,10,new Color(.75f,.83f,.89f));Box(2,16,60,8,Empty);
            Box(2,16,60*trail,8,new Color(1,.96f,.73f));
            Color health=ally?new Color(.24f,.66f,.84f):new Color(.85f,.29f,.32f);
            Box(2,16,60*hp,8,health);Box(2,21,60*hp,3,Color.Lerp(health,Color.white,ally?.4f:.22f));
            for(int i=1;i<divisions;i++)Box(2+60f*i/divisions,16,1,8,new Color(.10f,.23f,.18f,.72f));
            // White on both sides; width uses the same health-per-pixel scale as HP.
            // Keep it below HP and energy to leave the status medallions untouched.
            if(shield>0)
            {
                Box(1,-1,62,7,Border);Box(2,0,60,5,Empty);
                Box(2,0,60*shield,5,Color.white);
            }
            if(hasEnergy)
            {
                Box(1,6,62,8,Border);Box(2,7,60,6,Empty);
                Color charge=energy>=.999f?new Color(1,.94f,.48f):new Color(1,.71f,.16f);
                Box(2,7,60*energy,6,charge);Box(2,11,60*energy,2,Color.Lerp(charge,Color.white,.55f));
                for(int i=1;i<4;i++)Box(2+15*i,7,1,6,Border);
            }
            // Fit the shared geometry to a width that leaves a gap between adjacent cells.
            float horizontalScale=rectTransform.rect.width/64f;
            var vertex=new UIVertex();
            for(int i=0;i<vh.currentVertCount;i++)
            {
                vh.PopulateUIVertex(ref vertex,i);
                vertex.position.x=origin.x+(vertex.position.x-origin.x)*horizontalScale;
                vh.SetUIVertex(vertex,i);
            }
            // Stars sit beside the health gauge, leaving the entire upper row for
            // circular status icons. Their size is independent of bar scaling.
            if(monster)
            {
                if(!usesFactionBadge)DrawMonsterBadge(vh,origin+new Vector2(-12,17),monsterUpgraded);
            }
            else
            {
                for(int i=0;i<stars;i++)
                {
                    Vector2 offset=stars==1?new Vector2(-7,20):
                        stars==2?new Vector2(-7,25-i*10):
                        i==0?new Vector2(-9,27):new Vector2(-14+(i-1)*10,17);
                    DrawStar(vh,origin+offset);
                }
            }
        }

        static void StarShape(VertexHelper vh,Vector2 center,float radius,Color color)
        {
            int start=vh.currentVertCount;vh.AddVert(center,color,Vector2.zero);
            for(int i=0;i<10;i++)
            {
                float angle=(90+i*36)*Mathf.Deg2Rad,r=i%2==0?radius:radius*.45f;
                vh.AddVert(center+new Vector2(Mathf.Cos(angle)*r,Mathf.Sin(angle)*r),color,Vector2.zero);
            }
            for(int i=0;i<10;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%10);
        }
        static void DrawStar(VertexHelper vh,Vector2 center)
        {
            StarShape(vh,center,5.1f,Border);
            StarShape(vh,center,4.4f,new Color(1,.62f,.06f));
            StarShape(vh,center+Vector2.up*.5f,3.6f,new Color(1,.91f,.35f));
        }
        static void Quad(VertexHelper vh,Vector2 center,Color color,Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {
            int n=vh.currentVertCount;
            vh.AddVert(center+a,color,Vector2.zero);vh.AddVert(center+b,color,Vector2.zero);
            vh.AddVert(center+c,color,Vector2.zero);vh.AddVert(center+d,color,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
        static void DrawMonsterBadge(VertexHelper vh,Vector2 center,bool upgraded)
        {
            Color horn=upgraded?new Color(1,.12f,.16f):new Color(1,.86f,.45f);
            Color tip=upgraded?new Color(1,.52f,.32f):new Color(1,.97f,.76f);
            for(int side=-1;side<=1;side+=2)
            {
                Vector2 P(float x,float y)=>new Vector2(x*side,y);
                Quad(vh,center,Border,P(3,1),P(10,5),P(11,13),P(5,7));
                Quad(vh,center,horn,P(4,2),P(8.5f,5.5f),P(9.5f,11.5f),P(6,6));
                Quad(vh,center,tip,P(8.5f,8),P(9.5f,11.5f),P(8,10),P(7.5f,7));
            }
            // The Monster's horned head replaces the hero crown; only its horns
            // change colour when the Monster improvement has been purchased.
            Quad(vh,center,Border,new Vector2(-8,6),new Vector2(8,6),new Vector2(6,-5),new Vector2(-6,-5));
            Quad(vh,center,new Color(.35f,.25f,.67f),new Vector2(-6.5f,4.5f),new Vector2(6.5f,4.5f),new Vector2(4.5f,-3.5f),new Vector2(-4.5f,-3.5f));
            for(int side=-1;side<=1;side+=2)
            {
                Vector2 P(float x,float y)=>new Vector2(x*side,y);
                Quad(vh,center,new Color(1,.88f,.32f),P(1,0),P(5,2),P(4,-1),P(2,-1));
            }
        }
    }
}
