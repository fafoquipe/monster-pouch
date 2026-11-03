using System;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public enum BoardMenuSymbol { Shop, Brief, Battle, Guide, Challenges }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BoardMenuFace : MaskableGraphic
    {
        public bool Selected;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect r=rectTransform.rect;
            void Box(float x,float y,float w,float h,Color tint)
            {
                int n=mesh.currentVertCount; tint*=color;
                mesh.AddVert(new Vector2(x,y),tint,Vector2.zero);mesh.AddVert(new Vector2(x,y+h),tint,Vector2.zero);
                mesh.AddVert(new Vector2(x+w,y+h),tint,Vector2.zero);mesh.AddVert(new Vector2(x+w,y),tint,Vector2.zero);
                mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);
            }
            void Face(float inset,Color tint)
            {
                float x=r.xMin+inset,y=r.yMin+inset,w=r.width-2*inset,h=r.height-2*inset;
                Box(x+3,y,w-6,h,tint);Box(x,y+3,w,h-6,tint);
            }
            Face(0,Selected?BoardMenuStyle.Edge:new Color32(79,88,75,255));
            Face(2,Selected?new Color32(89,98,81,255):BoardMenuStyle.Tile);
            Box(r.xMin+7,r.yMax-5,r.width-14,1,Selected?new Color32(139,146,119,255):new Color32(86,99,82,255));
            Box(r.xMin+5,r.yMin+2,r.width-10,2,BoardMenuStyle.Ink);
        }
    }

    public static class BoardMenuStyle
    {
        public static readonly Color Ink=new Color32(37,44,38,255);
        public static readonly Color Tile=new Color32(56,65,55,255);
        public static readonly Color Paper=new Color32(227,220,199,255);
        public static readonly Color Muted=new Color32(130,144,120,255);
        public static readonly Color Edge=new Color32(186,169,128,255);
        static readonly string[] Icons={"shop","brief","battle","guide","challenges"};

        static RectTransform Rect(Transform parent,string name,UnityEngine.Rect bounds)
        {
            var go=new GameObject(name,typeof(RectTransform));var rt=(RectTransform)go.transform;
            rt.SetParent(parent,false);LocalGameUI.SetRect(rt,bounds);return rt;
        }

        public static RectTransform Tab(Transform parent,Rect bounds,BoardMenuSymbol symbol,string caption,bool selected,Action action)
        {
            var tab=Rect(parent,"nav-"+caption,bounds);
            var face=tab.gameObject.AddComponent<BoardMenuFace>();face.Selected=selected;
            var button=tab.gameObject.AddComponent<Button>();button.targetGraphic=face;
            button.onClick.AddListener(()=>action());
            var colors=button.colors;colors.normalColor=Color.white;colors.selectedColor=Color.white;
            colors.highlightedColor=new Color(.92f,.97f,.92f);colors.pressedColor=new Color(.72f,.78f,.72f);
            colors.fadeDuration=.08f;button.colors=colors;
            var icon=Rect(tab,"Board symbol",new Rect(9,0,84,80)).gameObject.AddComponent<Image>();
            icon.sprite=Resources.Load<Sprite>("MonsterPouch/UI/BoardMenu/"+Icons[(int)symbol]);
            icon.preserveAspect=true;icon.raycastTarget=false;
            var label=Rect(tab,"Text",new Rect(2,79,98,22)).gameObject.AddComponent<Text>();
            label.font=Resources.Load<Font>("MonsterPouch/UI/Fonts/Lato-Bold");label.fontSize=14;
            label.text=caption;label.color=selected?Paper:new Color32(196,202,185,255);
            label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;
            if(selected)
            {
                var marker=Rect(tab,"Selected tab",new Rect(35,104,32,2)).gameObject.AddComponent<Image>();
                marker.color=Edge;marker.raycastTarget=false;
            }
            return tab;
        }
    }
}
