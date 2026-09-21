using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    // One reusable effect vocabulary for the entire roster. Combat owns every trigger.
    public sealed class CombatStatusEffects : MonoBehaviour
    {
        sealed class Effect
        {
            public RectTransform Root; public Image Dome; public Image[] Sparks; public Text Caption;
            public SpriteRenderer Floor, Body; public BattleUnit Actor; public Vector3 Anchor;
            public string Kind; public float Age, Duration, Size; public bool Persistent, Follow;
        }
        MatchController match; Camera cameraView; RectTransform layer; Font font;
        CombatSimulation simulation; ObjectPool<Effect> pool; Material glowMaterial;
        readonly List<Effect> active=new List<Effect>(64);
        readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        const int Limit=64;
        public int ActiveEffectCount=>active.Count;
        public int CreatedEffectCount=>pool?.CountAll??0;

        public void Configure(MatchController owner,RectTransform canvas,Camera camera)
        {
            Unsubscribe();Clear();match=owner;cameraView=camera;
            if(layer==null)
            {
                layer=new GameObject("Combat status effects",typeof(RectTransform)).GetComponent<RectTransform>();
                layer.SetParent(canvas,false);layer.anchorMin=Vector2.zero;layer.anchorMax=Vector2.one;layer.offsetMin=layer.offsetMax=Vector2.zero;
            }
            font=Resources.Load<Font>("MonsterPouch/PixelFont");
            if(glowMaterial==null)glowMaterial=new Material(Resources.Load<Material>("MonsterPouch/GroundGlowMaterial"));
            var a=match.Mapper.GetWorldPosition(match.Board.GetCell(0,0));
            var b=match.Mapper.GetWorldPosition(match.Board.GetCell(BoardManager.Width-1,BoardManager.Height-1));
            Vector2 half=new Vector2(Mathf.Abs(match.Mapper.CellSize.x),Mathf.Abs(match.Mapper.CellSize.y))*.5f;
            glowMaterial.SetVector("_BoardRect",new Vector4(Mathf.Min(a.x,b.x)-half.x,Mathf.Min(a.y,b.y)-half.y,Mathf.Max(a.x,b.x)+half.x,Mathf.Max(a.y,b.y)+half.y));
            pool??=new ObjectPool<Effect>(CreateEffect,null,null,DestroyEffect,false,16,Limit);
            match.Healed+=Healed;match.CriticalImpacted+=Critical;match.AttackReset+=ResetAttack;match.HealingArea+=HealArea;
            match.Reviving+=Reviving;simulation=match.Simulation;
        }
        Effect CreateEffect()
        {
            var fx=new Effect();
            fx.Root=new GameObject("pooled-effect",typeof(RectTransform)).GetComponent<RectTransform>();fx.Root.SetParent(layer,false);
            fx.Root.anchorMin=fx.Root.anchorMax=fx.Root.pivot=Vector2.one*.5f;fx.Root.sizeDelta=Vector2.zero;
            var domeGo=new GameObject("torso-dome",typeof(RectTransform),typeof(Image));domeGo.transform.SetParent(fx.Root,false);
            fx.Dome=domeGo.GetComponent<Image>();fx.Dome.raycastTarget=false;
            fx.Sparks=new Image[6];
            for(int i=0;i<fx.Sparks.Length;i++)
            {
                var go=new GameObject("effect-particle",typeof(RectTransform),typeof(Image));go.transform.SetParent(fx.Root,false);
                var image=go.GetComponent<Image>();image.raycastTarget=false;fx.Sparks[i]=image;
            }
            var textGo=new GameObject("effect-caption",typeof(RectTransform),typeof(Text));textGo.transform.SetParent(fx.Root,false);
            fx.Caption=textGo.GetComponent<Text>();fx.Caption.font=font;fx.Caption.fontSize=14;fx.Caption.alignment=TextAnchor.MiddleCenter;
            fx.Caption.raycastTarget=false;fx.Caption.rectTransform.sizeDelta=new Vector2(110,26);
            fx.Floor=new GameObject("floor-light").AddComponent<SpriteRenderer>();fx.Floor.transform.SetParent(transform,false);
            fx.Floor.sharedMaterial=glowMaterial;fx.Floor.sortingOrder=21;
            return fx;
        }
        void DestroyEffect(Effect fx){if(fx.Root!=null)Destroy(fx.Root.gameObject);if(fx.Floor!=null)Destroy(fx.Floor.gameObject);}
        Effect Spawn(string kind,BattleUnit actor,Vector3 position,float duration,float size=1,bool persistent=false)
        {
            if(!isActiveAndEnabled||match==null||match.Phase!=MatchPhase.Combat||pool==null)return null;
            EnsureSimulation();
            foreach(var existing in active)
                if(existing.Actor==actor&&actor!=null&&existing.Kind==kind){existing.Age=0;existing.Duration=duration;return existing;}
            if(active.Count>=Limit)return null;
            var fx=pool.Get();fx.Kind=kind;fx.Actor=actor;fx.Anchor=position;fx.Age=0;fx.Duration=duration;fx.Size=size;fx.Persistent=persistent;
            fx.Body=actor!=null?actor.GetComponent<UnitPresentation>()?.Renderer:null;
            fx.Dome.enabled=kind=="reflect"||kind=="protection";
            fx.Dome.sprite=fx.Dome.enabled?SpriteFor(kind=="reflect"?"sphere":"dome"):null;
            fx.Follow=persistent||kind=="heal"||kind=="revive";
            fx.Root.name="fx-"+kind;fx.Root.gameObject.SetActive(true);fx.Floor.name="floor-"+kind;fx.Floor.gameObject.SetActive(true);
            fx.Caption.text=kind=="critical"?"¡CRÍTICO!":kind=="reset"?"":kind=="sleep"?"ZZZ":"";
            fx.Floor.sprite=SpriteFor(kind=="heal"||kind=="heal-area"?"glow":"ring");
            foreach(var spark in fx.Sparks){spark.sprite=SpriteFor(kind=="stun"||kind=="critical"?"star":kind=="slow"?"chevron":"spark");spark.enabled=true;}
            if(kind=="protection")fx.Sparks[0].sprite=SpriteFor("shield");
            active.Add(fx);Draw(fx);return fx;
        }
        void Healed(BattleUnit actor,int amount){if(actor!=null&&amount>0)Spawn("heal",actor,actor.transform.position,.9f,1.25f);}
        void Critical(BattleUnit source,BattleUnit target){if(target!=null)Spawn("critical",target,target.transform.position,.5f);}
        void ResetAttack(BattleUnit actor){if(actor!=null&&actor.IsAlive)Spawn("reset",actor,actor.transform.position,.35f);}
        void HealArea(BoardCell cell){if(cell!=null)Spawn("heal-area",null,match.Mapper.GetWorldPosition(cell),1.2f,3);}
        void Reviving(BattleUnit actor,float duration){if(actor!=null)Spawn("revive",actor,actor.transform.position,duration,1.2f);}
        static string Status(BattleUnit u)=>u==null||!u.IsAlive||u.IsReviving?null:
            u.IsStunned?"stun":u.IsRooted?"root":u.IsSleeping?"sleep":u.IsSlowed?"slow":u.Shield>0?"shield":u.IsInvisible?"stealth":null;
        static bool HasStatus(BattleUnit actor,string kind)
        {
            if(actor==null||!actor.IsAlive||actor.IsReviving)return false;
            return kind=="reflect"?actor.IsReflecting:kind=="protection"?actor.IsProtected:Status(actor)==kind;
        }
        void EnsureStatus(BattleUnit actor,string kind)
        {
            if(kind==null||!HasStatus(actor,kind))return;
            foreach(var fx in active)if(fx.Persistent&&fx.Actor==actor&&fx.Kind==kind)return;
            Spawn(kind,actor,actor.transform.position,1,1,true);
        }
        void EnsureSimulation(){if(simulation==match.Simulation)return;Clear();simulation=match.Simulation;}
        void LateUpdate()
        {
            if(match==null||pool==null)return;EnsureSimulation();
            if(match.Phase==MatchPhase.Menu||match.Phase==MatchPhase.Preparation){Clear();return;}
            if(match.Paused)return;
            for(int i=active.Count-1;i>=0;i--)
            {
                var fx=active[i];
                if(fx.Persistent&&(match.Phase!=MatchPhase.Combat||!HasStatus(fx.Actor,fx.Kind))){Release(i);continue;}
                fx.Age+=Time.deltaTime;
                if(!fx.Persistent&&fx.Age>=fx.Duration){Release(i);continue;}
                if(fx.Follow&&fx.Actor!=null)fx.Anchor=fx.Actor.transform.position;
                Draw(fx);
            }
            if(match.Phase!=MatchPhase.Combat)return;
            foreach(var pair in match.Actors)
            {
                var actor=pair.Value;
                EnsureStatus(actor,Status(actor));EnsureStatus(actor,"reflect");EnsureStatus(actor,"protection");
            }
        }
        void Draw(Effect fx)
        {
            float t=fx.Persistent?Mathf.Repeat(fx.Age,1.2f)/1.2f:Mathf.Clamp01(fx.Age/Mathf.Max(.01f,fx.Duration));
            bool heal=fx.Kind=="heal"||fx.Kind=="heal-area",critical=fx.Kind=="critical",reset=fx.Kind=="reset";
            Color color=heal?new Color(.22f,1,.3f):critical?new Color(1,.75f,.12f):fx.Kind=="stun"?new Color(1,.86f,.2f):
                fx.Kind=="reflect"?new Color(1,.16f,.48f):fx.Kind=="protection"?new Color(.2f,.94f,1):
                fx.Kind=="root"?new Color(1,.36f,.74f):fx.Kind=="stealth"||fx.Kind=="revive"?new Color(.75f,.38f,1):new Color(.3f,.82f,1);
            float fade=fx.Persistent?.85f:Mathf.Min(1,(1-t)*3),pulse=.85f+.15f*Mathf.Sin(fx.Age*8);
            fx.Floor.enabled=!critical&&!reset;
            fx.Floor.color=new Color(color.r,color.g,color.b,fade*(heal?.52f:.22f)*pulse);
            fx.Floor.transform.position=fx.Anchor;
            fx.Floor.transform.localScale=new Vector3(Mathf.Abs(match.Mapper.CellSize.x)*fx.Size*(.85f+t*.15f),Mathf.Abs(match.Mapper.CellSize.y)*fx.Size*.78f,1);
            if(fx.Dome.enabled){DrawDome(fx,color,pulse);return;}
            Vector3 position=fx.Anchor+(heal?Vector3.zero:Vector3.up*(fx.Kind=="slow"||fx.Kind=="root"?.25f:1f));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer,cameraView.WorldToScreenPoint(position),null,out var point);
            fx.Root.anchoredPosition=new Vector2(Mathf.Round(point.x),Mathf.Round(point.y));
            for(int i=0;i<fx.Sparks.Length;i++)
            {
                var image=fx.Sparks[i];float angle=i*Mathf.PI/3+fx.Age*(fx.Kind=="stun"?3:1);
                Vector2 p;
                if(heal)p=new Vector2((i-2.5f)*8,Mathf.Repeat(t*42+i*9,48));
                else if(fx.Kind=="slow")p=new Vector2((i%3-1)*15,12-Mathf.Repeat(fx.Age*18+(i/3)*14,28));
                else p=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)*(fx.Persistent?.4f:1))*(fx.Persistent?22:8+t*(critical?35:22));
                image.rectTransform.anchoredPosition=new Vector2(Mathf.Round(p.x),Mathf.Round(p.y));
                float size=heal?4:critical?10*(1-t)+3:reset?5:8;
                image.rectTransform.sizeDelta=Vector2.one*Mathf.Round(size);image.color=new Color(color.r,color.g,color.b,fade);
                image.rectTransform.localRotation=Quaternion.Euler(0,0,critical?i*60+t*90:reset?45:0);
            }
            fx.Caption.color=new Color(color.r,color.g,color.b,fade);fx.Caption.rectTransform.anchoredPosition=new Vector2(0,30+t*15);
            fx.Caption.transform.localScale=Vector3.one*(critical?1+.15f*Mathf.Sin(t*Mathf.PI):1);
        }
        void DrawDome(Effect fx,Color color,float pulse)
        {
            // Project an enclosing sphere around the body, including the upper arc.
            // This canvas layer stays visible over the torso, unlike a floor-only ring.
            Bounds bounds=fx.Body!=null?fx.Body.bounds:new Bounds(fx.Anchor+Vector3.up*.65f,new Vector3(1.1f,1.3f,.1f));
            float outer=fx.Kind=="protection"?1.24f:1.08f;
            float width=Mathf.Max(.9f,bounds.size.x)*outer;
            float height=Mathf.Max(1.1f,bounds.size.y)*outer;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer,cameraView.WorldToScreenPoint(bounds.center),null,out var center);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer,cameraView.WorldToScreenPoint(bounds.center+new Vector3(width*.5f,height*.5f,0)),null,out var corner);
            Vector2 size=new Vector2(Mathf.Max(36,Mathf.Abs(corner.x-center.x)*2),Mathf.Max(44,Mathf.Abs(corner.y-center.y)*2));
            fx.Root.anchoredPosition=new Vector2(Mathf.Round(center.x),Mathf.Round(center.y));
            fx.Dome.rectTransform.sizeDelta=size;
            fx.Dome.color=new Color(color.r,color.g,color.b,.7f+.18f*pulse);
            for(int i=0;i<fx.Sparks.Length;i++)
            {
                bool emblem=fx.Kind=="protection"&&i==0;
                float angle=i*Mathf.PI/3+fx.Age*(fx.Kind=="reflect"?1.1f:-.5f);
                var spark=fx.Sparks[i];
                spark.rectTransform.anchoredPosition=emblem?new Vector2(size.x*.34f,-size.y*.24f):new Vector2(Mathf.Cos(angle)*size.x*.46f,Mathf.Sin(angle)*size.y*.46f);
                spark.rectTransform.sizeDelta=Vector2.one*(emblem?19:4);
                spark.rectTransform.localRotation=Quaternion.identity;
                spark.color=new Color(color.r,color.g,color.b,emblem?1:.55f+.35f*pulse);
            }
        }
        Sprite SpriteFor(string kind)
        {
            if(sprites.TryGetValue(kind,out var sprite))return sprite;
            int size=kind=="sphere"||kind=="dome"?64:32;var data=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float half=(size-1)*.5f,dx=(x-half)/half,dy=(y-half)/half,r=Mathf.Sqrt(dx*dx+dy*dy);float alpha=0;
                if(kind=="glow")alpha=r<1?Mathf.Ceil((1-r)*5)/5:0;
                else if(kind=="ring")alpha=r>.72f&&r<.94f?1:0;
                else if(kind=="star")alpha=Mathf.Abs(dx)*Mathf.Abs(dy)<.1f&&Mathf.Abs(dx)+Mathf.Abs(dy)<1?1:0;
                else if(kind=="chevron")alpha=Mathf.Abs(dy-(Mathf.Abs(dx)-.4f))<.17f&&Mathf.Abs(dx)<.85f?1:0;
                else if(kind=="sphere"||kind=="dome")
                {
                    if(r<.95f)
                    {
                        alpha=kind=="sphere"?.065f:.025f;
                        if(r>.88f)alpha=1;
                        float equator=Mathf.Sqrt(dx*dx+dy*dy*12);
                        if(kind=="sphere"&&equator>.88f&&equator<.95f)alpha=Mathf.Max(alpha,.38f);
                        // A bright inner highlight makes the upper hemisphere readable.
                        if(dy>.28f&&r>.78f&&r<.81f)alpha=.65f;
                    }
                }
                else if(kind=="shield")
                {
                    float edge=dy>0?.72f:.72f*(1+dy);
                    if(dy<.7f&&dy>-.95f&&Mathf.Abs(dx)<edge)
                        alpha=Mathf.Abs(dx)>edge-.13f||dy>.53f||Mathf.Abs(dx)<.11f||Mathf.Abs(dy)<.1f?1:.16f;
                }
                else alpha=Mathf.Abs(dx)<.6f&&Mathf.Abs(dy)<.6f?1:0;
                data[y*size+x]=new Color(1,1,1,alpha);
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="status-"+kind,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(data);texture.Apply(false,true);
            sprite=Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,size);sprites.Add(kind,sprite);return sprite;
        }
        void Release(int index){var fx=active[index];active.RemoveAt(index);fx.Root.gameObject.SetActive(false);fx.Floor.gameObject.SetActive(false);fx.Actor=null;fx.Body=null;pool.Release(fx);}
        void Clear(){for(int i=active.Count-1;i>=0;i--)Release(i);}
        void Unsubscribe()
        {
            if(match==null)return;match.Healed-=Healed;match.CriticalImpacted-=Critical;match.AttackReset-=ResetAttack;match.HealingArea-=HealArea;match.Reviving-=Reviving;
        }
        void OnDisable(){Clear();}
        void OnDestroy()
        {
            Unsubscribe();Clear();pool?.Dispose();if(layer!=null)Destroy(layer.gameObject);if(glowMaterial!=null)Destroy(glowMaterial);
            foreach(var sprite in sprites.Values){Destroy(sprite.texture);Destroy(sprite);}
        }
    }
}
