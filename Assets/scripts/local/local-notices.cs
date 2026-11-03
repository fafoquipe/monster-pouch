using UnityEngine;
using UnityEngine.UI;
namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        TransientNoticeView errorNotice;
        void SetupNotices()
        {
            Match.ActionRejected+=ShowRejectedAction;
            online.ErrorReported+=ShowTransientNotice;
        }
        void ShowRejectedAction(string message)=>ShowTransientNotice(message,false);
        void ShowTransientNotice(string message,bool connection)
        {
            if(string.IsNullOrWhiteSpace(message)||!isActiveAndEnabled)return;
            if(errorNotice==null)
            {
                var root=Panel(CanvasRoot,"Transient error notice",new Rect(30,330,480,172),Color.clear,false);
                // A separate non-interactive canvas stays above sheets, and survives page rebuilds.
                var canvas=root.gameObject.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=200;
                var group=root.gameObject.AddComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=false;group.alpha=0;
                var label=Label(root,"",new Rect(8,20,464,132),28,Color.white,FontStyle.Bold);
                label.name="Error message";label.supportRichText=false;
                var outline=label.gameObject.AddComponent<Outline>();outline.effectColor=new Color(0,0,0,.85f);
                outline.effectDistance=new Vector2(2,-2);outline.useGraphicAlpha=true;
                var icon=Panel(root,"Connection warning",new Rect(207,0,66,42),Color.clear,false);
                var wifi=icon.gameObject.AddComponent<PixelWifiNotice>();wifi.color=new Color(1,1,1,.60f);wifi.raycastTarget=false;
                errorNotice=root.gameObject.AddComponent<TransientNoticeView>();errorNotice.Configure(group,label,icon);
            }
            errorNotice.Show(GameText.Resolve(message),connection);
        }
        void DisposeNotices()
        {
            if(Match!=null)Match.ActionRejected-=ShowRejectedAction;
            if(online!=null)online.ErrorReported-=ShowTransientNotice;
        }
    }
}
