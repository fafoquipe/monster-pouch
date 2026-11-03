using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    // Animate the drawing, leaving the hit rectangle and layout anchors stationary.
    public sealed class RetroButtonMotion : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        public static readonly Color Cream = new Color(.96f,.91f,.78f);
        public static readonly Color Gold = new Color(.80f,.63f,.32f);
        public static readonly Color Red = new Color(.57f,.25f,.26f);
        Button button;
        RetroButtonFace face;
        RetroButtonInk[] ink;
        bool pressed, hovered, selected;
        float offset, velocity, punch;
        internal float Offset => offset;
        internal float Scale => 1f + punch * .022f;
        internal bool Available => button != null && button.IsInteractable();
        internal RectTransform Bounds => (RectTransform)transform;

        public static void Attach(RectTransform root, Button button, Color fill)
        {
            var motion = root.gameObject.AddComponent<RetroButtonMotion>();
            motion.button = button;
            var hit = root.GetComponent<Image>();
            if (hit != null) { hit.color = Color.clear; hit.canvasRenderer.cullTransparentMesh = false; }
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child.name == "Pixel edge" || child.name == "Pixel shade" || child.name == "Top shine")
                { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            }
            var go = new GameObject("Button face", typeof(RectTransform));
            var rt = (RectTransform)go.transform; rt.SetParent(root, false); rt.SetAsFirstSibling();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            motion.face = go.AddComponent<RetroButtonFace>();
            motion.face.Owner = motion; motion.face.color = fill; motion.face.raycastTarget = false;
            button.transition = Selectable.Transition.None;
            button.targetGraphic = motion.face;
        }
        void Start()
        {
            // Labels and icons may be appended after the button factory returns.
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.transform == transform || graphic == face || !graphic.gameObject.activeSelf) continue;
                var effect = graphic.GetComponent<RetroButtonInk>() ?? graphic.gameObject.AddComponent<RetroButtonInk>();
                effect.Owner = this;
            }
            ink = GetComponentsInChildren<RetroButtonInk>(true);
        }
        void Update()
        {
            if (!Available) { pressed = hovered = selected = false; }
            float target = pressed ? -4f : hovered || selected ? 1f : 0f;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .033f);
            float previous = offset, previousPunch = punch;
            offset = Mathf.SmoothDamp(offset, target, ref velocity, .065f, 200, dt);
            punch = Mathf.MoveTowards(punch, 0, dt * 5);
            if (Mathf.Abs(offset-target)<.005f) offset=target;
            bool stateChanged = face != null && face.Disabled == Available;
            if (previous == offset && previousPunch == punch && !stateChanged) return;
            if (face != null) { face.Disabled = !Available; face.SetVerticesDirty(); }
            if (ink != null) foreach (var effect in ink) if (effect != null) effect.Refresh();
        }
        public void OnPointerDown(PointerEventData e) { if (Available && e.button == PointerEventData.InputButton.Left) pressed = true; }
        public void OnPointerUp(PointerEventData e) { if (pressed && Available && e.eligibleForClick) punch = 1; pressed = false; }
        public void OnPointerEnter(PointerEventData e) { if (e.pointerId < 0) hovered = true; }
        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
        public void OnSelect(BaseEventData e) { selected = true; }
        public void OnDeselect(BaseEventData e) { selected = false; }
        public void OnSubmit(BaseEventData e) { if (Available) punch = 1; }
        void ResetMotion() { pressed = hovered = selected = false; offset = velocity = punch = 0; if(face!=null)face.SetVerticesDirty(); }
        void OnDisable() { ResetMotion(); }
        void OnApplicationFocus(bool focused) { if (!focused) ResetMotion(); }
        internal Vector3 TransformPoint(Vector3 point)
        {
            var center = Bounds.rect.center;
            return new Vector3(center.x + (point.x-center.x)*Scale, center.y + (point.y-center.y)*Scale + Offset, point.z);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RetroButtonFace : MaskableGraphic
    {
        internal RetroButtonMotion Owner;
        internal bool Disabled;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (Owner == null) return;
            Rect r = rectTransform.rect;
            Color fill = Disabled ? Color.Lerp(color, new Color(.25f,.27f,.29f), .7f) : color;
            Draw(vh, new Rect(r.x, r.y-4, r.width, r.height), new Color(.10f,.12f,.14f), false);
            Draw(vh, r, new Color(.17f,.18f,.19f), true);
            Draw(vh, new Rect(r.x+3,r.y+3,r.width-6,r.height-6), fill, true);
            Box(vh, new Rect(r.x+9,r.yMax-6,r.width-18,2), Color.Lerp(fill,RetroButtonMotion.Cream,.28f),true);
        }
        void Draw(VertexHelper vh, Rect r, Color c, bool move)
        {
            float step = Mathf.Min(5, r.height/4);
            Box(vh,new Rect(r.x+step,r.y,r.width-2*step,step),c,move);
            Box(vh,new Rect(r.x,r.y+step,r.width,r.height-2*step),c,move);
            Box(vh,new Rect(r.x+step,r.yMax-step,r.width-2*step,step),c,move);
        }
        void Box(VertexHelper vh,Rect r,Color c,bool move)
        {
            if(r.width<=0||r.height<=0)return;
            int n=vh.currentVertCount;
            Add(new Vector3(r.xMin,r.yMin)); Add(new Vector3(r.xMin,r.yMax));
            Add(new Vector3(r.xMax,r.yMax)); Add(new Vector3(r.xMax,r.yMin));
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            void Add(Vector3 p)
            {
                if(move) p=rectTransform.InverseTransformPoint(Owner.transform.TransformPoint(Owner.TransformPoint(Owner.transform.InverseTransformPoint(rectTransform.TransformPoint(p)))));
                vh.AddVert(p,c,Vector2.zero);
            }
        }
    }
    public sealed class RetroButtonInk : BaseMeshEffect
    {
        internal RetroButtonMotion Owner;
        internal void Refresh() { if(graphic!=null)graphic.SetVerticesDirty(); }
        public override void ModifyMesh(VertexHelper vh)
        {
            if(!IsActive()||Owner==null)return;
            var toRoot=Owner.transform.worldToLocalMatrix*transform.localToWorldMatrix;
            var fromRoot=transform.worldToLocalMatrix*Owner.transform.localToWorldMatrix;
            var v=new UIVertex();
            for(int i=0;i<vh.currentVertCount;i++)
            { vh.PopulateUIVertex(ref v,i);v.position=fromRoot.MultiplyPoint3x4(Owner.TransformPoint(toRoot.MultiplyPoint3x4(v.position)));vh.SetUIVertex(v,i); }
        }
    }
}
