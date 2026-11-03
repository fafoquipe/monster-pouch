using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        RectTransform MoonkenCost(Transform parent,int amount,Rect rect)
        {
            var badge=Panel(parent,"Moonken cost",rect,Color.clear,false);
            Image(badge,MoonIcon,new Rect(0,0,rect.width,rect.height));
            var number=Label(badge,amount.ToString(),new Rect(0,0,rect.width,rect.height),
                Mathf.RoundToInt(rect.height*.48f),Color.white,FontStyle.Bold);
            number.name="Cost value";number.lineSpacing=1;
            number.resizeTextForBestFit=true;number.resizeTextMinSize=10;number.resizeTextMaxSize=number.fontSize;
            var outline=number.gameObject.AddComponent<Outline>();
            outline.effectColor=new Color(.09f,.065f,.22f,1);outline.effectDistance=new Vector2(1.5f,-1.5f);
            return badge;
        }

        // Keep the caption and currency together as one centered group.
        void SetMoonkenButtonCost(RectTransform button,string caption,int? cost)
        {
            var label=button.Find("Text").GetComponent<Text>();label.text=GameText.Resolve(caption);
            var badge=button.Find("Moonken cost") as RectTransform;
            if(cost.HasValue)
            {
                float diameter=Mathf.Min(44,button.rect.height-8);
                if(badge==null)badge=MoonkenCost(button,cost.Value,new Rect(0,0,diameter,diameter));
                badge.gameObject.SetActive(true);badge.GetComponentInChildren<Text>().text=cost.Value.ToString();
                float width=Mathf.Min(label.preferredWidth+8,button.rect.width-diameter-32);
                float left=(button.rect.width-width-12-diameter)*.5f;
                SetRect(label.rectTransform,new Rect(left,4,width,button.rect.height-8));
                SetRect(badge,new Rect(left+width+12,(button.rect.height-diameter)*.5f,diameter,diameter));
            }
            else
            {
                if(badge!=null)badge.gameObject.SetActive(false);
                SetRect(label.rectTransform,new Rect(12,4,button.rect.width-24,button.rect.height-8));
            }
        }
    }
}
