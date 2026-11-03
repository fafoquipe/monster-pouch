using UnityEngine;

namespace MonsterPouch.Local
{
    // New menu stills register to the body and feet (or floating body center),
    // independently of combat sprite pivots and asymmetrical weapons/capes.
    public static class LobbyViewLayout
    {
        public static Vector2 SourceAnchor(LobbyFrameClip clip)
        {
            var a=clip.idleAnchor;
            return a!=null&&a.Length>=2?new Vector2(a[0],a[1]):
                new Vector2(clip.idleBounds[0]+clip.idleBounds[2]*.5f,clip.idleBounds[1]);
        }
        public static Vector2 TargetAnchor(Rect box,bool floating)
            =>floating?box.center:new Vector2(box.center.x,box.yMin);
        public static float FitScale(Rect box,LobbyFrameClip clip,float contextScale)
        {
            var b=clip.idleBounds;var a=SourceAnchor(clip);
            float width=2*Mathf.Max(a.x-b[0],b[0]+b[2]-a.x);
            float height=clip.idleFloating?2*Mathf.Max(a.y-b[1],b[1]+b[3]-a.y):b[1]+b[3]-a.y;
            return Mathf.Min(box.width*.94f/Mathf.Max(1,width),box.height*.94f/Mathf.Max(1,height))*Mathf.Max(0,contextScale);
        }
    }
}
