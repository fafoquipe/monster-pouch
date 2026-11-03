using System.Collections;
using MonsterPouch.Gameplay.Match;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        int menuTab=2;
        RectTransform menuNavigation,outgoingMenu,menuSwipeSurface;
        Coroutine menuSlide;
        bool navigatingMenu;
        RectTransform menuPreview,menuPreviewModal;
        int menuSwipeOrigin,menuPreviewTab=-1;
        float menuSwipeWidth,menuSwipeOffset;
        bool menuDragging,menuSwipeSettling,menuSwipeCommit;
        public int MenuTab=>menuTab;
        public bool CanSwipeMenu=>Match.Phase==MatchPhase.Menu&&ModernMenu&&
            (modal==null||modal.name=="Help"||modal.name=="Audio"||modal.name=="Scenario skins"||
             (modal.name=="Character selection"&&collectionPickerSlot==-2));

        static string MenuPageForTab(int index)=>index==0?"shop":index==1?"decks":index==4?"challenges":"home";
        float MenuSlideWidth=>Mathf.Max(540,Screen.width/Mathf.Max(.01f,canvasScaler.scaleFactor));

        public bool BeginMenuSwipe(Vector2 delta)
        {
            if(!CanSwipeMenu||menuDragging||Mathf.Abs(delta.x)<=Mathf.Abs(delta.y)*1.25f)return false;
            // Settle the previously chosen page before accepting the next gesture.
            // A transition must never consume a fresh drag or require another press.
            StopMenuSlide();
            menuSwipeOrigin=menuTab;menuSwipeWidth=MenuSlideWidth;menuSwipeOffset=0;
            menuDragging=true;menuSwipeCommit=false;
            SetMenuInput(false);
            UpdateMenuSwipe(delta);
            return true;
        }
        public void UpdateMenuSwipe(Vector2 delta)
        {
            if(!menuDragging)return;
            float offset=delta.x/Mathf.Max(.01f,canvasScaler.scaleFactor);
            int target=offset<0?menuSwipeOrigin+1:menuSwipeOrigin-1;
            if(target>=0&&target<=4)
            {
                if(menuPreviewTab!=target)BuildMenuPreview(target);
                offset=Mathf.Clamp(offset,-menuSwipeWidth,menuSwipeWidth);
            }
            else
            {
                DiscardMenuPreview();
                // A little resistance at either end, without wrapping the tabs.
                offset=Mathf.Clamp(offset*.18f,-menuSwipeWidth*.12f,menuSwipeWidth*.12f);
            }
            PositionMenuSwipe(offset);
        }
        public void EndMenuSwipe(Vector2 delta)
        {
            if(!menuDragging)return;
            UpdateMenuSwipe(delta);menuDragging=false;menuSwipeSettling=true;
            // Only the page covering more than half the screen wins, even after a fast flick.
            menuSwipeCommit=menuPreview!=null&&Mathf.Abs(menuSwipeOffset)>menuSwipeWidth*.5f+.001f;
            if(menuSwipeCommit){menuTab=menuPreviewTab;menuPage=MenuPageForTab(menuTab);}
            menuSlide=StartCoroutine(SettleMenuSwipe());
        }
        public void CancelMenuSwipe()
        {
            if(!menuDragging)return;
            menuDragging=false;FinishMenuSwipe();
        }
        public void SwipeMenu(Vector2 delta)
        {
            if(BeginMenuSwipe(delta))EndMenuSwipe(delta);
        }
        void BuildMenuPreview(int index)
        {
            DiscardMenuPreview();
            menuPreview=Panel(CanvasRoot,"Menu swipe preview",new Rect(0,0,540,960),Color.clear,false);
            var group=menuPreview.gameObject.AddComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=false;
            var currentPage=page;var currentModal=modal;string currentMenuPage=menuPage;
            var currentBackdrop=screenBackdrop.sprite;var currentColor=screenBackdrop.color;
            var currentModalBackdrop=modalScreenBackdrop;var currentModalShield=modalScreenShield;
            try
            {
                // Render alongside the current page so cancelling preserves its controls and scroll position.
                page=menuPreview;modal=null;menuPage=MenuPageForTab(index);
                if(menuPage=="decks")RenderDeckRoom();
                else if(menuPage=="shop"||menuPage=="challenges")RenderComingSoon();
                else RenderHomeRoom();
                if(index==3)
                {
                    ShowHelp();UpdateModalScreenShield();menuPreviewModal=modal;
                    menuPreviewModal.SetParent(menuPreview,false);
                }
                menuPreviewTab=index;
            }
            finally
            {
                page=currentPage;modal=currentModal;menuPage=currentMenuPage;
                screenBackdrop.sprite=currentBackdrop;screenBackdrop.color=currentColor;
                modalScreenBackdrop=currentModalBackdrop;modalScreenShield=currentModalShield;
            }
            RaiseMenuNavigation();
        }
        void PositionMenuSwipe(float offset)
        {
            menuSwipeOffset=offset;page.anchoredPosition=new Vector2(offset,0);
            if(modal!=null)modal.anchoredPosition=page.anchoredPosition;
            if(menuPreview!=null)
                menuPreview.anchoredPosition=new Vector2(offset+(menuPreviewTab>menuSwipeOrigin?menuSwipeWidth:-menuSwipeWidth),0);
        }
        IEnumerator SettleMenuSwipe()
        {
            float start=menuSwipeOffset;
            float end=menuSwipeCommit?(menuPreviewTab>menuSwipeOrigin?-menuSwipeWidth:menuSwipeWidth):0;
            float duration=Mathf.Lerp(.26f,.36f,Mathf.Abs(end-start)/menuSwipeWidth);
            float age=0;
            while(age<duration&&Match.Phase==MatchPhase.Menu)
            {
                // Unscaled time also keeps this responsive while the game is paused.
                age+=Time.unscaledDeltaTime;
                PositionMenuSwipe(Mathf.Lerp(start,end,Mathf.SmoothStep(0,1,Mathf.Clamp01(age/duration))));
                yield return null;
            }
            FinishMenuSwipe();menuSlide=null;
        }
        void FinishMenuSwipe()
        {
            menuDragging=false;menuSwipeSettling=false;
            if(menuSwipeCommit&&menuPreview!=null)
            {
                foreach(Transform child in page){child.gameObject.SetActive(false);Destroy(child.gameObject);}
                CloseModal();
                if(menuPreviewModal!=null)
                {
                    modal=menuPreviewModal;modal.SetParent(CanvasRoot,false);menuPreviewModal=null;
                    modalScreenBackdrop=modal.Find("Full screen modal shade") as RectTransform;
                    modalScreenShield=modal.Find("Full screen modal touch shield") as RectTransform;
                }
                var backdrop=menuPreview.Find("Menu background")?.GetComponent<Image>();
                screenBackdrop.sprite=null;
                screenBackdrop.color=LobbyArtworkForMenuAvailable?Color.black:PixelScenery.MarginColor;
                while(menuPreview.childCount>0)menuPreview.GetChild(0).SetParent(page,false);
                if(menuNavigation!=null){menuNavigation.gameObject.SetActive(false);Destroy(menuNavigation.gameObject);}
                menuNavigation=null;RenderNavigation();
            }
            menuSwipeCommit=false;DiscardMenuPreview();
            menuSwipeOffset=0;page.anchoredPosition=Vector2.zero;
            if(modal!=null)modal.anchoredPosition=Vector2.zero;
            SetMenuInput(true);RaiseMenuNavigation();
        }
        void DiscardMenuPreview()
        {
            if(menuPreview!=null){menuPreview.gameObject.SetActive(false);Destroy(menuPreview.gameObject);}
            menuPreview=null;menuPreviewModal=null;menuPreviewTab=-1;
        }
        static void SetMenuRootInput(RectTransform root,bool enabled)
        {
            if(root==null)return;
            var group=root.GetComponent<CanvasGroup>();if(group==null)group=root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts=enabled;group.interactable=enabled;
        }
        void SetMenuInput(bool enabled)
        {
            SetMenuRootInput(page,enabled);SetMenuRootInput(modal,enabled);SetMenuRootInput(menuNavigation,enabled);
        }
        public void NavigateMenu(int index)
        {
            if(Match.Phase!=MatchPhase.Menu||index<0||index>4)return;
            StopMenuSlide();
            int previous=menuTab;
            if(previous!=index)
            {
                outgoingMenu=Panel(CanvasRoot,"Outgoing menu",new Rect(0,0,540,960),Color.clear,false);
                var group=outgoingMenu.gameObject.AddComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=false;
                while(page.childCount>0)page.GetChild(0).SetParent(outgoingMenu,false);
                if(modal!=null){modal.SetParent(outgoingMenu,false);modal=null;}
            }
            menuTab=index;menuPage=MenuPageForTab(index);
            navigatingMenu=true;CloseModal();RenderPage();
            if(index==3)ShowHelp();
            navigatingMenu=false;
            RaiseMenuNavigation();
            if(previous!=index)menuSlide=StartCoroutine(SlideMenu(index>previous?1:-1));
        }
        IEnumerator SlideMenu(int direction)
        {
            var incomingModal=modal;
            float width=MenuSlideWidth;
            var pageGroup=page.GetComponent<CanvasGroup>();if(pageGroup==null)pageGroup=page.gameObject.AddComponent<CanvasGroup>();
            pageGroup.alpha=1;pageGroup.blocksRaycasts=false;
            CanvasGroup modalGroup=null;
            if(incomingModal!=null){modalGroup=incomingModal.GetComponent<CanvasGroup>();if(modalGroup==null)modalGroup=incomingModal.gameObject.AddComponent<CanvasGroup>();modalGroup.blocksRaycasts=false;}
            float age=0;
            while(age<.24f&&Match.Phase==MatchPhase.Menu)
            {
                float t=Mathf.Clamp01(age/.24f),ease=1-Mathf.Pow(1-t,3);
                page.anchoredPosition=new Vector2(direction*width*(1-ease),0);
                if(incomingModal!=null)incomingModal.anchoredPosition=page.anchoredPosition;
                if(outgoingMenu!=null)outgoingMenu.anchoredPosition=new Vector2(-direction*width*ease,0);
                age+=Time.unscaledDeltaTime;yield return null;
            }
            page.anchoredPosition=Vector2.zero;pageGroup.blocksRaycasts=true;
            if(incomingModal!=null)incomingModal.anchoredPosition=Vector2.zero;
            if(modalGroup!=null)modalGroup.blocksRaycasts=true;
            if(outgoingMenu!=null)Destroy(outgoingMenu.gameObject);outgoingMenu=null;menuSlide=null;
        }
        void StopMenuSlide()
        {
            if(menuSlide!=null)StopCoroutine(menuSlide);menuSlide=null;
            if(menuDragging||menuSwipeSettling)FinishMenuSwipe();
            if(page!=null){page.anchoredPosition=Vector2.zero;var group=page.GetComponent<CanvasGroup>();if(group!=null){group.alpha=1;group.blocksRaycasts=true;}}
            if(modal!=null){modal.anchoredPosition=Vector2.zero;var group=modal.GetComponent<CanvasGroup>();if(group!=null)group.blocksRaycasts=true;}
            if(outgoingMenu!=null){outgoingMenu.gameObject.SetActive(false);Destroy(outgoingMenu.gameObject);}outgoingMenu=null;
        }
        void ClearMenuNavigation()
        {
            if(!navigatingMenu)StopMenuSlide();
            if(menuNavigation!=null){menuNavigation.gameObject.SetActive(false);Destroy(menuNavigation.gameObject);}menuNavigation=null;
        }
        void RaiseMenuNavigation()
        {
            if(menuNavigation!=null&&CanSwipeMenu)menuNavigation.SetAsLastSibling();
        }
    }

    // Buttons inherit this drag handler. Unity cancels their click when a swipe starts.
    public sealed class MenuSwipeGesture:MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        LocalGameUI ui;bool accepted;int pointerId;
        public void Configure(LocalGameUI owner){ui=owner;}
        public void OnBeginDrag(PointerEventData data)
        {
            if(accepted||data.button!=PointerEventData.InputButton.Left)return;
            accepted=ui!=null&&ui.BeginMenuSwipe(data.position-data.pressPosition);pointerId=data.pointerId;
            if(accepted)data.eligibleForClick=false;
        }
        public void OnDrag(PointerEventData data)
        {
            if(!accepted||data.pointerId!=pointerId)return;
            data.eligibleForClick=false;ui.UpdateMenuSwipe(data.position-data.pressPosition);
        }
        public void OnEndDrag(PointerEventData data)
        {
            if(!accepted||data.pointerId!=pointerId)return;
            accepted=false;data.eligibleForClick=false;ui.EndMenuSwipe(data.position-data.pressPosition);
        }
        void Cancel(){if(accepted&&ui!=null)ui.CancelMenuSwipe();accepted=false;}
        void OnDisable(){Cancel();}
        void OnApplicationFocus(bool focused){if(!focused)Cancel();}
        void OnApplicationPause(bool paused){if(paused)Cancel();}
    }

    // Lock the gesture's axis at the start so vertical collection scrolling stays independent.
    public sealed class MenuCollectionScroll:ScrollRect
    {
        public LocalGameUI Owner;
        bool swipingHorizontally;int pointerId;
        public override void OnBeginDrag(PointerEventData data)
        {
            if(swipingHorizontally||data.button!=PointerEventData.InputButton.Left)return;
            swipingHorizontally=Owner!=null&&Owner.BeginMenuSwipe(data.position-data.pressPosition);pointerId=data.pointerId;
            if(swipingHorizontally){StopMovement();data.eligibleForClick=false;}else base.OnBeginDrag(data);
        }
        public override void OnDrag(PointerEventData data)
        {
            if(swipingHorizontally)
            {
                if(data.pointerId!=pointerId)return;
                data.eligibleForClick=false;Owner.UpdateMenuSwipe(data.position-data.pressPosition);
            }
            else base.OnDrag(data);
        }
        public override void OnEndDrag(PointerEventData data)
        {
            if(swipingHorizontally)
            {
                if(data.pointerId!=pointerId)return;
                swipingHorizontally=false;data.eligibleForClick=false;Owner.EndMenuSwipe(data.position-data.pressPosition);
            }
            else base.OnEndDrag(data);
        }
        void CancelSwipe(){if(swipingHorizontally&&Owner!=null)Owner.CancelMenuSwipe();swipingHorizontally=false;}
        protected override void OnDisable(){CancelSwipe();base.OnDisable();}
        void OnApplicationFocus(bool focused){if(!focused)CancelSwipe();}
        void OnApplicationPause(bool paused){if(paused)CancelSwipe();}
    }
}
