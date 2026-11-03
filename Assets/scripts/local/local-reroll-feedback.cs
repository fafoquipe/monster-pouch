using MonsterPouch.Gameplay.Match;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        [Header("Reroll feedback")]
        [SerializeField, Min(.25f)] float rerollSpinDuration=.6f;
        RectTransform rerollFeedbackRoot,rerollSpinner;
        Button rerollButton;
        PouchState rerollFeedbackPlayer;
        int rerollFeedbackRound;
        float rerollFeedbackStarted=-1;
        bool rerollPressed;

        void RenderRerollButton(RectTransform tray,Rect rect,int version)
        {
            var hit=HitArea(tray,"Reroll",rect);
            rerollButton=hit.gameObject.AddComponent<Button>();
            rerollButton.onClick.AddListener(()=>{PlaySound("button");RequestReroll(version);});
            hit.gameObject.AddComponent<RerollPressFeedback>().Configure(this,rerollButton);
            rerollPressed=false;
            // Keep the hit area fixed; the centered artwork compresses and rebounds inside it.
            rerollFeedbackRoot=Panel(hit,"Reroll feedback",new Rect(0,0,72,72),Color.clear,false);
            rerollFeedbackRoot.pivot=new Vector2(.5f,.5f);
            rerollFeedbackRoot.anchoredPosition=new Vector2(36,-36);
            rerollSpinner=Panel(rerollFeedbackRoot,"Reroll arrow",new Rect(6,2,60,60),Color.clear,false);
            rerollSpinner.pivot=new Vector2(.5f,.5f);rerollSpinner.anchoredPosition=new Vector2(36,-32);
            var icon=rerollSpinner.gameObject.AddComponent<Image>();
            icon.sprite=RerollIcon;icon.preserveAspect=true;icon.raycastTarget=false;
            var count=Label(rerollFeedbackRoot,Match.Player.Rerolls.ToString(),new Rect(0,22,72,46),30,Color.white,FontStyle.Bold);
            count.name="Reroll count";
            var outline=count.gameObject.AddComponent<Outline>();
            outline.effectColor=new Color(.015f,.09f,.025f,1);outline.effectDistance=new Vector2(2,-2);
            UpdateRerollFeedback();
        }

        internal void SetRerollPressed(bool pressed)
        {
            rerollPressed=pressed;UpdateRerollFeedback();
        }

        void StartRerollFeedback()
        {
            rerollFeedbackPlayer=Match.Player;rerollFeedbackRound=Match.Round;
            rerollFeedbackStarted=Time.unscaledTime;rerollPressed=false;
            UpdateRerollFeedback();
        }

        void UpdateRerollFeedback()
        {
            if(rerollFeedbackRoot==null||!rerollFeedbackRoot.gameObject.activeInHierarchy)return;
            rerollButton.interactable=BriefReady;
            bool current=rerollFeedbackStarted>=0&&Match.Player==rerollFeedbackPlayer&&
                Match.Round==rerollFeedbackRound&&Match.Phase==MatchPhase.Preparation;
            float elapsed=current?Time.unscaledTime-rerollFeedbackStarted:float.PositiveInfinity;
            float duration=Mathf.Max(.25f,rerollSpinDuration),t=Mathf.Clamp01(elapsed/duration);
            // State lives on the UI so rebuilding the offers halfway through does not reset the turn.
            float turn=t*t*t*(t*(6*t-15)+10);
            rerollSpinner.localRotation=Quaternion.Euler(0,0,-360*turn);
            float scale=1;
            if(t<.24f)scale=Mathf.Lerp(.86f,1.16f,Mathf.SmoothStep(0,1,t/.24f));
            else if(t<.56f)scale=Mathf.Lerp(1.16f,1,Mathf.SmoothStep(0,1,(t-.24f)/.32f));
            if(rerollPressed&&rerollButton.interactable)scale=.86f;
            rerollFeedbackRoot.localScale=Vector3.one*scale;
        }
    }

    public sealed class RerollPressFeedback : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IPointerExitHandler
    {
        LocalGameUI ui;Button button;
        public void Configure(LocalGameUI owner,Button target){ui=owner;button=target;}
        public void OnPointerDown(PointerEventData e)
        {if(e.button==PointerEventData.InputButton.Left&&button.IsInteractable())ui.SetRerollPressed(true);}
        public void OnPointerUp(PointerEventData e){ui.SetRerollPressed(false);}
        public void OnPointerExit(PointerEventData e){ui.SetRerollPressed(false);}
    }
}
