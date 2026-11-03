using UnityEngine;

namespace MonsterPouch.Local
{
    public static class PortraitViewport
    {
        public const float Width=540, Height=960;
        public static Rect Fit(Vector2 screen, Rect safeArea, out float scale)
        {
            safeArea=NormalizeSafeArea(screen,safeArea);
            scale=Mathf.Max(.001f,Mathf.Min(safeArea.width/Width,safeArea.height/Height));
            var size=new Vector2(Width,Height)*scale;
            return new Rect(safeArea.center-size*.5f,size);
        }
        public static Rect NormalizeSafeArea(Vector2 screen, Rect safeArea)
        {
            screen=new Vector2(Mathf.Max(1,screen.x),Mathf.Max(1,screen.y));
            if(float.IsNaN(safeArea.x)||float.IsNaN(safeArea.y)||float.IsNaN(safeArea.width)||float.IsNaN(safeArea.height)||
               float.IsInfinity(safeArea.x)||float.IsInfinity(safeArea.y)||float.IsInfinity(safeArea.width)||float.IsInfinity(safeArea.height))
                safeArea=new Rect(Vector2.zero,screen);
            float left=Mathf.Clamp(safeArea.xMin,0,screen.x),right=Mathf.Clamp(safeArea.xMax,0,screen.x);
            float bottom=Mathf.Clamp(safeArea.yMin,0,screen.y),top=Mathf.Clamp(safeArea.yMax,0,screen.y);
            if(right<=left||top<=bottom)safeArea=new Rect(Vector2.zero,screen);
            else safeArea=Rect.MinMaxRect(left,bottom,right,top);
            return safeArea;
        }
    }
}
