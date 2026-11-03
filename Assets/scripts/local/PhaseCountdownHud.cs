using System.Globalization;
using MonsterPouch.Gameplay.Match;
using UnityEngine;
using UnityEngine.UI;
namespace MonsterPouch.Local
{
    // Reads the match clock; it never starts/stops a phase or owns another countdown.
    public sealed class PhaseCountdownHud : MonoBehaviour
    {
        static readonly Color NormalNumber=new Color(1,.90f,.65f,.28f);
        static readonly Color UrgentNumber=new Color32(255,139,36,242);
        static readonly Color FinalNumber=new Color32(184,8,22,250);
        MatchController match; Camera view; RectTransform root; CanvasGroup group; Text number; Outline outline;
        MatchPhase phase; int round=-1,shown=-1; float age;
        public int VisibleNumber {get;private set;}
        bool suppressed;
        public bool Suppressed
        {
            get=>suppressed;
            set {suppressed=value;if(value&&group!=null)group.alpha=0;}
        }
        public static int NumberFor(MatchPhase phase,float remaining)
        {
            int threshold=phase==MatchPhase.Preparation?10:phase==MatchPhase.Combat?5:0;
            if(threshold==0||float.IsNaN(remaining)||float.IsInfinity(remaining)||remaining<=0||remaining>threshold)return 0;
            return Mathf.CeilToInt(remaining);
        }
        public void Configure(MatchController owner,RectTransform canvas,Camera camera,Font font)
        {
            match=owner;view=camera;
            root=new GameObject("Central phase countdown",typeof(RectTransform),typeof(CanvasGroup)).GetComponent<RectTransform>();
            root.SetParent(canvas,false);root.anchorMin=root.anchorMax=root.pivot=Vector2.one*.5f;
            root.anchoredPosition=Vector2.zero;root.sizeDelta=new Vector2(180,150);
            group=root.GetComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;group.alpha=0;
            var go=new GameObject("Countdown number",typeof(RectTransform),typeof(Text),typeof(Outline));
            number=go.GetComponent<Text>();number.rectTransform.SetParent(root,false);
            number.rectTransform.anchorMin=Vector2.zero;number.rectTransform.anchorMax=Vector2.one;
            number.rectTransform.offsetMin=number.rectTransform.offsetMax=Vector2.zero;
            number.font=font;number.fontSize=112;number.alignment=TextAnchor.MiddleCenter;number.supportRichText=false;
            number.horizontalOverflow=HorizontalWrapMode.Overflow;number.verticalOverflow=VerticalWrapMode.Overflow;
            number.raycastTarget=false;number.color=NormalNumber;
            outline=go.GetComponent<Outline>();outline.effectDistance=new Vector2(3,-3);outline.useGraphicAlpha=false;
            outline.effectColor=new Color32(29,8,9,245);
        }
        void LateUpdate()
        {
            if(match==null||group==null)return;
            int next=NumberFor(match.Phase,match.Remaining);
            if(phase!=match.Phase||round!=match.Round){phase=match.Phase;round=match.Round;shown=-1;}
            bool changed=next!=shown;
            if(changed)
            {
                shown=next;age=0;
                if(next>0)
                {
                    number.text=next.ToString(CultureInfo.InvariantCulture);
                    number.color=next==1?FinalNumber:next<=5?UrgentNumber:NormalNumber;
                }
            }
            VisibleNumber=next;
            if(next==0||match.Paused||Suppressed){group.alpha=0;return;}
            if(!changed)age+=Time.unscaledDeltaTime;
            float enter=Mathf.Clamp01(age/.16f),exit=Mathf.Clamp01((age-.40f)/.30f);
            float punch=1+Mathf.Sin(enter*Mathf.PI)*.18f;
            root.localScale=Vector3.one*punch;root.localRotation=Quaternion.Euler(0,0,Mathf.Sin(enter*Mathf.PI)*(next%2==0?3:-3));
            group.alpha=(1-exit)*Mathf.Clamp01(age/.045f);
            if(match.Phase==MatchPhase.Combat)
            {
                // Reduce opacity further when a fighter passes behind the numeral.
                foreach(var actor in match.Actors.Values)
                {
                    if(actor==null||!actor.IsAlive)continue;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(root,view.WorldToScreenPoint(actor.transform.position+Vector3.up*.5f),null,out var p);
                    if(Mathf.Abs(p.x)<90&&Mathf.Abs(p.y)<82){group.alpha*=.48f;break;}
                }
            }
            root.SetAsLastSibling();
        }
        void OnDestroy(){if(root!=null)Destroy(root.gameObject);}
    }
}

