using MonsterPouch.Gameplay.Match;
using UnityEngine;
using UnityEngine.UI;
namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        float readyAmount;
        RectTransform readyFill;
        Text readyWord,readyCheck;
        Button readyButton;
        void RenderReadyButton(RectTransform tray)
        {
            var rect=Button(tray,"",new Rect(400,3+BankColumnOffset,100,42),()=>
            {CloseModal();if(Match.ToggleReady())PlaySound("button");},gold,ink,20);
            rect.name="LISTO";readyButton=rect.GetComponent<Button>();
            readyFill=Panel(rect,"Ready green fill",new Rect(0,0,100,42),new Color(.19f,.74f,.30f),false);
            readyWord=Label(rect,GameText.Ready,new Rect(0,0,100,42),20,ink,FontStyle.Bold);
            readyCheck=Label(rect,"✓",new Rect(0,0,100,42),28,Color.white,FontStyle.Bold);readyCheck.font=symbolFont;
            PaintReady();
        }
        void UpdateReadyMotion()
        {
            if(Match.Phase!=MatchPhase.Preparation){readyAmount=0;return;}
            readyAmount=Mathf.MoveTowards(readyAmount,Match.OnlineReady?1:0,Time.unscaledDeltaTime/.28f);PaintReady();
        }
        void PaintReady()
        {
            if(readyFill==null)return;
            float eased=readyAmount*readyAmount*(3-2*readyAmount);
            SetRect(readyFill,new Rect(0,0,100*eased,42));
            readyWord.color=new Color(ink.r,ink.g,ink.b,1-eased);readyCheck.color=new Color(1,1,1,eased);
            readyCheck.rectTransform.localScale=Vector3.one*(.75f+.25f*eased);
            readyButton.interactable=!Match.PreparationLocked&&Match.Remaining>0;
        }
    }
}
