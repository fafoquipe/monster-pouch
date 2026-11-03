using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        Camera sceneryCamera;
        Image screenBackdrop;
        float menuExtraHeight;
        Rect appliedSafeArea;

        RectTransform modalScreenShield,modalScreenBackdrop;

        void BuildEdgeToEdgeBackdrop()
        {
            // The centered portrait owns all artwork; unused display space stays neutral.
            var cameraObject=new GameObject("Full screen scenery",typeof(Camera));
            cameraObject.transform.SetParent(transform,false);
            sceneryCamera=cameraObject.GetComponent<Camera>();
            var background=Panel(fullCanvas,"Full screen menu background",new Rect(),PixelScenery.MarginColor,false);
            background.SetAsFirstSibling();background.anchorMin=Vector2.zero;background.anchorMax=Vector2.one;
            background.offsetMin=background.offsetMax=Vector2.zero;
            screenBackdrop=background.GetComponent<Image>();

        }

        void ApplyViewportLayout(Rect safe)
        {
            var screen=new Vector2(Screen.width,Screen.height);
            appliedSafeArea=PortraitViewport.NormalizeSafeArea(screen,safe);
            var viewport=PortraitViewport.Fit(screen,appliedSafeArea,out float scale);
            canvasScaler.scaleFactor=scale;
            canvasScaler.GetComponent<Canvas>().scaleFactor=scale;
            CanvasRoot.anchoredPosition=(viewport.center-new Vector2(Screen.width*.5f,Screen.height*.5f))/scale;
            CanvasRoot.sizeDelta=new Vector2(540,960);
            GameCamera.rect=new Rect(viewport.x/Screen.width,viewport.y/Screen.height,viewport.width/Screen.width,viewport.height/Screen.height);
            // Scenery and every control share one immutable portrait composition.
            menuExtraHeight=0;
            GameCamera.aspect=PortraitViewport.Width/PortraitViewport.Height;
            GameCamera.ResetProjectionMatrix();
            sceneryCamera.CopyFrom(GameCamera);
            sceneryCamera.rect=new Rect(0,0,1,1);
            sceneryCamera.aspect=(float)Screen.width/Screen.height;
            sceneryCamera.ResetProjectionMatrix();
            sceneryCamera.depth=GameCamera.depth-1;
            sceneryCamera.clearFlags=CameraClearFlags.SolidColor;
            sceneryCamera.backgroundColor=PixelScenery.MarginColor;
            sceneryCamera.cullingMask=GameCamera.cullingMask;
            sceneryCamera.orthographicSize=GameCamera.orthographicSize*Screen.height/viewport.height;
            var offset=(new Vector2(Screen.width,Screen.height)*.5f-viewport.center)*(2*GameCamera.orthographicSize/viewport.height);
            sceneryCamera.transform.SetPositionAndRotation(GameCamera.transform.position+new Vector3(offset.x,offset.y),GameCamera.transform.rotation);
            UpdateEdgeToEdgeVisibility();
            if(page!=null&&Match.Phase==MonsterPouch.Gameplay.Match.MatchPhase.Menu)rebuild=true;
            Canvas.ForceUpdateCanvases();
        }

        void UpdateEdgeToEdgeVisibility()
        {
            bool menu=Match.Phase==MonsterPouch.Gameplay.Match.MatchPhase.Menu;
            screenBackdrop.gameObject.SetActive(menu);
            sceneryCamera.enabled=!menu;
            if(menuSwipeSurface!=null)
            {
                menuSwipeSurface.gameObject.SetActive(menu);
                if(menu)
                {
                    Vector2 lower=ToCanvas(Vector2.zero),upper=ToCanvas(new Vector2(Screen.width,Screen.height));
                    SetRect(menuSwipeSurface,Rect.MinMaxRect(lower.x,upper.y,upper.x,lower.y));
                }
            }
        }

        Rect MenuRect(Rect rect,float anchor)
        {
            rect.y+=(anchor-.5f)*menuExtraHeight;
            return rect;
        }

        // Taller menus extend past the compact board layout. Modals must still
        // intercept touches over the entire physical display.
        void UpdateModalScreenShield()
        {
            if(modal==null)return;
            if(modalScreenBackdrop==null||modalScreenBackdrop.parent!=modal)
            {
                var original=modal.GetComponent<Image>();
                modalScreenBackdrop=Panel(modal,"Full screen modal shade",new Rect(),original.color,false);
                modalScreenBackdrop.SetAsFirstSibling();original.color=Color.clear;
            }
            // Coordinates relative to the stationary root keep this backdrop attached
            // to its page during a swipe, while covering tall phones edge to edge.
            Vector2 lower=ToCanvas(Vector2.zero),upper=ToCanvas(new Vector2(Screen.width,Screen.height));
            SetRect(modalScreenBackdrop,Rect.MinMaxRect(lower.x,upper.y,upper.x,lower.y));
            if(modalScreenShield==null||modalScreenShield.parent!=modal)
            {
                modalScreenShield=HitArea(modal,"Full screen modal touch shield",new Rect());
                modalScreenShield.SetAsFirstSibling();
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(modal,Vector2.zero,null,out var bottom);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(modal,new Vector2(Screen.width,Screen.height),null,out var top);
            modalScreenShield.anchorMin=modalScreenShield.anchorMax=modalScreenShield.pivot=Vector2.zero;
            modalScreenShield.anchoredPosition=bottom-modal.rect.min;
            modalScreenShield.sizeDelta=top-bottom;
        }

        Vector2 WorldToPortrait(Vector3 world)
        {
            var point=GameCamera.WorldToViewportPoint(world);
            return new Vector2(point.x*540,(1-point.y)*960);
        }
    }
}

