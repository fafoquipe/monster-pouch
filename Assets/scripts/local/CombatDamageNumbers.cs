using System.Collections.Generic;
using System.Globalization;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    // Damage comes from the simulation's impact event. CriticalImpacted follows
    // that event synchronously and upgrades the same number, never a second one.
    public sealed class CombatDamageNumbers : MonoBehaviour
    {
        sealed class Hit
        {
            public Text Text;public BattleUnit Source,Target;public Vector3 Anchor;
            public Vector2 Direction;public int Frame,ResetVersion;public bool Terminal,Dirty;
            public DamageNumberBurst Burst;
        }
        static readonly Vector2[] Directions={new Vector2(.94f,.34f),new Vector2(-.94f,.34f),
            new Vector2(.3f,.95f),new Vector2(-.75f,-.66f),new Vector2(.75f,-.66f),new Vector2(-.3f,.95f)};
        readonly List<Hit> active=new List<Hit>(64);
        readonly Stack<Hit> available=new Stack<Hit>(64);
        readonly Dictionary<BattleUnit,int> slots=new Dictionary<BattleUnit,int>(64);
        static readonly string[] SmallAmounts=CacheAmounts();
        Hit lastImpact;
        MatchController match;CombatSimulation simulation;Camera cameraView;RectTransform layer;Font font;
        const int Limit=64;
        DamageNumberSettings settings; bool ownsSettings;
        public DamageNumberSettings Settings=>settings;
        public int ActiveNumberCount { get { int count=0;foreach(var hit in active)if(hit.Burst.Visible)count++;return count; } }

        public void Configure(MatchController owner,RectTransform canvas,Camera camera)
        {
            Unsubscribe();Clear();
            if(settings==null){settings=Resources.Load<DamageNumberSettings>("MonsterPouch/DamageNumbers");if(settings==null){settings=ScriptableObject.CreateInstance<DamageNumberSettings>();ownsSettings=true;}}
            match=owner;cameraView=camera;simulation=owner.Simulation;
            if(layer==null)
            {
                layer=new GameObject("Combat damage numbers",typeof(RectTransform)).GetComponent<RectTransform>();
                layer.SetParent(canvas,false);layer.anchorMin=Vector2.zero;layer.anchorMax=Vector2.one;
                layer.offsetMin=layer.offsetMax=Vector2.zero;
            }
            font=Resources.Load<Font>("MonsterPouch/UI/Fonts/LilitaOne-Regular")??
                Resources.Load<Font>("MonsterPouch/UI/Fonts/Lato-Bold")??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Warm the existing bounded pool outside the impact path.
            while(available.Count<Limit){var hit=Create();hit.Text.gameObject.SetActive(false);available.Push(hit);}
            match.Impacted+=Impacted;match.CriticalImpacted+=Critical;match.LethalImpacted+=Lethal;
            match.Died+=Died;match.Reviving+=Reviving;match.Changed+=MatchChanged;
        }
        Hit Create()
        {
            var go=new GameObject("damage-number",typeof(RectTransform),typeof(Text),typeof(Outline));
            go.transform.SetParent(layer,false);
            var text=go.GetComponent<Text>();text.font=font;text.alignment=TextAnchor.MiddleCenter;
            text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;
            text.raycastTarget=false;text.rectTransform.sizeDelta=new Vector2(100,55);
            var outline=go.GetComponent<Outline>();outline.effectColor=new Color(.12f,.035f,.055f,1);
            outline.effectDistance=new Vector2(1.8f,-1.8f);outline.useGraphicAlpha=true;
            return new Hit{Text=text};
        }
        void EnsureSimulation()
        {
            if(simulation==match.Simulation)return;Clear();simulation=match.Simulation;
        }
        void Impacted(BattleUnit source,BattleUnit target,int amount)
        {
            lastImpact=null;
            if(!isActiveAndEnabled||match==null||match.Paused||!IsPlayerDamage(source,target)||amount<=0||match.Phase!=MatchPhase.Combat)return;
            EnsureSimulation();
            float impactTime=simulation!=null?simulation.Elapsed:0;
            Hit hit=null;
            for(int i=active.Count-1;i>=0;i--)
            {
                var candidate=active[i];if(candidate.Target!=target)continue;
                if(candidate.ResetVersion!=target.CombatResetVersion){Release(i);continue;}
                if(candidate.Burst.TryAdd(amount,impactTime)){hit=candidate;break;}
                candidate.Burst.Close();Present(candidate);break;
            }
            if(hit==null)
            {
                if(active.Count>=Limit)Release(0);
                hit=available.Pop();hit.Burst.Begin(amount,impactTime);
                hit.Target=target;hit.ResetVersion=target.CombatResetVersion;hit.Terminal=false;
                hit.Anchor=target.transform.position+Vector3.up*.5f;
                slots.TryGetValue(target,out int slot);slots[target]=(slot+1)%Directions.Length;
                hit.Direction=Directions[slot];active.Add(hit);
            }
            hit.Source=source;hit.Frame=Time.frameCount;hit.Dirty=true;lastImpact=hit;
            // HP is already settled before this callback, including simultaneous packets.
            // Show lethal totals now, but allow the remaining packets in this tick to join.
            if(!target.IsAlive){hit.Terminal=true;hit.Anchor=target.transform.position+Vector3.up*.5f;hit.Burst.Show();Present(hit);}
        }
        void Critical(BattleUnit source,BattleUnit target)
        {
            var hit=lastImpact;
            if(hit==null||hit.Frame!=Time.frameCount||hit.Source!=source||hit.Target!=target||!IsPlayerDamage(source,target))return;
            hit.Burst.MarkCritical();hit.Dirty=true;
            if(hit.Terminal)Present(hit);
        }
        void Lethal(BattleUnit source,BattleUnit target){FinishTarget(target,false);}
        void Died(BattleUnit target){FinishTarget(target,true);}
        void Reviving(BattleUnit target,float duration){FinishTarget(target,true);}
        void FinishTarget(BattleUnit target,bool close)
        {
            if(target==null)return;
            foreach(var hit in active)
            {
                if(hit.Target!=target||hit.ResetVersion!=target.CombatResetVersion)continue;
                hit.Terminal=true;hit.Anchor=target.transform.position+Vector3.up*.5f;
                if(close)hit.Burst.Close();else hit.Burst.Show();Present(hit);
            }
        }
        void MatchChanged()
        {
            EnsureSimulation();
            if(match.Phase==MatchPhase.Menu||match.Phase==MatchPhase.Preparation)Clear();
        }
        // The joining player owns Red; presentation follows the local perspective.
        bool IsPlayerDamage(BattleUnit source,BattleUnit target)=>
            match!=null&&source!=null&&target!=null&&source.Side==match.LocalSide&&target.Side==match.RivalSide;
        void LateUpdate(){AdvancePresentation(Time.deltaTime);}
        void AdvancePresentation(float delta)
        {
            if(match==null)return;EnsureSimulation();
            if(match.Phase==MatchPhase.Menu||match.Phase==MatchPhase.Preparation){Clear();return;}
            if(match.Paused)return;
            for(int i=active.Count-1;i>=0;i--)
            {
                var hit=active[i];
                if(hit.Target!=null&&hit.ResetVersion!=hit.Target.CombatResetVersion){Release(i);continue;}
                if(!hit.Terminal&&(hit.Target==null||!hit.Target.isActiveAndEnabled)){Release(i);continue;}
                if(hit.Target!=null&&!hit.Target.IsAlive&&!hit.Terminal)FinishTarget(hit.Target,true);
                if(!hit.Burst.Visible&&hit.Target!=null)hit.Anchor=hit.Target.transform.position+Vector3.up*.5f;
                hit.Burst.Advance(delta);
                if(hit.Terminal||match.Phase!=MatchPhase.Combat)hit.Burst.Close();
                if(hit.Burst.Expired)Release(i);else Present(hit);
            }
        }
        void Present(Hit hit)
        {
            if(!hit.Burst.Visible)return;
            if(hit.Dirty)
            {
                long amount=hit.Burst.Amount;
                hit.Text.text=amount<SmallAmounts.Length?SmallAmounts[(int)amount]:amount.ToString(CultureInfo.InvariantCulture);
                hit.Text.gameObject.name=hit.Burst.Critical?"damage-number-critical":"damage-number-normal";hit.Dirty=false;
            }
            if(!hit.Text.gameObject.activeSelf){hit.Text.gameObject.SetActive(true);hit.Text.transform.SetAsLastSibling();}
            Draw(hit);
        }
        void Draw(Hit hit)
        {
            bool critical=hit.Burst.Critical;float age=hit.Burst.VisibleAge,duration=hit.Burst.Duration;
            float emerge=Mathf.Clamp01(age/.18f),settle=Mathf.Clamp01((age-.12f)/.16f);
            float alpha=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(duration-.25f,duration,hit.Burst.FadeAge));
            hit.Text.color=critical?new Color(1,.16f,.23f,alpha):new Color(1,.94f,.51f,alpha);
            float baseSize=settings.SizeFor((int)System.Math.Min(int.MaxValue,hit.Burst.Amount),critical);
            hit.Text.fontSize=Mathf.RoundToInt(baseSize);
            float pop=Mathf.Lerp(.65f,critical?1.3f:1.16f,1-Mathf.Pow(1-emerge,3));
            float animated=settings.ClampAnimatedSize(hit.Text.fontSize*Mathf.Lerp(pop,1,Mathf.SmoothStep(0,1,settle)));
            hit.Text.rectTransform.localScale=Vector3.one*(animated/hit.Text.fontSize);
            hit.Text.rectTransform.localRotation=Quaternion.Euler(0,0,hit.Direction.x*(critical?-7:-4));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer,cameraView.WorldToScreenPoint(hit.Anchor),null,out var point);
            Vector2 offset=hit.Direction*Mathf.Lerp(12,critical?29:24,1-Mathf.Pow(1-emerge,3));
            point+=offset+Vector2.up*(Mathf.Max(0,age-.25f)*7);
            point.x=Mathf.Clamp(point.x,layer.rect.xMin+32,layer.rect.xMax-32);
            point.y=Mathf.Clamp(point.y,layer.rect.yMin+28,layer.rect.yMax-28);
            hit.Text.rectTransform.anchoredPosition=point;
        }
        void Release(int index)
        {
            var hit=active[index];active.RemoveAt(index);if(hit.Text!=null)hit.Text.gameObject.SetActive(false);
            if(lastImpact==hit)lastImpact=null;
            if(!ReferenceEquals(hit.Target,null))slots.Remove(hit.Target);
            hit.Source=hit.Target=null;available.Push(hit);
        }
        static string[] CacheAmounts(){var result=new string[257];for(int i=0;i<result.Length;i++)result[i]=i.ToString(CultureInfo.InvariantCulture);return result;}
        void Clear(){for(int i=active.Count-1;i>=0;i--)Release(i);slots.Clear();lastImpact=null;}
        void Unsubscribe(){if(match==null)return;match.Impacted-=Impacted;match.CriticalImpacted-=Critical;match.LethalImpacted-=Lethal;match.Died-=Died;match.Reviving-=Reviving;match.Changed-=MatchChanged;}
        void OnDisable(){Clear();}
        void OnDestroy(){Unsubscribe();Clear();if(layer!=null)Destroy(layer.gameObject);if(ownsSettings&&settings!=null)Destroy(settings);}
    }
}
