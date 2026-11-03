using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class DocumentedBattleEffects
    {
        readonly HashSet<GameObject> areaFloors = new HashSet<GameObject>();
        Material areaMaterial;
        public int ActiveAreaCount => areaFloors.Count;

        void AreaEffect(BattleUnit source, string kind, BoardCell[] cells)
        {
            if(!isActiveAndEnabled || layer == null || cells == null || cells.Length == 0)return;
            if (kind.StartsWith("edited-area:",System.StringComparison.Ordinal))
            { StartCoroutine(ShowArea(source,kind,cells)); return; }
            if (TryVolkanarArea(source, kind, cells)) return;
            if (TryNaturalisContact(source, kind, cells)) return;
            if (IsNaturalisActive(source, kind)) return; // One authored release burst; persistent states use the central frame.
            if (TryAireolaAreaEffect(source, kind, cells)) return;
            if (TryAquarionAreaEffect(source, kind, cells)) return;
            if(GeotharArea(source,kind,cells))return;
            if(kind=="kazuru-rising-rings") { StartCoroutine(RisingKazuruRings(cells)); return; }
            if(kind=="tsu-chocolate")
            {
                foreach(var cell in cells) if(cell!=null)
                    StartCoroutine(ChocolateMiniExplosions(match.Mapper.GetWorldPosition(cell)+Vector3.up*.5f));
                return;
            }
            if(kind=="stein-lightning"&&source!=null)
            {
                foreach(var cell in cells)StartCoroutine(InstantRay(source.transform.position+Vector3.up*.65f,match.Mapper.GetWorldPosition(cell)+Vector3.up*.5f,true,ContactCells(source,true)));
                return;
            }
            if(kind=="jazar-front-row")
            {
                StartCoroutine(ShowToxicRow(source,cells));
                return;
            }
            StartCoroutine(ShowArea(source,kind,cells));
        }

        IEnumerator ShowToxicRow(BattleUnit source,BoardCell[] cells)
        {
            var simulation=match.Simulation;
            float duration=simulation.GroundEffects
                .Where(effect=>effect.Kind=="radiation"&&effect.Side==source.Side&&cells.Contains(effect.Cell))
                .Select(effect=>effect.RemainingSeconds).DefaultIfEmpty(3f).Max();
            duration=Mathf.Max(.1f,duration);
            var floorRoot=new GameObject("area-floor-jazar-front-row");
            floorRoot.transform.SetParent(transform,false);areaFloors.Add(floorRoot);
            if(areaMaterial==null)areaMaterial=Resources.Load<Material>("MonsterPouch/UnitSpriteMaterial");
            var tiles=new List<SpriteRenderer>();
            var tileCells=new List<BoardCell>();
            var centers=new List<Vector2>();
            var motes=new List<Image>();
            var moteCells=new List<int>();
            var motePhases=new List<float>();
            var uiRoot=EffectRoot("jazar-toxic-row-vfx");uiRoot.SetAsFirstSibling();
            float cellWidth=CellPixels(cells[0]);
            BoardCell nextRow=match.Board.GetCell(cells[0].X,cells[0].Y+1);
            float cellHeight=nextRow==null?cellWidth:Mathf.Abs(Point(match.Mapper.GetWorldPosition(cells[0])).y-Point(match.Mapper.GetWorldPosition(nextRow)).y);
            if(cellHeight<1)cellHeight=cellWidth;
            foreach(var cell in cells)
            {
                if(cell==null)continue;
                Vector3 position=match.Mapper.GetWorldPosition(cell);
                Vector2 center=Round(Point(position));centers.Add(center);tileCells.Add(cell);
                var floor=new GameObject("toxic-cell-"+cell.X).AddComponent<SpriteRenderer>();
                floor.transform.SetParent(floorRoot.transform,false);floor.transform.position=position;
                floor.sharedMaterial=areaMaterial;floor.sprite=PixelSprite("toxic-puddle");floor.sortingOrder=21;
                floor.transform.localScale=new Vector3(Mathf.Abs(match.Mapper.CellSize.x),Mathf.Abs(match.Mapper.CellSize.y),1);
                tiles.Add(floor);
                // Stagger a steady stream of small slime bubbles over every affected tile;
                // each has its own cycle so the row reads as actively bubbling, not as a burst.
                for(int i=0;i<4;i++)
                {
                    var mote=Box("toxic-bubble",uiRoot,new Vector2(10,10),Color.white);
                    mote.sprite=PixelSprite("toxic-bubble");motes.Add(mote);
                    moteCells.Add(tileCells.Count-1);
                    motePhases.Add(Mathf.Repeat((cell.X*7+i*5)*.071f,1f));
                }
            }

            float elapsed=0;
            while(elapsed<duration&&floorRoot!=null&&Current(simulation))
            {
                elapsed+=EffectDelta;
                float t=Mathf.Clamp01(elapsed/duration);
                float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.82f,1,t));
                for(int i=0;i<tiles.Count;i++)
                {
                    float stagger=Mathf.Abs(tileCells[i].X-(BoardManager.Width-1)*.5f)*.055f;
                    float reveal=Mathf.SmoothStep(0,1,(elapsed-stagger)/.28f);
                    float pulse=.84f+.08f*Mathf.Sin(elapsed*3-i*.45f);
                    tiles[i].color=Alpha(Color.white,reveal*pulse*fade);
                    float spread=Mathf.Lerp(.35f,1,reveal)*(1+.015f*Mathf.Sin(elapsed*4+i));
                    tiles[i].transform.localScale=new Vector3(Mathf.Abs(match.Mapper.CellSize.x)*spread,Mathf.Abs(match.Mapper.CellSize.y)*Mathf.Lerp(.35f,1,reveal),1);
                }
                for(int i=0;i<motes.Count;i++)
                {
                    int cellIndex=moteCells[i];
                    float age=Mathf.Repeat(elapsed*(.82f+(i%3)*.11f)+motePhases[i],1f);
                    float stagger=Mathf.Abs(tileCells[cellIndex].X-(BoardManager.Width-1)*.5f)*.055f;
                    float reveal=Mathf.SmoothStep(0,1,(elapsed-stagger)/.28f);
                    float swell=Mathf.SmoothStep(0,1,age/.72f);
                    bool popping=age>=.72f;
                    float pop=popping?Mathf.InverseLerp(.72f,1,age):0;
                    float offsetX=(Mathf.Repeat(i*.618f,.85f)-.42f)*cellWidth;
                    float offsetY=(Mathf.Repeat(i*.371f,.55f)-.27f)*cellHeight;
                    Vector2 p=centers[cellIndex]+new Vector2(offsetX,offsetY-swell*6);
                    motes[i].rectTransform.anchoredPosition=p;
                    float size=Mathf.Lerp(3,10+(i%3)*2,swell);
                    motes[i].sprite=PixelSprite(popping?"toxic-pop":"toxic-bubble");
                    motes[i].rectTransform.sizeDelta=new Vector2(size*(1+pop*.7f),size*(popping?.48f:.84f));
                    motes[i].rectTransform.localRotation=Quaternion.identity;
                    float appear=Mathf.SmoothStep(0,1,age/.12f);
                    motes[i].color=Alpha(Color.white,appear*(1-pop)*reveal*fade*.95f);
                }
                yield return null;
            }
            areaFloors.Remove(floorRoot);if(floorRoot!=null)Destroy(floorRoot);
            ReleaseEffect(uiRoot);
        }

        IEnumerator ShowArea(BattleUnit source,string kind,BoardCell[] cells)
        {
            var simulation=match.Simulation;
            bool fire=kind.StartsWith("gochan"), taunt=kind=="bugaloo-taunt", revival=kind=="anuik-revive-impact";
            Color color=kind=="pokrak-bubbles"?new Color(.7f,.61f,.44f):kind=="rosie-heal"?new Color(.35f,1,.57f):taunt?new Color(1,.12f,.12f):revival?new Color(1,.42f,.63f):
                kind.StartsWith("jazar")?new Color(.6f,1,.12f):Ember;
            float duration=taunt?1f:fire?.27f:revival?.8f:.45f;
            var floorRoot=new GameObject("area-floor-"+kind);
            floorRoot.transform.SetParent(transform,false);areaFloors.Add(floorRoot);
            if(areaMaterial==null)areaMaterial=Resources.Load<Material>("MonsterPouch/UnitSpriteMaterial");
            var tiles=new List<SpriteRenderer>();
            var flames=new List<Image>();
            var cores=new List<Image>();
            var fireOrigins=new List<Vector2>();
            float fireCell=CellPixels(cells[0]);
            var uiRoot=EffectRoot("area-effects-"+kind);
            Vector3 center=Vector3.zero;
            foreach(var cell in cells)
            {
                if(cell==null)continue;
                Vector3 position=match.Mapper.GetWorldPosition(cell);center+=position;
                var floor=new GameObject("affected-cell-"+cell.X+"-"+cell.Y).AddComponent<SpriteRenderer>();
                floor.transform.SetParent(floorRoot.transform,false);floor.transform.position=position;
                // The board has no scene lights. Use the game's unlit sprite material,
                // otherwise URP's default lit renderer turns colored areas black.
                floor.sharedMaterial=areaMaterial;
                floor.sprite=PixelSprite("area-tile");floor.sortingOrder=21;
                floor.transform.localScale=new Vector3(Mathf.Abs(match.Mapper.CellSize.x),Mathf.Abs(match.Mapper.CellSize.y),1);
                tiles.Add(floor);
                if(fire)
                {
                    for(int i=0;i<3;i++)
                    {
                        var flame=Box("area-fire",uiRoot,new Vector2(12,20),Ember);
                        flame.sprite=PixelSprite("dash-flame");
                        flame.rectTransform.anchoredPosition=Round(Point(position)+new Vector2((i-1)*12,-4+i%2*6));
                        flames.Add(flame);fireOrigins.Add(Point(position)+Vector2.right*(i-1)*fireCell*.09f);
                        var core=Box("area-fire-core",flame.transform,new Vector2(6,12),Flame);
                        core.sprite=flame.sprite;cores.Add(core);
                    }
                }
            }
            center/=Mathf.Max(1,tiles.Count);
            Vector2 origin=Point(source!=null && (revival||taunt)?source.transform.position:center);
            var burst=new List<Image>();
            if(revival||taunt)
            {
                for(int i=0;i<16;i++)
                    burst.Add(Box(revival?"revive-explosion-wave":"taunt-wave",uiRoot,new Vector2(8,4),color));
            }
            var hearts=new List<Image>();
            if(revival)for(int i=0;i<8;i++)
            {
                var heart=Box("revive-explosion-heart",uiRoot,Vector2.one*14,Color.white);
                heart.sprite=PixelSprite("heart");hearts.Add(heart);
            }
            float elapsed=0;
            while(elapsed<duration && floorRoot!=null && match.Simulation==simulation &&
                match.Phase!=Gameplay.Match.MatchPhase.Menu && match.Phase!=Gameplay.Match.MatchPhase.Preparation)
            {
                elapsed+=EffectDelta;float t=Mathf.Clamp01(elapsed/duration);
                float fade=Mathf.Clamp01((1-t)*2);
                foreach(var tile in tiles)tile.color=Alpha(color,fade*(fire?.10f:kind=="pokrak-bubbles"?.22f:taunt?.55f:.72f));
                for(int i=0;i<flames.Count;i++)
                {
                    float flicker=Mathf.FloorToInt(elapsed*16+i)%3;
                    flames[i].rectTransform.anchoredPosition=fireOrigins[i]+Vector2.up*(t*fireCell*.16f);
                    flames[i].rectTransform.sizeDelta=new Vector2(.035f,.065f)*fireCell*(1-t*.65f);
                    flames[i].color=Alpha(t>.5f?new Color(.35f,.29f,.24f):Ember,fade*.65f);
                    cores[i].rectTransform.sizeDelta=new Vector2(.014f,.025f)*fireCell*(1-t);
                    cores[i].color=Alpha(new Color(1,.88f,.34f),fade);
                }
                float radius=Mathf.Lerp(8,revival?85:60,Mathf.Clamp01(t*2));
                for(int i=0;i<burst.Count;i++)
                {
                    float angle=i*Mathf.PI*2/burst.Count;
                    var point=origin+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                    burst[i].rectTransform.anchoredPosition=Round(point);
                    burst[i].rectTransform.localRotation=Quaternion.Euler(0,0,angle*Mathf.Rad2Deg+90);
                    burst[i].color=Alpha(t<.18f?Color.white:color,fade);
                }
                for(int i=0;i<hearts.Count;i++)
                {
                    float angle=i*Mathf.PI/4;
                    hearts[i].rectTransform.anchoredPosition=Round(origin+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(12+t*68));
                    hearts[i].rectTransform.localScale=Vector3.one*(1-t*.4f);
                    hearts[i].color=Alpha(Color.white,fade);
                }
                yield return null;
            }
            areaFloors.Remove(floorRoot);if(floorRoot!=null)Destroy(floorRoot);
            ReleaseEffect(uiRoot);
        }

        void ClearAreaVisuals()
        {
            foreach(var root in areaFloors)if(root!=null)Destroy(root);
            areaFloors.Clear();
        }
    }
}
