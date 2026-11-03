using MonsterPouch.Gameplay.Presentation;
using UnityEngine;

namespace MonsterPouch.Local
{
    // Preserve only the inherited rectangle for runtime and editor preview.
    // LobbyFrameGraphic applies the custom context percentage after its fit.
    public static class CharacterMenuFigureLayout
    {
        public static float InheritedScale(string id,UnitArt art,bool inheritedSize,string surface)
        {
            float scale=1;
            if(inheritedSize)
            {
                scale=id=="trimol"?1.10f:art?.DisplayScale??1f;
                if(surface=="Choose featured Monster")
                {
                    if(id=="popow")scale*=.90f;
                    if(id=="kazuru")scale*=1.40f;
                }
                if(surface=="deck-monster")scale=Mathf.Min(scale,1.02f);
                else if(surface=="Figure stage")scale=Mathf.Min(scale,1.04f);
            }
            return scale;
        }
        public static Rect Frame(Rect rect,string id,UnitArt art,bool inheritedSize,string surface)
        {
            float scale=InheritedScale(id,art,inheritedSize,surface);
            return new Rect(rect.center.x-rect.width*scale*.5f,rect.yMax-rect.height*scale,rect.width*scale,rect.height*scale);
        }
    }
}
