using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace MonsterPouch.Local
{
    // Error-only overlay: no input capture, no message queue and no dependency on the match clock.
    public sealed class TransientNoticeView:MonoBehaviour
    {
        CanvasGroup group;
        Text message;
        RectTransform connectionIcon;
        Coroutine fadeRoutine;
        public void Configure(CanvasGroup opacity,Text label,RectTransform icon)
        {group=opacity;message=label;connectionIcon=icon;}
        public void Show(string text,bool connection)
        {
            if(fadeRoutine!=null)StopCoroutine(fadeRoutine);
            gameObject.SetActive(true);message.text=text;
            connectionIcon.gameObject.SetActive(connection);
            LocalGameUI.SetRect(message.rectTransform,new Rect(8,connection?54:20,464,connection?118:132));
            fadeRoutine=StartCoroutine(Display(Mathf.Clamp(1.2f+text.Length*.018f,1.8f,3.5f)));
        }
        IEnumerator Display(float hold)
        {
            float initial=group.alpha;
            for(float t=0;t<.18f;t+=Time.unscaledDeltaTime)
            {group.alpha=Mathf.Lerp(initial,1,Mathf.SmoothStep(0,1,t/.18f));yield return null;}
            group.alpha=1;
            yield return new WaitForSecondsRealtime(hold);
            for(float t=0;t<.45f;t+=Time.unscaledDeltaTime)
            {group.alpha=1-Mathf.SmoothStep(0,1,t/.45f);yield return null;}
            group.alpha=0;fadeRoutine=null;gameObject.SetActive(false);
        }
        void OnDisable(){if(group!=null)group.alpha=0;fadeRoutine=null;}
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PixelWifiNotice:MaskableGraphic
    {
        static readonly string[] Pixels={
            "0000011111111100000",
            "0001111111111111000",
            "0011100000000011100",
            "0110000000000000110",
            "0000000000000000000",
            "0000001111111000000",
            "0000111111111110000",
            "0001110000000111000",
            "0000000000000000000",
            "0000000011100000000",
            "0000000111110000000",
            "0000000111110000000"};
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;
            float pixel=Mathf.Min(r.width/19,r.height/Pixels.Length);
            float left=r.center.x-19*pixel*.5f,top=r.center.y+Pixels.Length*pixel*.5f;
            for(int row=0;row<Pixels.Length;row++)for(int col=0;col<Pixels[row].Length;col++)
            {
                if(Pixels[row][col]!='1')continue;
                float x=left+col*pixel,y=top-row*pixel;int i=mesh.currentVertCount;
                mesh.AddVert(new Vector3(x,y),color,Vector2.zero);mesh.AddVert(new Vector3(x,y-pixel),color,Vector2.zero);
                mesh.AddVert(new Vector3(x+pixel,y-pixel),color,Vector2.zero);mesh.AddVert(new Vector3(x+pixel,y),color,Vector2.zero);
                mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
            }
        }
    }
}
